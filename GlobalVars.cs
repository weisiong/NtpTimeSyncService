using NtpTimeSyncService.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NtpTimeSyncService
{
    public static class GlobalVars
    {
        // System Variables
        public static string SOFTWARE_NAME = "Ingensys NTP Time Sync Service";
        public static string SOFTWARE_VERSION = "20180622_1030";
        public static string APP_PATH = System.IO.Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);

        public static string NTP_Server = "sg.pool.ntp.org";
        public static int NTP_UDPPort = 123;
        public static bool EnableTimeSyncService = true;
        public static bool EnableNTP { get; internal set; } = true;
        public static int LPR_UDPPort = 2002;
        public static int NTP_SYNC_INTERVAL_MIN = 1;

        public static void Init()
        {
            ReadConfigFile();
        }

        private static void ReadConfigFile()
        {
            try
            {
                IniFile ini = new IniFile(GlobalVars.APP_PATH + "\\Configuration.ini");                

                GlobalVars.NTP_Server = ini.read("NTP", "NTP_Server", "sg.pool.ntp.org");
                GlobalVars.NTP_UDPPort = ini.read("NTP", "NTP_UDPPort", 123);
                GlobalVars.NTP_SYNC_INTERVAL_MIN = ini.read("NTP", "NTP_PoolInterval_Min", NTP_SYNC_INTERVAL_MIN) * 60000;
                GlobalVars.EnableTimeSyncService = ini.read("NTP", "EnableTimeSyncService", true);

            }
            catch (Exception e)
            {
                Console.Write($"Read File Error {GlobalVars.APP_PATH}\\Configuration.ini \r\n{e.Message}\r\n");
            }
        }

    }
}
