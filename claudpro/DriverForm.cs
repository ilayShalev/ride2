using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using claudpro.Models;
using claudpro.Services;
using claudpro.UI;

namespace claudpro
{
    public partial class DriverForm : Form
    {
        // Fields for database and services
        private readonly DatabaseService dbService;
        private readonly MapService mapService;
        private readonly int userId;
        private readonly string username;

        // UI controls
        private GMapControl gMapControl;
        private CheckBox availabilityCheckBox;
        private RichTextBox routeDetailsTextBox;
        private Button refreshButton;
        private Button logoutButton;
        private Panel leftPanel;

        // Fields for location setting functionality
        private bool isSettingLocation = false;
        private Label locationInstructionsLabel;
        private TextBox addressTextBox;
        private Button searchAddressButton;
        private Button setLocationButton;

        // Data models
        private Vehicle vehicle;
        private List<Passenger> assignedPassengers;
        private DateTime? pickupTime;

        public DriverForm(DatabaseService dbService, MapService mapService, int userId, string username)
        {
            this.dbService = dbService;
            this.mapService = mapService;
            this.userId = userId;
            this.username = username;

            // Use the designer-generated InitializeComponent
            InitializeComponent();

            // Setup UI manually
            SetupUI();

            this.Load += async (s, e) => await LoadDriverDataAsync();
        }

        private void SetupUI()
        {
            // Set form properties - this can be moved to the designer
            this.Text = "RideMatch - Driver Interface";
            this.Size = new Size(1000, 700);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Title
            var titleLabel = ControlExtensions.CreateLabel(
                $"Welcome, {username}",
                new Point(20, 20),
                new Size(960, 30),
                new Font("Arial", 16, FontStyle.Bold),
                ContentAlignment.MiddleCenter
            );
            Controls.Add(titleLabel);

            // Left panel for controls and details
            leftPanel = ControlExtensions.CreatePanel(
                new Point(20, 70),
                new Size(350, 580),
                BorderStyle.FixedSingle
            );
            Controls.Add(leftPanel);

            // Availability controls
            leftPanel.Controls.Add(ControlExtensions.CreateLabel(
                "Tomorrow's Status:",
                new Point(20, 20),
                new Size(150, 20),
                new Font("Arial", 10, FontStyle.Bold)
            ));

            availabilityCheckBox = ControlExtensions.CreateCheckBox(
                "I am available to drive tomorrow",
                new Point(20, 50),
                new Size(300, 30),
                true
            );
            availabilityCheckBox.CheckedChanged += async (s, e) => await UpdateAvailabilityAsync();
            leftPanel.Controls.Add(availabilityCheckBox);

            var statusPanel = ControlExtensions.CreatePanel(
                new Point(20, 90),
                new Size(310, 2),
                BorderStyle.FixedSingle
            );
            statusPanel.BackColor = Color.Gray;
            leftPanel.Controls.Add(statusPanel);

            // Route details section
            leftPanel.Controls.Add(ControlExtensions.CreateLabel(
                "Your Route Details:",
                new Point(20, 110),
                new Size(200, 20),
                new Font("Arial", 10, FontStyle.Bold)
            ));

            routeDetailsTextBox = ControlExtensions.CreateRichTextBox(
                new Point(20, 140),
                new Size(310, 200),
                true
            );
            leftPanel.Controls.Add(routeDetailsTextBox);

            // Add location setting functionality
            AddLocationSettingControls();

            // Buttons
            refreshButton = ControlExtensions.CreateButton(
                "Refresh",
                new Point(20, 530),
                new Size(150, 30),
                async (s, e) => await LoadDriverDataAsync()
            );
            leftPanel.Controls.Add(refreshButton);

            logoutButton = ControlExtensions.CreateButton(
                "Logout",
                new Point(180, 530),
                new Size(150, 30),
                (s, e) => Close()
            );
            leftPanel.Controls.Add(logoutButton);

            // Map
            gMapControl = new GMapControl
            {
                Location = new Point(390, 70),
                Size = new Size(580, 580),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 13,
                DragButton = MouseButtons.Left
            };
            Controls.Add(gMapControl);
            mapService.InitializeGoogleMaps(gMapControl);
        }

        private void AddLocationSettingControls()
        {
            // Add a separator panel
            var locationPanel = ControlExtensions.CreatePanel(
                new Point(20, 350),
                new Size(310, 2),
                BorderStyle.FixedSingle
            );
            locationPanel.BackColor = Color.Gray;
            leftPanel.Controls.Add(locationPanel);

            // Add location setting section title
            leftPanel.Controls.Add(ControlExtensions.CreateLabel(
                "Set Your Starting Location:",
                new Point(20, 360),
                new Size(200, 20),
                new Font("Arial", 10, FontStyle.Bold)
            ));

            // Add a button for setting location
            setLocationButton = ControlExtensions.CreateButton(
                "Set Location on Map",
                new Point(20, 390),
                new Size(150, 30),
                (s, e) => EnableMapLocationSelection()
            );
            leftPanel.Controls.Add(setLocationButton);

            // Add a search box for address
            leftPanel.Controls.Add(ControlExtensions.CreateLabel(
                "Or Search Address:",
                new Point(20, 430),
                new Size(150, 20)
            ));

            addressTextBox = ControlExtensions.CreateTextBox(
                new Point(20, 455),
                new Size(220, 25)
            );
            searchAddressButton = ControlExtensions.CreateButton(
                "Search",
                new Point(250, 455),
                new Size(80, 25),
                async (s, e) => await SearchAddressAsync(addressTextBox.Text)
            );

            leftPanel.Controls.Add(addressTextBox);
            leftPanel.Controls.Add(searchAddressButton);

            // Add instructions label
            locationInstructionsLabel = ControlExtensions.CreateLabel(
                "Click on the map to set your starting location",
                new Point(20, 490),
                new Size(310, 20),
                null,
                ContentAlignment.MiddleCenter
            );
            locationInstructionsLabel.ForeColor = Color.Red;
            locationInstructionsLabel.Visible = false;
            leftPanel.Controls.Add(locationInstructionsLabel);
        }

        private async Task LoadDriverDataAsync()
        {
            refreshButton.Enabled = false;
            routeDetailsTextBox.Clear();
            routeDetailsTextBox.AppendText("Loading route data...\n");

            try
            {
                // Load vehicle and route data
                vehicle = await dbService.GetVehicleByUserIdAsync(userId);

                if (vehicle == null)
                {
                    routeDetailsTextBox.Clear();
                    routeDetailsTextBox.AppendText("No vehicle is assigned to you.\n");
                    routeDetailsTextBox.AppendText("Please contact your administrator to set up your vehicle.\n");
                    return;
                }

                // Update availability checkbox
                availabilityCheckBox.Checked = vehicle.IsAvailableTomorrow;

                // Get today's date in the format used by the database
                string today = DateTime.Now.ToString("yyyy-MM-dd");

                // Get route data
                var routeData = await dbService.GetDriverRouteAsync(userId, today);
                vehicle = routeData.Vehicle;
                assignedPassengers = routeData.Passengers;

                // Clear map and display the route
                ShowRouteOnMap();

                // Update route details text
                UpdateRouteDetailsText(routeData.PickupTime);
            }
            catch (Exception ex)
            {
                routeDetailsTextBox.Clear();
                routeDetailsTextBox.AppendText($"Error loading data: {ex.Message}\n");
            }
            finally
            {
                refreshButton.Enabled = true;
            }
        }

        private void ShowRouteOnMap()
        {
            if (gMapControl == null) return;

            try
            {
                gMapControl.Overlays.Clear();

                if (vehicle == null)
                    return;

                // Create overlays
                var vehiclesOverlay = new GMapOverlay("vehicles");
                var passengersOverlay = new GMapOverlay("passengers");
                var routesOverlay = new GMapOverlay("routes");
                var destinationOverlay = new GMapOverlay("destination");

                // Get destination from database
                Task.Run(async () => {
                    try
                    {
                        var destination = await dbService.GetDestinationAsync();

                        // Add destination marker
                        this.Invoke(new Action(() => {
                            try
                            {
                                var destinationMarker = MapOverlays.CreateDestinationMarker(destination.Latitude, destination.Longitude);
                                destinationOverlay.Markers.Add(destinationMarker);

                                // Show vehicle marker
                                var vehicleMarker = MapOverlays.CreateVehicleMarker(vehicle);
                                vehiclesOverlay.Markers.Add(vehicleMarker);

                                // Show passenger markers
                                if (assignedPassengers != null)
                                {
                                    foreach (var passenger in assignedPassengers)
                                    {
                                        if (passenger != null)
                                        {
                                            var passengerMarker = MapOverlays.CreatePassengerMarker(passenger);
                                            passengersOverlay.Markers.Add(passengerMarker);
                                        }
                                    }
                                }

                                // Show route
                                if (assignedPassengers != null && assignedPassengers.Count > 0)
                                {
                                    var points = new List<PointLatLng>();
                                    points.Add(new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude));

                                    foreach (var passenger in assignedPassengers)
                                    {
                                        if (passenger != null)
                                        {
                                            points.Add(new PointLatLng(passenger.Latitude, passenger.Longitude));
                                        }
                                    }

                                    points.Add(new PointLatLng(destination.Latitude, destination.Longitude));

                                    var routeColor = Color.Blue;
                                    var route = MapOverlays.CreateRoute(points, "Route", routeColor);
                                    routesOverlay.Routes.Add(route);
                                }

                                // Add overlays to map in the correct order
                                gMapControl.Overlays.Add(routesOverlay);
                                gMapControl.Overlays.Add(vehiclesOverlay);
                                gMapControl.Overlays.Add(passengersOverlay);
                                gMapControl.Overlays.Add(destinationOverlay);

                                // Center map on vehicle location
                                gMapControl.Position = new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude);
                                gMapControl.Zoom = 13;
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Error displaying route: {ex.Message}", "Map Display Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }));
                    }
                    catch (Exception ex)
                    {
                        this.Invoke(new Action(() => {
                            MessageBox.Show($"Error loading destination: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }));
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error preparing map display: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateRouteDetailsText(DateTime? departureTime)
        {
            routeDetailsTextBox.Clear();

            if (vehicle == null)
            {
                routeDetailsTextBox.AppendText("No vehicle assigned.\n");
                return;
            }

            routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
            routeDetailsTextBox.AppendText("Your Vehicle Details:\n");
            routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
            routeDetailsTextBox.AppendText($"Vehicle ID: {vehicle.Id}\n");
            routeDetailsTextBox.AppendText($"Capacity: {vehicle.Capacity}\n");

            if (!string.IsNullOrEmpty(vehicle.StartAddress))
            {
                routeDetailsTextBox.AppendText($"Starting Location: {vehicle.StartAddress}\n\n");
            }
            else
            {
                routeDetailsTextBox.AppendText($"Starting Location: ({vehicle.StartLatitude:F6}, {vehicle.StartLongitude:F6})\n\n");
            }

            if (assignedPassengers != null && assignedPassengers.Count > 0)
            {
                routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
                routeDetailsTextBox.AppendText("Assigned Passengers:\n");
                routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;

                for (int i = 0; i < assignedPassengers.Count; i++)
                {
                    var passenger = assignedPassengers[i];
                    routeDetailsTextBox.AppendText($"{i + 1}. {passenger.Name}\n");

                    if (!string.IsNullOrEmpty(passenger.Address))
                        routeDetailsTextBox.AppendText($"   Pick-up: {passenger.Address}\n");
                    else
                        routeDetailsTextBox.AppendText($"   Pick-up: ({passenger.Latitude:F6}, {passenger.Longitude:F6})\n");
                }

                routeDetailsTextBox.AppendText("\n");

                if (departureTime.HasValue)
                {
                    routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
                    routeDetailsTextBox.AppendText($"Scheduled Departure: {departureTime.Value.ToString("HH:mm")}\n");
                }
            }
            else
            {
                routeDetailsTextBox.AppendText("No passengers assigned for today's route.\n");
            }
        }

        private async Task UpdateAvailabilityAsync()
        {
            if (vehicle == null)
                return;

            try
            {
                bool success = await dbService.UpdateVehicleAvailabilityAsync(vehicle.Id, availabilityCheckBox.Checked);

                if (success)
                {
                    vehicle.IsAvailableTomorrow = availabilityCheckBox.Checked;

                    if (vehicle.IsAvailableTomorrow)
                    {
                        MessageBox.Show("You are now marked as available to drive tomorrow.",
                            "Availability Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("You are now marked as unavailable to drive tomorrow.",
                            "Availability Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    MessageBox.Show("Failed to update availability. Please try again.",
                        "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                    // Revert checkbox to match database state
                    availabilityCheckBox.CheckedChanged -= async (s, e) => await UpdateAvailabilityAsync();
                    availabilityCheckBox.Checked = vehicle.IsAvailableTomorrow;
                    availabilityCheckBox.CheckedChanged += async (s, e) => await UpdateAvailabilityAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating availability: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Revert checkbox to match database state
                availabilityCheckBox.CheckedChanged -= async (s, e) => await UpdateAvailabilityAsync();
                availabilityCheckBox.Checked = vehicle.IsAvailableTomorrow;
                availabilityCheckBox.CheckedChanged += async (s, e) => await UpdateAvailabilityAsync();
            }
        }

        /// <summary>
        /// Enables map location selection mode
        /// </summary>
        private void EnableMapLocationSelection()
        {
            isSettingLocation = true;
            locationInstructionsLabel.Visible = true;

            // Change cursor to indicate map is clickable
            gMapControl.Cursor = Cursors.Hand;

            // Add event handler for map clicks
            gMapControl.MouseClick += MapClickToSetLocation;

            MessageBox.Show("Click on the map to set your starting location",
                "Set Location", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Handles map click events when setting location
        /// </summary>
        private void MapClickToSetLocation(object sender, MouseEventArgs e)
        {
            if (!isSettingLocation) return;

            // Convert clicked point to geo coordinates
            PointLatLng point = gMapControl.FromLocalToLatLng(e.X, e.Y);

            // Set vehicle location
            UpdateVehicleLocation(point.Lat, point.Lng);

            // Disable location setting mode
            isSettingLocation = false;
            locationInstructionsLabel.Visible = false;
            gMapControl.Cursor = Cursors.Default;
            gMapControl.MouseClick -= MapClickToSetLocation;
        }

        /// <summary>
        /// Searches for an address and updates the location if found
        /// </summary>
        private async Task SearchAddressAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;

            try
            {
                // Show searching indicator
                Cursor = Cursors.WaitCursor;
                addressTextBox.Enabled = false;
                searchAddressButton.Enabled = false;

                var result = await mapService.GeocodeAddressAsync(address);
                if (result.HasValue)
                {
                    // Center map on found location
                    gMapControl.Position = new PointLatLng(result.Value.Latitude, result.Value.Longitude);
                    gMapControl.Zoom = 15;

                    // Update vehicle location
                    UpdateVehicleLocation(result.Value.Latitude, result.Value.Longitude);
                }
                else
                {
                    MessageBox.Show("Address not found. Please try again.", "Search Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error searching: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Reset cursor and enable address box
                Cursor = Cursors.Default;
                addressTextBox.Enabled = true;
                searchAddressButton.Enabled = true;
            }
        }

        /// <summary>
        /// Updates the vehicle location in the database
        /// </summary>
        private async void UpdateVehicleLocation(double latitude, double longitude)
        {
            try
            {
                // Show waiting cursor
                Cursor = Cursors.WaitCursor;

                // Get address from coordinates (reverse geocoding)
                string address = await mapService.ReverseGeocodeAsync(latitude, longitude);

                // Update vehicle in database
                if (vehicle == null)
                {
                    MessageBox.Show("No vehicle is assigned to you. Please contact your administrator.",
                        "No Vehicle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                bool success = await dbService.UpdateVehicleAsync(
                    vehicle.Id,
                    vehicle.Capacity,
                    latitude,
                    longitude,
                    address
                );

                if (success)
                {
                    // Update local vehicle data
                    vehicle.StartLatitude = latitude;
                    vehicle.StartLongitude = longitude;
                    vehicle.StartAddress = address;

                    // Show confirmation and update marker on map
                    MessageBox.Show($"Your starting location has been set to:\n{address}",
                        "Location Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Refresh map display
                    ShowRouteOnMap();

                    // Refresh vehicle details
                    await LoadDriverDataAsync();
                }
                else
                {
                    MessageBox.Show("Failed to update location. Please try again.",
                        "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating location: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Reset cursor
                Cursor = Cursors.Default;
            }
        }

        private void DriverForm_Load(object sender, EventArgs e)
        {
            // Initialization code for DriverForm
        }
    }
}