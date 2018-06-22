using System;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using Microsoft.Win32;
using System.Timers;
using System.Threading;

namespace NtpTimeSyncService
{
    internal struct Timestamp
    {
        public readonly uint seconds, fraction;

        public Timestamp(DateTime dateTime)
        {
            var time = dateTime.ToUniversalTime() - new DateTime(1900, 1, 1);
            seconds = (uint)time.TotalSeconds;
            fraction = (uint)((time.TotalSeconds - seconds) * (1UL << 32));
        }

        public Timestamp(uint seconds, uint fraction)
        {
            this.seconds = seconds;
            this.fraction = fraction;
        }

        public Timestamp(byte[] bytes)
        {
            if (bytes.Length < 8)
                throw new ArgumentException($"Timestamp constructor requires at least 8 bytes but {bytes.Length} given.");
            seconds = (uint)bytes[3] | (uint)bytes[2] << 8 | (uint)bytes[1] << 16 | (uint)bytes[0] << 24;
            fraction = (uint)bytes[7] | (uint)bytes[6] << 8 | (uint)bytes[5] << 16 | (uint)bytes[4] << 24;
        }

        public byte[] ToBytes()
        {
            var bytes = new byte[8];
            bytes[0] = (byte)((seconds & 0xff000000) >> 24);
            bytes[1] = (byte)((seconds & 0x00ff0000) >> 16);
            bytes[2] = (byte)((seconds & 0x0000ff00) >> 8);
            bytes[3] = (byte)(seconds & 0x000000ff);
            bytes[4] = (byte)((fraction & 0xff000000) >> 24);
            bytes[5] = (byte)((fraction & 0x00ff0000) >> 16);
            bytes[6] = (byte)((fraction & 0x0000ff00) >> 8);
            bytes[7] = (byte)(fraction & 0x000000ff);
            return bytes;
        }
    }

    internal enum Mode
    {
        Reserved = 0,
        SymmetricPassive = 1,
        SymmetricActive = 2,
        Client = 3,
        Server = 4,
        Broadcast = 5,
        ManagmentNtpMessage = 6,
        PrivateUseReserved = 7
    }

    internal struct SntpFrame
    {
        public LeapIndicator LI { get; private set; }
        public int VersionNumber { get; }
        public Mode Mode { get; private set; }
        public byte Stratum { get; set; }
        public byte PollInterval { get; private set; }
        public byte Precision { get; private set; }
        public byte[] RootDelay { get; private set; }
        public byte[] RootDispersion { get; private set; }
        public byte[] ReferenceIdentifier { get; private set; }
        public Timestamp ReferenceTimestamp { get; private set; }
        public Timestamp OriginateTimestamp { get; private set; }
        public Timestamp ReceiveTimestamp { get; private set; }
        public Timestamp TransmitTimestamp { get; private set; }

        internal enum LeapIndicator
        {
            NoWarning = 0,
            LastMinut61Seconds = 1,
            LastMinut59Seconds = 2,
            NoSyncronization = 3
        }

        public SntpFrame(byte[] bytes)
        {
            if (bytes.Length < 48)
                throw new ArgumentException($"SNTP frame should be at least 48 bytes long but was {bytes.Length}");

            LI = ParseLi(bytes);
            VersionNumber = ParseVersionNumber(bytes);
            Mode = ParseMode(bytes);
            Stratum = bytes[1];
            PollInterval = bytes[2];
            Precision = bytes[3];
            RootDelay = bytes.Skip(4).Take(4).ToArray();
            RootDispersion = bytes.Skip(8).Take(4).ToArray();
            ReferenceIdentifier = bytes.Skip(12).Take(4).ToArray();
            ReferenceTimestamp = new Timestamp(bytes.Skip(16).Take(8).ToArray());
            OriginateTimestamp = new Timestamp(bytes.Skip(24).Take(8).ToArray());
            ReceiveTimestamp = new Timestamp(bytes.Skip(32).Take(8).ToArray());
            TransmitTimestamp = new Timestamp(bytes.Skip(40).Take(8).ToArray());
            // TODO extra info
        }

        private static string ParseReferenceIdentifier(byte[] bytes)
        {
            return new string(bytes.Skip(12).Take(4).Select(b => (char)b).ToArray());
        }

        private static Mode ParseMode(byte[] bytes)
        {
            //return (Mode) ((bytes[0] & 0b0000_0111) >> 5);
            return (Mode)((bytes[0] & 0x07) >> 5);
        }

        private static int ParseVersionNumber(byte[] bytes)
        {
            //return (bytes[0] & 0b0011_1000) >> 3;
            return (bytes[0] & 0x38) >> 3;
        }

        private static LeapIndicator ParseLi(byte[] bytes)
        {
            // return (LeapIndicator) (bytes[0] & 0b1100_0000 >> 6);
            return (LeapIndicator)(bytes[0] & 0xC0 >> 6);
        }

        public void RearrangeForResponse(TimeSpan delay)
        {
            LI = LeapIndicator.NoWarning;
            Stratum = 2;
            PollInterval = 4;
            ReferenceIdentifier = new byte[] { 0x80, 0x8a, 0x8d, 0xac };
            RootDelay = new byte[] { 0, 0, 0x0e, 0x66 };
            RootDispersion = new byte[] { 0, 0, 0x04, 0x24 };
            Mode = Mode.Server;
            Precision = 0xe9;
            OriginateTimestamp = TransmitTimestamp;

            var now = new Timestamp(DateTime.Now + delay);
            ReceiveTimestamp = now;
            TransmitTimestamp = now;

            ReferenceTimestamp = new Timestamp(DateTime.Now - new TimeSpan(0, 1, 0, 0));
        }

        public byte[] ToBytes()
        {
            var bytes = new byte[48];
            bytes.Initialize();

            bytes[0] = (byte)((uint)LI << 6 | (uint)VersionNumber << 3 | (uint)Mode);
            bytes[1] = Stratum;
            bytes[2] = PollInterval;
            bytes[3] = Precision;

            RootDelay.CopyTo(bytes, 4);
            RootDispersion.CopyTo(bytes, 8);
            ReferenceIdentifier.Select(c => (byte)c).ToArray().CopyTo(bytes, 12);
            ReferenceTimestamp.ToBytes().CopyTo(bytes, 16);
            OriginateTimestamp.ToBytes().CopyTo(bytes, 24);
            ReceiveTimestamp.ToBytes().CopyTo(bytes, 32);
            TransmitTimestamp.ToBytes().CopyTo(bytes, 40);

            return bytes;
        }
    }

    public class NtpManager : ISimpleServiceWorker
    {
        private UdpClient _udpServer;
        private NTPClient _udpClient;
        //private static ScheduleTimer _tickTimer;

        //Disable Windows Time Service
        //HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\services\W32Time\Start ==> set to 4
        //Automatic - 2  
        //Manual - 3  
        //Disabled - 4  
        //Automatic(Delayed Start) - 2
        private void TurnOffWindowTimeService()
        {
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\services\W32Time", "Start", 4, RegistryValueKind.DWord);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error1: " + e.Message);
                return;
            }

            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\services\W32Time\Parameters", "Type", "NoSync", RegistryValueKind.String);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error2: " + e.Message);
                return;
            }
            Console.WriteLine("Done.");
        }

        void ISimpleServiceWorker.Init()
        {
            GlobalVars.Init();
            Start();
        }

        void ISimpleServiceWorker.Run()
        {
            while (true)
            {
                PerformTimeSync(GlobalVars.NTP_Server);
                Thread.Sleep(GlobalVars.NTP_SYNC_INTERVAL_MIN);
            }
        }

        void ISimpleServiceWorker.Cleanup()
        {
            Dispose();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            PerformTimeSync(GlobalVars.NTP_Server);
        }

        public void Dispose()
        {
            try
            {
                _udpServer = null;
                _udpClient = null;
            }
            catch (Exception ex)
            {
                SimpleService.WriteLog(ex.Message + Environment.NewLine + ex.StackTrace);
                throw;
            }
        }

        public void Start()
        {
            TurnOffWindowTimeService();
            PerformTimeSync(GlobalVars.NTP_Server);
            if (GlobalVars.EnableTimeSyncService)
            {
                try
                {
                    _udpServer = new UdpClient(GlobalVars.NTP_UDPPort);
                    _udpServer.BeginReceive(OnReceive, null);
                    SimpleService.WriteLog($"NTP Service is started at Port:{GlobalVars.NTP_UDPPort}");
                }
                catch (Exception ex)
                {
                    if(ex.HResult.Equals(-2147467259)) //Open Port Error
                        SimpleService.WriteLog($"Failed To Open Port {GlobalVars.NTP_UDPPort}, Turn Off Windows Time Service If It Is Running!");
                    else
                        SimpleService.WriteLog(ex.Message + Environment.NewLine + ex.StackTrace);
                }
            
            }
        }

        private void PerformTimeSync(string TimeServer)
        {
            try
            {
                SimpleService.WriteLog($"Connecting to Time Server: {TimeServer}");
                _udpClient = new NTPClient(TimeServer);
                _udpClient.Connect(true);
                SimpleService.WriteLog($"{_udpClient.ToString()}");
            }
            catch (Exception ex)
            {
                SimpleService.WriteLog($"ERROR: {ex.Message}");
            }
        }

        private void OnReceive(IAsyncResult ar)
        {
            var remoteIpEndPoint = new IPEndPoint(IPAddress.Any, GlobalVars.LPR_UDPPort);
            var datagram = _udpServer.EndReceive(ar, ref remoteIpEndPoint);
            
            try
            {
                var delay = new TimeSpan(0, 0, 0, 1);
                var frame = new SntpFrame(datagram);

                frame.RearrangeForResponse(delay);
                var responseBytes = frame.ToBytes();

                SimpleService.WriteLog($"Receive time sync request from {remoteIpEndPoint.ToString()}.");
                _udpServer.Send(responseBytes, responseBytes.Length, remoteIpEndPoint);
                SimpleService.WriteLog($"Finished time sync to {remoteIpEndPoint.ToString()}.");
            }
            catch (Exception ex)
            {
                SimpleService.WriteLog(ex.Message);
            }
            finally
            {
                _udpServer.BeginReceive(OnReceive, null);
            }

        }


    }

    
}
