using System;
using System.ServiceProcess;
using System.Windows.Forms;

namespace RideMatchScheduler
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            // Check if we should run as a console app for testing
            if (args.Length > 0 && args[0].ToLower() == "-console")
            {
                // Run as console application for testing
                Console.WriteLine("Starting RideMatch Scheduler in console mode...");

                var service = new RideMatchSchedulerService();
                // Call OnStart manually
                typeof(ServiceBase).GetMethod("OnStart", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(service, new object[] { args });

                Console.WriteLine("Service started. Press any key to stop...");
                Console.ReadKey();

                // Call OnStop manually
                typeof(ServiceBase).GetMethod("OnStop", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(service, null);

                Console.WriteLine("Service stopped. Press any key to exit...");
                Console.ReadKey();
            }
            else
            {
                // Run as Windows Service
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                    new RideMatchSchedulerService()
                };
                ServiceBase.Run(ServicesToRun);
            }
        }
    }
}