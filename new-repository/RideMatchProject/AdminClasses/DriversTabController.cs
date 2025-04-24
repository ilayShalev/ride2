using GMap.NET.WindowsForms;
using RideMatchProject.Models;
using RideMatchProject.Services;
using RideMatchProject.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.AdminClasses
{
    /// <summary>
    /// Controller for the Drivers tab
    /// </summary>
    public class DriversTabController : TabControllerBase
    {
        private ListView _driversListView;
        private Button _refreshButton;
        private GMapControl _mapControl;

        public DriversTabController(
            DatabaseService dbService,
            MapService mapService,
            AdminDataManager dataManager)
            : base(dbService, mapService, dataManager)
        {
        }

        public override void InitializeTab(TabPage tabPage)
        {
            CreateListView(tabPage);
            CreateActionButtons(tabPage);
            CreateMapControl(tabPage);
        }

        private void CreateListView(TabPage tabPage)
        {
            _driversListView = new ListView
            {
                Location = new Point(10, 50),
                Size = new Size(1140, 400),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            // Add columns
            _driversListView.Columns.Add("ID", 50);
            _driversListView.Columns.Add("Driver Name", 150);
            _driversListView.Columns.Add("Vehicle Capacity", 80);
            _driversListView.Columns.Add("Start Location", 300);
            _driversListView.Columns.Add("Available Tomorrow", 120);
            _driversListView.Columns.Add("User ID", 80);

            tabPage.Controls.Add(_driversListView);
        }

        private void CreateActionButtons(TabPage tabPage)
        {
            // Create Refresh Button
            _refreshButton = AdminUIFactory.CreateButton(
                "Refresh Drivers",
                new Point(10, 10),
                new Size(120, 30),
                RefreshButtonClick
            );

            // Add all buttons to the tab
            tabPage.Controls.Add(_refreshButton);
        }

        private void CreateMapControl(TabPage tabPage)
        {
            _mapControl = new GMapControl
            {
                Location = new Point(10, 460),
                Size = new Size(1140, 240),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 12,
                DragButton = MouseButtons.Left
            };

            tabPage.Controls.Add(_mapControl);
            MapService.InitializeGoogleMaps(_mapControl);
        }

        private async void RefreshButtonClick(object sender, EventArgs e)
        {
            await RefreshTabAsync();
        }



        public override async Task RefreshTabAsync()
        {
            await DataManager.LoadVehiclesAsync();
            await DisplayDriversAsync();
            DisplayVehiclesOnMap();
        }

        private async Task DisplayDriversAsync()
        {
            _driversListView.Items.Clear();

            var vehicles = DataManager.Vehicles;
            if (vehicles == null)
            {
                return;
            }

            foreach (var vehicle in vehicles)
            {
                var item = new ListViewItem(vehicle.Id.ToString());
                item.SubItems.Add(vehicle.DriverName ?? $"User {vehicle.UserId}");
                item.SubItems.Add(vehicle.Capacity.ToString());

                string location = !string.IsNullOrEmpty(vehicle.StartAddress)
                    ? vehicle.StartAddress
                    : $"({vehicle.StartLatitude:F4}, {vehicle.StartLongitude:F4})";
                item.SubItems.Add(location);

                item.SubItems.Add(vehicle.IsAvailableTomorrow ? "Yes" : "No");
                item.SubItems.Add(vehicle.UserId.ToString());

                _driversListView.Items.Add(item);
            }
        }

        private void DisplayVehiclesOnMap()
        {
            var vehicles = DataManager.Vehicles;
            var destination = DataManager.Destination;

            if (vehicles == null || _mapControl == null)
            {
                return;
            }

            _mapControl.Overlays.Clear();
            var overlay = new GMapOverlay("vehicles");

            foreach (var vehicle in vehicles)
            {
                var marker = MapOverlays.CreateVehicleMarker(vehicle);
                overlay.Markers.Add(marker);
            }

            // Add destination marker if available
            if (destination != default)
            {
                var destMarker = MapOverlays.CreateDestinationMarker(
                    destination.Latitude,
                    destination.Longitude
                );
                overlay.Markers.Add(destMarker);
            }

            _mapControl.Overlays.Add(overlay);

            // Center map on first vehicle or destination
            CenterMapOnFirstPointOfInterest(vehicles, destination);
        }

        private void CenterMapOnFirstPointOfInterest(
            List<Vehicle> vehicles,
            (int Id, string Name, double Latitude, double Longitude, string Address, string TargetTime) destination)
        {
            if (vehicles != null && vehicles.Any())
            {
                var firstVehicle = vehicles.First();
                _mapControl.Position = new GMap.NET.PointLatLng(
                    firstVehicle.StartLatitude,
                    firstVehicle.StartLongitude
                );
            }
            else if (destination != default)
            {
                _mapControl.Position = new GMap.NET.PointLatLng(
                    destination.Latitude,
                    destination.Longitude
                );
            }

            _mapControl.Zoom = 12;
        }
    }

}
