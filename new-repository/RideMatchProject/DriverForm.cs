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
    /// Main form for driver interface with proper thread handling
    /// </summary>
    public partial class DriverForm : Form
    {
        private DriverDataManager _dataManager;
        private DriverUIManager _uiManager;
        private DriverMapManager _mapManager;
        private DriverLocationManager _locationManager;
        private readonly int _userId;
        private readonly string _username;

        public DriverForm(DatabaseService dbService, MapService mapService, int userId, string username)
        {
            ValidateArguments(dbService, mapService, username);

            _userId = userId;
            _username = username;

            InitializeComponent();
            InitializeManagers(dbService, mapService);
        }

        private void ValidateArguments(DatabaseService dbService, MapService mapService, string username)
        {
            if (dbService == null) throw new ArgumentNullException(nameof(dbService));
            if (mapService == null) throw new ArgumentNullException(nameof(mapService));
            if (string.IsNullOrEmpty(username)) throw new ArgumentNullException(nameof(username));
        }

        private void InitializeManagers(DatabaseService dbService, MapService mapService)
        {
            _dataManager = new DriverDataManager(dbService, _userId, _username);
            _mapManager = new DriverMapManager(mapService, dbService);
            _locationManager = new DriverLocationManager(mapService, _dataManager);
            _uiManager = new DriverUIManager(this, _dataManager, _mapManager, _locationManager, _username);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            try
            {
                // Initialize UI components on the UI thread
                _uiManager.InitializeUI();

                // Initialize map (this is done on the UI thread by the manager)
                _mapManager.InitializeMap(_uiManager.MapControl, 32.0741, 34.7922);

                // Use our thread-safe task runner to load data
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

        // Required by the designer
        private void DriverForm_Load(object sender, EventArgs e)
        {
            // Implementation is in OnLoad override
        }

        private void HandleLoadingError(Exception ex)
        {
            ThreadUtils.ShowErrorMessage(this,
                $"Error loading driver data: {(ex.InnerException?.Message ?? ex.Message)}",
                "Loading Error");
        }

        private async Task LoadDataAndRefreshUI()
        {
            await _dataManager.LoadDriverDataAsync();

            // UI updates are handled on the UI thread within these methods
            _uiManager.RefreshUI();
            await _mapManager.DisplayRouteOnMapAsync(_dataManager.Vehicle, _dataManager.AssignedPassengers);
        }

        // Ensure cleanup on form close
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            // Clean up any resources or event handlers if needed
        }
    }
}