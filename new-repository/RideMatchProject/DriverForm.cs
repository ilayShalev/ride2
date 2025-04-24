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
    /// Main form for driver interface
    /// </summary>
    public partial class DriverForm : Form
    {
        private DriverDataManager _dataManager;
        private DriverUIManager _uiManager;
        private DriverMapManager _mapManager;
        private DriverLocationManager _locationManager;

        public DriverForm(DatabaseService dbService, MapService mapService, int userId, string username)
        {
            ValidateArguments(dbService, mapService, username);
            InitializeComponent();
            InitializeManagers(dbService, mapService, userId, username);
        }

        private void ValidateArguments(DatabaseService dbService, MapService mapService, string username)
        {
            if (dbService == null) throw new ArgumentNullException(nameof(dbService));
            if (mapService == null) throw new ArgumentNullException(nameof(mapService));
            if (string.IsNullOrEmpty(username)) throw new ArgumentNullException(nameof(username));
        }

        private void InitializeManagers(DatabaseService dbService, MapService mapService, int userId, string username)
        {
            _dataManager = new DriverDataManager(dbService, userId, username);
            _mapManager = new DriverMapManager(mapService, dbService);
            _locationManager = new DriverLocationManager(mapService, _dataManager);
            _uiManager = new DriverUIManager(this, _dataManager, _mapManager, _locationManager, username);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            try
            {
                _uiManager.InitializeUI();
                _mapManager.InitializeMap(_uiManager.MapControl, 32.0741, 34.7922);

                // Use SafeTaskRun to properly handle errors in async operations
                ThreadUtils.SafeTaskRun(
                    async () => await LoadDataAndRefreshUI(),
                    ex => HandleLoadingError(ex)
                );
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error initializing driver form", ex.Message);
            }
        }

        // This method is required by the designer
        private void DriverForm_Load(object sender, EventArgs e)
        {
            // This is intentionally left empty as we use OnLoad instead
        }

        private void HandleLoadingError(Exception ex)
        {
            ThreadUtils.ShowErrorMessage(this,
                $"Error loading driver data: {(ex.InnerException?.Message ?? ex.Message)}",
                "Error");
        }

        private async Task LoadDataAndRefreshUI()
        {
            await _dataManager.LoadDriverDataAsync();

            // Use ThreadUtils to ensure UI updates happen on the UI thread
            ThreadUtils.ExecuteOnUIThread(this, () => {
                _uiManager.RefreshUI();
                _mapManager.DisplayRouteOnMap(_dataManager.Vehicle, _dataManager.AssignedPassengers);
            });
        }

        private void ShowErrorMessage(string title, string message)
        {
            ThreadUtils.ShowErrorMessage(this, message, title);
        }
    }

    /// <summary>
    /// Models for data transfer
    /// </summary>
    public class RouteData
    {
        public Vehicle Vehicle { get; set; }
        public List<Passenger> Passengers { get; set; }
        public DateTime? PickupTime { get; set; }
    }

    /// <summary>
    /// Model for destination location
    /// </summary>
    public class Destination
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Address { get; set; }
        public string TargetTime { get; set; }
    }
}