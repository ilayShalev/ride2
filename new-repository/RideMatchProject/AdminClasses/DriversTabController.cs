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
    /// Controller for the Drivers tab in the admin panel.
    /// This class is responsible for managing the display and interactions of the Drivers tab.
    /// </summary>
    public class DriversTabController : TabControllerBase
    {
        private ListView _driversListView;  // ListView for displaying drivers' information.
        private Button _refreshButton;      // Button for refreshing the drivers' data.
        private GMapControl _mapControl;    // GMapControl to display the map with drivers' locations.

        /// <summary>
        /// Initializes a new instance of the <see cref="DriversTabController"/> class.
        /// </summary>
        /// <param name="dbService">The service for interacting with the database.</param>
        /// <param name="mapService">The service for interacting with the map functionalities.</param>
        /// <param name="dataManager">The data manager responsible for loading and managing data.</param>
        public DriversTabController(
            DatabaseService dbService,
            MapService mapService,
            AdminDataManager dataManager)
            : base(dbService, mapService, dataManager)
        {
        }

        /// <summary>
        /// Initializes the tab by creating necessary controls and UI elements.
        /// </summary>
        /// <param name="tabPage">The TabPage where the UI elements will be added.</param>
        public override void InitializeTab(TabPage tabPage)
        {
            CreateListView(tabPage);   // Initializes the ListView to display the driver data.
            CreateActionButtons(tabPage);  // Initializes the action buttons, such as the refresh button.
            CreateMapControl(tabPage);  // Initializes the map control to display the drivers' locations.
        }

        /// <summary>
        /// Creates and initializes the ListView that displays the drivers' information.
        /// </summary>
        /// <param name="tabPage">The TabPage where the ListView will be added.</param>
        private void CreateListView(TabPage tabPage)
        {
            _driversListView = new ListView
            {
                Location = new Point(10, 50),  // Sets the location on the TabPage.
                Size = new Size(1140, 400),     // Sets the size of the ListView.
                View = View.Details,           // Displays detailed view.
                FullRowSelect = true,          // Enables selection of full rows.
                GridLines = true               // Enables grid lines in the ListView.
            };

            // Adding columns to the ListView to display specific driver information.
            _driversListView.Columns.Add("ID", 50);                      // Driver ID.
            _driversListView.Columns.Add("Driver Name", 150);             // Driver's name.
            _driversListView.Columns.Add("Vehicle Capacity", 80);         // Vehicle's capacity.
            _driversListView.Columns.Add("Start Location", 300);          // Driver's start location.
            _driversListView.Columns.Add("Available Tomorrow", 120);     // Availability for the next day.
            _driversListView.Columns.Add("User ID", 80);                  // User ID associated with the driver.

            // Adding the ListView to the TabPage.
            tabPage.Controls.Add(_driversListView);
        }

        /// <summary>
        /// Creates and initializes the action buttons (e.g., Refresh button) for the Drivers tab.
        /// </summary>
        /// <param name="tabPage">The TabPage where the buttons will be added.</param>
        private void CreateActionButtons(TabPage tabPage)
        {
            // Creates the "Refresh Drivers" button.
            _refreshButton = AdminUIFactory.CreateButton(
                "Refresh Drivers",               // Button text.
                new Point(10, 10),               // Button location.
                new Size(120, 30),               // Button size.
                RefreshButtonClick              // Event handler for button click.
            );

            // Adding the button to the TabPage.
            tabPage.Controls.Add(_refreshButton);
        }

        /// <summary>
        /// Creates and initializes the GMapControl to display the map of driver locations.
        /// </summary>
        /// <param name="tabPage">The TabPage where the map control will be added.</param>
        private void CreateMapControl(TabPage tabPage)
        {
            _mapControl = new GMapControl
            {
                Location = new Point(10, 460),   // Sets the location of the map on the TabPage.
                Size = new Size(1140, 240),       // Sets the size of the map control.
                MinZoom = 2,                      // Minimum zoom level.
                MaxZoom = 18,                     // Maximum zoom level.
                Zoom = 12,                        // Default zoom level.
                DragButton = MouseButtons.Left   // Sets the drag button for the map.
            };

            // Adding the map control to the TabPage.
            tabPage.Controls.Add(_mapControl);

            // Initializes the map service (e.g., using Google Maps API).
            MapService.InitializeGoogleMaps(_mapControl);
        }

        /// <summary>
        /// Event handler for the "Refresh" button click event.
        /// Refreshes the tab asynchronously by reloading the data and updating the UI.
        /// </summary>
        /// <param name="sender">The object that triggered the event (the Refresh button).</param>
        /// <param name="e">Event arguments.</param>
        private async void RefreshButtonClick(object sender, EventArgs e)
        {
            await RefreshTabAsync();  // Calls the asynchronous method to refresh the data.
        }

        /// <summary>
        /// Asynchronously refreshes the data for the Drivers tab, including vehicles and map updates.
        /// </summary>
        public override async Task RefreshTabAsync()
        {
            await DataManager.LoadVehiclesAsync();  // Loads the latest vehicle data.
            await DisplayDriversAsync();            // Updates the ListView with the latest driver information.
            DisplayVehiclesOnMap();                 // Updates the map with the latest vehicle locations.
        }

        /// <summary>
        /// Displays the list of drivers in the ListView.
        /// </summary>
        private async Task DisplayDriversAsync()
        {
            _driversListView.Items.Clear();  // Clears the existing list of drivers.

            var vehicles = DataManager.Vehicles;  // Retrieves the list of vehicles (drivers).
            if (vehicles == null)
            {
                return;  // If no vehicles are available, exit the method.
            }

            // Iterates through each vehicle (driver) and creates a ListViewItem for it.
            foreach (var vehicle in vehicles)
            {
                var item = new ListViewItem(vehicle.Id.ToString());  // Adds the vehicle ID.

                // Adds other details for the driver.
                item.SubItems.Add(vehicle.DriverName ?? $"User {vehicle.UserId}");  // Driver name (or user ID if no name).
                item.SubItems.Add(vehicle.Capacity.ToString());                      // Vehicle capacity.
                item.SubItems.Add(string.IsNullOrEmpty(vehicle.StartAddress)
                    ? $"({vehicle.StartLatitude:F4}, {vehicle.StartLongitude:F4})"  // Location as coordinates if no address.
                    : vehicle.StartAddress);  // Otherwise, display the start address.
                item.SubItems.Add(vehicle.IsAvailableTomorrow ? "Yes" : "No");      // Availability tomorrow.
                item.SubItems.Add(vehicle.UserId.ToString());                        // User ID.

                _driversListView.Items.Add(item);  // Adds the item to the ListView.
            }
        }

        /// <summary>
        /// Displays the vehicles and their locations on the map.
        /// </summary>
        private void DisplayVehiclesOnMap()
        {
            var vehicles = DataManager.Vehicles;  // Retrieves the list of vehicles.
            var destination = DataManager.Destination;  // Retrieves the destination.

            if (vehicles == null || _mapControl == null)
            {
                return;  // If no vehicles or map control, exit the method.
            }

            _mapControl.Overlays.Clear();  // Clears existing overlays on the map.

            var overlay = new GMapOverlay("vehicles");  // Creates a new overlay for vehicles.

            // Adds a marker for each vehicle.
            foreach (var vehicle in vehicles)
            {
                var marker = MapOverlays.CreateVehicleMarker(vehicle);  // Creates a marker for the vehicle.
                overlay.Markers.Add(marker);  // Adds the marker to the overlay.
            }

            // Adds a marker for the destination if available.
            if (destination != default)
            {
                var destMarker = MapOverlays.CreateDestinationMarker(
                    destination.Latitude, destination.Longitude  // Adds a destination marker with its coordinates.
                );
                overlay.Markers.Add(destMarker);  // Adds the destination marker to the overlay.
            }

            // Adds the overlay to the map control.
            _mapControl.Overlays.Add(overlay);

            // Centers the map on the first vehicle or destination.
            CenterMapOnFirstPointOfInterest(vehicles, destination);
        }

        /// <summary>
        /// Centers the map on the first vehicle or destination.
        /// </summary>
        /// <param name="vehicles">The list of vehicles to check for the first one.</param>
        /// <param name="destination">The destination to center the map on.</param>
        private void CenterMapOnFirstPointOfInterest(
            List<Vehicle> vehicles,
            (int Id, string Name, double Latitude, double Longitude, string Address, string TargetTime) destination)
        {
            // Centers on the first vehicle if available.
            if (vehicles != null && vehicles.Any())
            {
                var firstVehicle = vehicles.First();
                _mapControl.Position = new GMap.NET.PointLatLng(
                    firstVehicle.StartLatitude,
                    firstVehicle.StartLongitude
                );
            }
            // Centers on the destination if available.
            else if (destination != default)
            {
                _mapControl.Position = new GMap.NET.PointLatLng(
                    destination.Latitude,
                    destination.Longitude
                );
            }

            // Sets the zoom level to a default of 12.
            _mapControl.Zoom = 12;
        }
    }
}
