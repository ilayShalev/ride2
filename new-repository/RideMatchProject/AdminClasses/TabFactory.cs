using RideMatchProject.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.AdminClasses
{
    /// <summary>
    /// Factory for creating and managing tab pages
    /// </summary>
    public class TabFactory
    {
        private readonly DatabaseService _dbService;
        private readonly MapService _mapService;

        // Tab controllers
        private readonly UsersTabController _usersController;
        private readonly DriversTabController _driversController;
        private readonly PassengersTabController _passengersController;
        private readonly RoutesTabController _routesController;
        private readonly DestinationTabController _destinationController;
        private readonly SchedulingTabController _schedulingController;

        // Data managers
        private readonly AdminDataManager _dataManager;

        public TabFactory(DatabaseService dbService, MapService mapService)
        {
            _dbService = dbService;
            _mapService = mapService;

            // Initialize data manager
            _dataManager = new AdminDataManager(dbService);

            // Initialize all tab controllers
            _usersController = new UsersTabController(dbService, _dataManager);
            _driversController = new DriversTabController(dbService, mapService, _dataManager);
            _passengersController = new PassengersTabController(dbService, mapService, _dataManager);
            _routesController = new RoutesTabController(dbService, mapService, _dataManager);
            _destinationController = new DestinationTabController(dbService, mapService);
            _schedulingController = new SchedulingTabController(dbService, mapService, _dataManager);
        }

        public TabPage CreateUsersTab()
        {
            var usersTab = new TabPage("Users");
            _usersController.InitializeTab(usersTab);
            return usersTab;
        }

        public TabPage CreateDriversTab()
        {
            var driversTab = new TabPage("Drivers");
            _driversController.InitializeTab(driversTab);
            return driversTab;
        }

        public TabPage CreatePassengersTab()
        {
            var passengersTab = new TabPage("Passengers");
            _passengersController.InitializeTab(passengersTab);
            return passengersTab;
        }

        public TabPage CreateRoutesTab()
        {
            var routesTab = new TabPage("Routes");
            _routesController.InitializeTab(routesTab);
            return routesTab;
        }

        public TabPage CreateDestinationTab()
        {
            var destinationTab = new TabPage("Destination");
            _destinationController.InitializeTab(destinationTab);
            return destinationTab;
        }

        public TabPage CreateSchedulingTab()
        {
            var schedulingTab = new TabPage("Scheduling");
            _schedulingController.InitializeTab(schedulingTab);
            return schedulingTab;
        }

        public async Task LoadAllDataAsync()
        {
            await _dataManager.LoadAllDataAsync();
        }

        public async Task RefreshTabAsync(TabPage selectedTab)
        {
            string tabName = selectedTab.Text;

            if (tabName == "Users")
            {
                await _usersController.RefreshTabAsync();
            }
            else if (tabName == "Drivers")
            {
                await _driversController.RefreshTabAsync();
            }
            else if (tabName == "Passengers")
            {
                await _passengersController.RefreshTabAsync();
            }
            else if (tabName == "Routes")
            {
                await _routesController.RefreshTabAsync();
            }
            else if (tabName == "Destination")
            {
                await _destinationController.RefreshTabAsync();
            }
            else if (tabName == "Scheduling")
            {
                await _schedulingController.RefreshTabAsync();
            }
        }
    }
}