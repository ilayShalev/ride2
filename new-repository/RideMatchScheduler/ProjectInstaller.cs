// Updated ProjectInstaller.cs for better service installation

using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;
using System.IO;

namespace RideMatchScheduler
{
    [RunInstaller(true)]
    public class ProjectInstaller : Installer
    {
        private ServiceProcessInstaller serviceProcessInstaller;
        private ServiceInstaller serviceInstaller;

        public ProjectInstaller()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // Create installers
            serviceProcessInstaller = new ServiceProcessInstaller();
            serviceInstaller = new ServiceInstaller();

            // Configure service account
            // Use NetworkService for better security than LocalSystem
            serviceProcessInstaller.Account = ServiceAccount.NetworkService;
            serviceProcessInstaller.Username = null;
            serviceProcessInstaller.Password = null;

            // Configure service details
            serviceInstaller.ServiceName = "RideMatchSchedulerService";
            serviceInstaller.DisplayName = "RideMatch Scheduler Service";
            serviceInstaller.Description = "Automatically calculates optimal ride sharing routes for the next day";
            serviceInstaller.StartType = ServiceStartMode.Automatic;

            // Enable delayed auto-start for Windows 7/Server 2008 and later
            try
            {
                // This property only exists in .NET 4.5+
                var delayedAutoStartProperty = typeof(ServiceInstaller).GetProperty("DelayedAutoStart");
                if (delayedAutoStartProperty != null)
                {
                    delayedAutoStartProperty.SetValue(serviceInstaller, true);
                }
            }
            catch
            {
                // Ignore if not supported
            }

            // Add recovery actions (restart on failure)
            try
            {
                // Add first recovery action - restart after 1 minute
                serviceInstaller.ServicesDependedOn = new string[] { "RPCSS", "LanmanWorkstation" };

                // Use SC.exe to configure recovery actions post-install
                this.Committed += new InstallEventHandler(ProjectInstaller_Committed);
            }
            catch
            {
                // Ignore if not supported
            }

            // Add the installers to the installer collection
            Installers.Add(serviceProcessInstaller);
            Installers.Add(serviceInstaller);
        }

        private void ProjectInstaller_Committed(object sender, InstallEventArgs e)
        {
            try
            {
                // Set up recovery actions using SC.exe
                string serviceName = "RideMatchSchedulerService";

                // Configure recovery: first restart after 60 seconds, second restart after 2 minutes
                string scArguments = $"failure \"{serviceName}\" reset= 86400 actions= restart/60000/restart/120000";

                System.Diagnostics.Process process = new System.Diagnostics.Process();
                process.StartInfo.FileName = "sc.exe";
                process.StartInfo.Arguments = scArguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.WaitForExit();

                // Log the setup
                string logPath = Path.Combine(
                    Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "ServiceInstall.log");

                using (StreamWriter writer = File.AppendText(logPath))
                {
                    writer.WriteLine($"{DateTime.Now}: RideMatch Scheduler Service successfully installed");
                    writer.WriteLine($"Recovery actions configured: restart after 60s, restart after 120s");
                }
            }
            catch
            {
                // Ignore errors in recovery setup
            }
        }
    }
}