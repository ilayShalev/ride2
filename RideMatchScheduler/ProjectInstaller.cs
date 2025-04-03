using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

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
            serviceProcessInstaller = new ServiceProcessInstaller();
            serviceInstaller = new ServiceInstaller();

            // Service account information
            serviceProcessInstaller.Account = ServiceAccount.LocalSystem;

            // Service information
            serviceInstaller.ServiceName = "RideMatchSchedulerService";
            serviceInstaller.DisplayName = "RideMatch Scheduler Service";
            serviceInstaller.Description = "Automatically calculates optimal ride sharing routes for the next day";
            serviceInstaller.StartType = ServiceStartMode.Automatic;

            // Add installers to collection
            Installers.Add(serviceProcessInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}