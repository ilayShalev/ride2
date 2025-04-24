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
    /// Controller for the Passengers tab
    /// </summary>
    public class PassengersTabController : TabControllerBase
    {
        private ListView _passengersListView;
        private Button _refreshButton;
        private GMapControl _mapControl;

        public PassengersTabController(
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
            _passengersListView = new ListView
            {
                Location = new Point(10, 50),
                Size = new Size(1140, 400),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            // Add columns
            _passengersListView.Columns.Add("ID", 50);
            _passengersListView.Columns.Add("Name", 150);
            _passengersListView.Columns.Add("Location", 300);
            _passengersListView.Columns.Add("Available Tomorrow", 120);
            _passengersListView.Columns.Add("User ID", 80);

            tabPage.Controls.Add(_passengersListView);
        }

        private void CreateActionButtons(TabPage tabPage)
        {
            // Create Refresh Button
            _refreshButton = AdminUIFactory.CreateButton(
                "Refresh Passengers",
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
            await DataManager.LoadPassengersAsync();
            await DisplayPassengersAsync();
            DisplayPassengersOnMap();
        }

        private async Task DisplayPassengersAsync()
        {
            _passengersListView.Items.Clear();

            var passengers = DataManager.Passengers;
            if (passengers == null)
            {
                return;
            }

            foreach (var passenger in passengers)
            {
                var item = new ListViewItem(passenger.Id.ToString());
                item.SubItems.Add(passenger.Name);

                string location = !string.IsNullOrEmpty(passenger.Address)
                    ? passenger.Address
                    : $"({passenger.Latitude:F4}, {passenger.Longitude:F4})";
                item.SubItems.Add(location);

                item.SubItems.Add(passenger.IsAvailableTomorrow ? "Yes" : "No");
                item.SubItems.Add(passenger.UserId.ToString());

                _passengersListView.Items.Add(item);
            }
        }

        private void DisplayPassengersOnMap()
        {
            var passengers = DataManager.Passengers;
            var destination = DataManager.Destination;

            if (passengers == null || _mapControl == null)
            {
                return;
            }

            _mapControl.Overlays.Clear();
            var overlay = new GMapOverlay("passengers");

            foreach (var passenger in passengers)
            {
                var marker = MapOverlays.CreatePassengerMarker(passenger);
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

            // Center map on first passenger or destination
            CenterMapOnFirstPointOfInterest(passengers, destination);
        }

        private void CenterMapOnFirstPointOfInterest(
            List<Passenger> passengers,
            (int Id, string Name, double Latitude, double Longitude, string Address, string TargetTime) destination)
        {
            if (passengers != null && passengers.Any())
            {
                var firstPassenger = passengers.First();
                _mapControl.Position = new GMap.NET.PointLatLng(
                    firstPassenger.Latitude,
                    firstPassenger.Longitude
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
