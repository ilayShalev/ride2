using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using RideMatchProject.Models;
using RideMatchProject.Services;
using RideMatchProject.UI;
using RideMatchProject.Utilities;
using RideMatchProject.DriverClasses;

namespace RideMatchProject
{
    /// <summary>
    /// Main form for driver interface. Handles UI setup, map visualization, and background data loading.
    /// </summary>
    public partial class DriverForm : Form
    {
        // Core driver-related services
        private DriverDataManager _dataManager;
        private DriverUIManager _uiManager;
        private DriverMapManager _mapManager;
        private DriverLocationManager _locationManager;

        // Readonly user information
        private readonly int _userId;
        private readonly string _username;

        /// <summary>
        /// Initializes the driver form and injects shared services
        /// </summary>
        /// <param name="dbService">Database service for data access</param>
        /// <param name="mapService">Map service for routing/coordinates</param>
        /// <param name="userId">Logged-in driver's ID</param>
        /// <param name="username">Logged-in driver's name</param>
        public DriverForm(DatabaseService dbService, MapService mapService, int userId, string username)
        {
            ValidateArguments(dbService, mapService, username);

            _userId = userId;
            _username = username;

            InitializeComponent(); // Initialize WinForms controls

            InitializeManagers(dbService, mapService); // Set up core logic managers
        }

        /// <summary>
        /// Validates constructor arguments before using them
        /// </summary>
        private void ValidateArguments(DatabaseService dbService, MapService mapService, string username)
        {
            if (dbService == null) throw new ArgumentNullException(nameof(dbService));
            if (mapService == null) throw new ArgumentNullException(nameof(mapService));
            if (string.IsNullOrEmpty(username)) throw new ArgumentNullException(nameof(username));
        }

        /// <summary>
        /// Initializes the core logic managers that control data, map, UI and location tracking
        /// </summary>
        private void InitializeManagers(DatabaseService dbService, MapService mapService)
        {
            _dataManager = new DriverDataManager(dbService, _userId, _username);
            _mapManager = new DriverMapManager(mapService, dbService);
            _locationManager = new DriverLocationManager(mapService, _dataManager);
            _uiManager = new DriverUIManager(this, _dataManager, _mapManager, _locationManager, _username);
        }

        /// <summary>
        /// Called when the form is loaded. Initializes UI and loads data asynchronously.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            try
            {
                _uiManager.InitializeUI(); // Sets up UI controls and labels
                _mapManager.InitializeMap(_uiManager.MapControl, 32.0741, 34.7922); // Center the map on a default location (e.g., Tel Aviv)

                // Load data safely on a background thread and update UI after
                ThreadUtils.SafeTaskRun(
                    async () => await LoadDataAndRefreshUI(),
                    ex => HandleLoadingError(ex)
                );
            }
            catch (Exception ex)
            {
                ThreadUtils.ShowErrorMessage(this,
                    $"Error initializing driver form: {ex.Message}",
                    "Initialization Error");
            }
        }

        /// <summary>
        /// Placeholder for designer-required form load event.
        /// All initialization is done in OnLoad.
        /// </summary>
        private void DriverForm_Load(object sender, EventArgs e)
        {
            // Do nothing. Designer will call this automatically.
        }

        /// <summary>
        /// Handles errors that occur during async loading of driver data
        /// </summary>
        private void HandleLoadingError(Exception ex)
        {
            ThreadUtils.ShowErrorMessage(this,
                $"Error loading driver data: {(ex.InnerException?.Message ?? ex.Message)}",
                "Loading Error");
        }

        /// <summary>
        /// Loads driver's assigned passengers, vehicle data and updates the map
        /// </summary>
        private async Task LoadDataAndRefreshUI()
        {
            await _dataManager.LoadDriverDataAsync(); // Load from DB or cache

            _uiManager.RefreshUI(); // Update UI elements (list of passengers, status, etc.)

            // Draw the route on the map based on assigned passengers
            await _mapManager.DisplayRouteOnMapAsync(_dataManager.Vehicle, _dataManager.AssignedPassengers);
        }

        /// <summary>
        /// Called when the form is closed. Releases any resources if necessary.
        /// </summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            // Add disposal or cleanup here if needed in the future
        }
    }
}
