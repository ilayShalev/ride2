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
    /// Controller for the Passengers tab.
    /// Handles the display and interaction with passenger data, including rendering a list and showing their locations on a map.
    /// </summary>
    public class PassengersTabController : TabControllerBase
    {
        private ListView _passengersListView;
        private Button _refreshButton;
        private GMapControl _mapControl;

        /// <summary>
        /// Initializes a new instance of the <see cref="PassengersTabController"/> class.
        /// </summary>
        /// <param name="dbService">The database service used to interact with the data.</param>
        /// <param name="mapService">The map service used for managing the map.</param>
        /// <param name="dataManager">The data manager that handles loading data.</param>
        public PassengersTabController(
            DatabaseService dbService,
            MapService mapService,
            AdminDataManager dataManager)
            : base(dbService, mapService, dataManager)
        {
        }

        /// <summary>
        /// Initializes the Passengers tab by setting up the ListView, action buttons, and map control.
        /// </summary>
        /// <param name="tabPage">The tab page to add the controls to.</param>
        public override void InitializeTab(TabPage tabPage)
        {
            CreateListView(tabPage);
            CreateActionButtons(tabPage);
            CreateMapControl(tabPage);
        }

        /// <summary>
        /// Creates and sets up the ListView for displaying the passengers.
        /// </summary>
        /// <param name="tabPage">The tab page to add the ListView to.</param>
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

            // Add columns to the ListView
            _passengersListView.Columns.Add("ID", 50);
            _passengersListView.Columns.Add("Name", 150);
            _passengersListView.Columns.Add("Location", 300);
            _passengersListView.Columns.Add("Available Tomorrow", 120);
            _passengersListView.Columns.Add("User ID", 80);

            tabPage.Controls.Add(_passengersListView);
        }

        /// <summary>
        /// Creates and sets up the action buttons for refreshing the passengers data.
        /// </summary>
        /// <param name="tabPage">The tab page to add the buttons to.</param>
        private void CreateActionButtons(TabPage tabPage)
        {
            // Create and configure the Refresh button
            _refreshButton = AdminUIFactory.CreateButton(
                "Refresh Passengers",
                new Point(10, 10),
                new Size(120, 30),
                RefreshButtonClick
            );

            // Add the button to the tab
            tabPage.Controls.Add(_refreshButton);
        }

        /// <summary>
        /// Creates and sets up the map control to display the passengers' locations.
        /// </summary>
        /// <param name="tabPage">The tab page to add the map control to.</param>
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

        /// <summary>
        /// Handles the click event for the Refresh button, triggering the refresh process asynchronously.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        private async void RefreshButtonClick(object sender, EventArgs e)
        {
            await RefreshTabAsync();
        }

        /// <summary>
        /// Refreshes the data on the Passengers tab asynchronously by loading passengers, displaying them in the ListView, and showing them on the map.
        /// </summary>
        public override async Task RefreshTabAsync()
        {
            await DataManager.LoadPassengersAsync(); // Load the passenger data.
            await DisplayPassengersAsync(); // Display the passengers in the ListView.
            DisplayPassengersOnMap(); // Display the passengers' locations on the map.
        }

        /// <summary>
        /// Displays the passengers in the ListView.
        /// </summary>
        private async Task DisplayPassengersAsync()
        {
            _passengersListView.Items.Clear(); // Clear any existing items in the ListView.

            var passengers = DataManager.Passengers;
            if (passengers == null)
            {
                return; // If no passengers, do nothing.
            }

            // Add passengers to the ListView
            foreach (var passenger in passengers)
            {
                var item = new ListViewItem(passenger.Id.ToString());
                item.SubItems.Add(passenger.Name);

                // Location display either from the address or latitude/longitude
                string location = !string.IsNullOrEmpty(passenger.Address)
                    ? passenger.Address
                    : $"({passenger.Latitude:F4}, {passenger.Longitude:F4})";
                item.SubItems.Add(location);

                item.SubItems.Add(passenger.IsAvailableTomorrow ? "Yes" : "No");
                item.SubItems.Add(passenger.UserId.ToString());

                _passengersListView.Items.Add(item);
            }
        }

        /// <summary>
        /// Displays the passengers' locations on the map, and adds a marker for each passenger and the destination.
        /// </summary>
        private void DisplayPassengersOnMap()
        {
            var passengers = DataManager.Passengers;
            var destination = DataManager.Destination;

            if (passengers == null || _mapControl == null)
            {
                return; // If there are no passengers or the map is null, do nothing.
            }

            _mapControl.Overlays.Clear(); // Clear any existing overlays on the map.
            var overlay = new GMapOverlay("passengers");

            // Add markers for each passenger
            foreach (var passenger in passengers)
            {
                var marker = MapOverlays.CreatePassengerMarker(passenger);
                overlay.Markers.Add(marker);
            }

            // Add a marker for the destination if available
            if (destination != default)
            {
                var destMarker = MapOverlays.CreateDestinationMarker(
                    destination.Latitude,
                    destination.Longitude
                );
                overlay.Markers.Add(destMarker);
            }

            _mapControl.Overlays.Add(overlay); // Add the overlay with all markers to the map.

            // Center the map on the first passenger or destination
            CenterMapOnFirstPointOfInterest(passengers, destination);
        }

        /// <summary>
        /// Centers the map on either the first passenger or the destination.
        /// </summary>
        /// <param name="passengers">The list of passengers.</param>
        /// <param name="destination">The destination object.</param>
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

            _mapControl.Zoom = 12; // Set the zoom level of the map.
        }
    }
}
