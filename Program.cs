namespace NtpTimeSyncService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            SimpleService.Worker = new NtpManager();
            SimpleService.Start(args);
        }
    }
}
