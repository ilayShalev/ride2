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
    /// Main administrative interface for the RideMatch application.
    /// Responsible for initializing UI, loading destination data, and controlling tabs.
    /// </summary>
    public partial class AdminForm : Form
    {
        // Core backend services
        private readonly DatabaseService _dbService;
        private readonly MapService _mapService;

        // Logic and algorithm components
        private RoutingService _routingService;
        private RideSharingGenetic _algorithmService;

        // UI components
        private TabControl _tabControl;
        private TabFactory _tabFactory;

        // Current solution state
        private Solution _currentSolution;

        // Destination details for the day
        private double _destinationLat;
        private double _destinationLng;
        private string _destinationName;
        private string _destinationAddress;
        private string _destinationTargetTime;

        /// <summary>
        /// Constructs the AdminForm and initializes services.
        /// </summary>
        public AdminForm(DatabaseService dbService, MapService mapService)
        {
            _dbService = dbService;
            _mapService = mapService;

            InitializeComponent();
            InitializeAdminInterface();

            // Hook Load event
            this.Load += AdminForm_Load;
        }

        /// <summary>
        /// On form load, begin loading destination and data in background.
        /// </summary>
        private void AdminForm_Load(object sender, EventArgs e)
        {
            // Run loading logic asynchronously to keep UI responsive
            Task.Run(LoadDestinationAndDataAsync);
        }

        /// <summary>
        /// Sets up the form's UI structure and tabs.
        /// </summary>
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

            // Initialize tab factory to handle content creation
            _tabFactory = new TabFactory(_dbService, _mapService);

            // Create and register all system tabs
            CreateAndAddAllTabs();
        }

        /// <summary>
        /// Creates and adds all functional tabs to the interface.
        /// </summary>
        private void CreateAndAddAllTabs()
        {
            var usersTabPage = _tabFactory.CreateUsersTab();
            var driversTabPage = _tabFactory.CreateDriversTab();
            var passengersTabPage = _tabFactory.CreatePassengersTab();
            var routesTabPage = _tabFactory.CreateRoutesTab();
            var destinationTabPage = _tabFactory.CreateDestinationTab();
            var schedulingTabPage = _tabFactory.CreateSchedulingTab();

            _tabControl.TabPages.AddRange(new TabPage[]
            {
                usersTabPage,
                driversTabPage,
                passengersTabPage,
                routesTabPage,
                destinationTabPage,
                schedulingTabPage
            });

            // Register handler to refresh tab when changed
            _tabControl.SelectedIndexChanged += TabSelectionChanged;
        }

        /// <summary>
        /// Loads the current destination and initializes services/tabs accordingly.
        /// </summary>
        private async Task LoadDestinationAndDataAsync()
        {
            try
            {
                // Retrieve current destination information
                var dest = await _dbService.GetDestinationAsync();
                _destinationLat = dest.Latitude;
                _destinationLng = dest.Longitude;
                _destinationName = dest.Name;
                _destinationAddress = dest.Address;
                _destinationTargetTime = dest.TargetTime;

                // Initialize routing logic with retrieved destination
                _routingService = new RoutingService(
                    _mapService,
                    _destinationLat,
                    _destinationLng,
                    _destinationTargetTime
                );

                // Trigger async loading for each tab's data
                await _tabFactory.LoadAllDataAsync();
            }
            catch (Exception ex)
            {
                // Ensure exception message is shown on the UI thread
                if (this.IsHandleCreated)
                {
                    this.Invoke((Action)(() => {
                        MessageDisplayer.ShowError($"Error loading destination: {ex.Message}");
                    }));
                }
            }
        }

        /// <summary>
        /// Refreshes the selected tab when user switches between them.
        /// </summary>
        private void TabSelectionChanged(object sender, EventArgs e)
        {
            var selectedTab = _tabControl.SelectedTab;
            if (selectedTab == null) return;

            // Use utility to safely run async refresh on the selected tab
            TaskManager.ExecuteAsync(() => _tabFactory.RefreshTabAsync(selectedTab));
        }
    }

    /// <summary>
    /// Utility class for async task execution with error display
    /// </summary>
    public static class TaskManager
    {
        /// <summary>
        /// Executes an async task with built-in error handling
        /// </summary>
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
