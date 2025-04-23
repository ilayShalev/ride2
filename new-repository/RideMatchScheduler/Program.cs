using System;
using System.ServiceProcess;
using System.Windows.Forms;
using System.Reflection;

namespace RideMatchScheduler
{
    /// <summary>
    /// Application entry point that handles both service and console modes
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            if (ShouldRunInConsoleMode(args))
            {
                RunAsConsoleApplication(args);
            }
            else
            {
                RunAsWindowsService();
            }
        }

        /// <summary>
        /// Determines if the application should run in console mode
        /// </summary>
        private static bool ShouldRunInConsoleMode(string[] args)
        {
            return args.Length > 0 && args[0].ToLower() == "-console";
        }

        /// <summary>
        /// Runs the application as a console application for testing
        /// </summary>
        private static void RunAsConsoleApplication(string[] args)
        {
            Console.WriteLine("Starting RideMatch Scheduler in console mode...");
            var service = new RideMatchCoordinatorService();

            // Call OnStart manually using reflection
            InvokeServiceMethod(service, "OnStart", new object[] { args });

            Console.WriteLine("Service started. Press any key to stop...");
            Console.ReadKey();

            // Call OnStop manually using reflection
            InvokeServiceMethod(service, "OnStop", null);

            Console.WriteLine("Service stopped. Press any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Invokes a method on the service instance using reflection
        /// </summary>
        private static void InvokeServiceMethod(ServiceBase service, string methodName, object[] parameters)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo method = typeof(ServiceBase).GetMethod(methodName, flags);
            method?.Invoke(service, parameters);
        }

        /// <summary>
        /// Runs the application as a Windows Service
        /// </summary>
        private static void RunAsWindowsService()
        {
            ServiceBase[] ServicesToRun = new ServiceBase[]
            {
                new RideMatchCoordinatorService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}