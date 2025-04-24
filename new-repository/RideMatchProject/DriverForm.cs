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
        private DriverDataManager dataManager;
        private DriverUIManager uiManager;
        private DriverMapManager mapManager;
        private DriverLocationManager locationManager;

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
            dataManager = new DriverDataManager(dbService, userId, username);
            mapManager = new DriverMapManager(mapService, dbService);
            locationManager = new DriverLocationManager(mapService, dataManager);
            uiManager = new DriverUIManager(this, dataManager, mapManager, locationManager,username);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            try
            {
                uiManager.InitializeUI();
                mapManager.InitializeMap(uiManager.MapControl, 32.0741, 34.7922);

                Task.Run(async () => await LoadDataAndRefreshUI())
                    .ContinueWith(HandleLoadingError, TaskContinuationOptions.OnlyOnFaulted);
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

        private void HandleLoadingError(Task task)
        {
            if (task.Exception == null) return;

            this.Invoke(new Action(() => {
                ShowErrorMessage("Error loading driver data",
                    task.Exception.InnerException?.Message ?? task.Exception.Message);
            }));
        }

        private async Task LoadDataAndRefreshUI()
        {
            await dataManager.LoadDriverDataAsync();

            this.Invoke(new Action(() => {
                uiManager.RefreshUI();
                mapManager.DisplayRouteOnMap(dataManager.Vehicle, dataManager.AssignedPassengers);
            }));
        }

        private void ShowErrorMessage(string title, string message)
        {
            MessageBox.Show($"{title}: {message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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