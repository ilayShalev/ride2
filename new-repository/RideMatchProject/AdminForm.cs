using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SQLite;
using RideMatchProject.Models;
using RideMatchProject.Services;
using RideMatchProject.UI;
using RideMatchProject.AdminClasses;
using GMap.NET.WindowsForms;

namespace RideMatchProject
{
    /// <summary>
    /// Main administrative interface for the RideMatch application
    /// </summary>
    public partial class AdminForm : Form
    {
        private readonly DatabaseService _dbService;
        private readonly MapService _mapService;
        private RoutingService _routingService;
        private RideSharingGenetic _algorithmService;

        // Core UI components
        private TabControl _tabControl;
        private TabFactory _tabFactory;

        // Current solution being viewed
        private Solution _currentSolution;

        // Destination information
        private double _destinationLat;
        private double _destinationLng;
        private string _destinationName;
        private string _destinationAddress;
        private string _destinationTargetTime;

        public AdminForm(DatabaseService dbService, MapService mapService)
        {
            _dbService = dbService;
            _mapService = mapService;

            InitializeComponent();
            InitializeAdminInterface();

            // Add the Load event handler
            this.Load += AdminForm_Load;
        }

        private void AdminForm_Load(object sender, EventArgs e)
        {
            // Call the async method without awaiting
            Task.Run(LoadDestinationAndDataAsync);
        }

        private void InitializeAdminInterface()
        {
            Text = "RideMatch - Administrator Interface";
            Size = new Size(1200, 800);
            StartPosition = FormStartPosition.CenterScreen;

            _tabControl = new TabControl
            {
                Location = new Point(10, 10),
                Size = new Size(1170, 740),
                Dock = DockStyle.Fill
            };
            Controls.Add(_tabControl);

            // Initialize factory and tab controllers
            _tabFactory = new TabFactory(_dbService, _mapService);

            // Create and add all tabs
            CreateAndAddAllTabs();
        }

        private void CreateAndAddAllTabs()
        {
            // Create all tab pages using the factory
            var usersTabPage = _tabFactory.CreateUsersTab();
            var driversTabPage = _tabFactory.CreateDriversTab();
            var passengersTabPage = _tabFactory.CreatePassengersTab();
            var routesTabPage = _tabFactory.CreateRoutesTab();
            var destinationTabPage = _tabFactory.CreateDestinationTab();
            var schedulingTabPage = _tabFactory.CreateSchedulingTab();

            // Add all tabs to the control
            _tabControl.TabPages.AddRange(new TabPage[] {
                usersTabPage,
                driversTabPage,
                passengersTabPage,
                routesTabPage,
                destinationTabPage,
                schedulingTabPage
            });

            // Configure tab selection behavior
            _tabControl.SelectedIndexChanged += TabSelectionChanged;
        }

        private async Task LoadDestinationAndDataAsync()
        {
            try
            {
                var dest = await _dbService.GetDestinationAsync();
                _destinationLat = dest.Latitude;
                _destinationLng = dest.Longitude;
                _destinationName = dest.Name;
                _destinationAddress = dest.Address;
                _destinationTargetTime = dest.TargetTime;

                // Initialize routing service with destination coordinates and target time
                _routingService = new RoutingService(
                    _mapService,
                    _destinationLat,
                    _destinationLng,
                    _destinationTargetTime // Pass the target time
                );

                // Load initial data
                await _tabFactory.LoadAllDataAsync();
            }
            catch (Exception ex)
            {
                if (this.IsHandleCreated)
                {
                    this.Invoke((Action)(() => {
                        MessageDisplayer.ShowError($"Error loading destination: {ex.Message}");
                    }));
                }
            }
        }
        private void TabSelectionChanged(object sender, EventArgs e)
        {
            var selectedTab = _tabControl.SelectedTab;

            if (selectedTab == null)
            {
                return;
            }

            // Delegate to the tab factory to refresh the selected tab
            TaskManager.ExecuteAsync(() => _tabFactory.RefreshTabAsync(selectedTab));
        }
    }

  
  

  


 
 

  
 

 

    /// <summary>
    /// Utility class for async task execution
    /// </summary>
    public static class TaskManager
    {
        public static async void ExecuteAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Operation failed: {ex.Message}",
                    "Error"
                );
            }
        }
    }
}
