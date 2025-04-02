using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using claudpro.Models;
using claudpro.Services;
using claudpro.UI;

namespace claudpro
{
    public partial class DriverForm : Form
    {
        private readonly DatabaseService dbService;
        private readonly MapService mapService;
        private readonly int userId;
        private readonly string username;
        private Vehicle vehicle;
        private List<Passenger> assignedPassengers;
        private GMapControl gMapControl;
        private RichTextBox routeDetailsTextBox;
        private CheckBox availabilityCheckBox;
        private Button refreshButton;
        private Button logoutButton;

        public DriverForm(DatabaseService dbService, MapService mapService, int userId, string username)
        {
            this.dbService = dbService;
            this.mapService = mapService;
            this.userId = userId;
            this.username = username;

            InitializeComponent();
            SetupUI();

            this.Load += async (s, e) => await LoadDriverDataAsync();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "RideMatch - Driver Interface";
            this.Size = new Size(1000, 700);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;

            this.ResumeLayout(false);
        }

        private void SetupUI()
        {
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
            var leftPanel = ControlExtensions.CreatePanel(
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
                new Size(310, 380),
                true
            );
            leftPanel.Controls.Add(routeDetailsTextBox);

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
                DisplayRouteOnMap();

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

        private void DisplayRouteOnMap()
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
                var destination = await dbService.GetDestinationAsync();

                // Add destination marker
                this.Invoke(new Action(() => {
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
                            var passengerMarker = MapOverlays.CreatePassengerMarker(passenger);
                            passengersOverlay.Markers.Add(passengerMarker);
                        }
                    }

                    // Show route
                    if (assignedPassengers != null && assignedPassengers.Count > 0)
                    {
                        var points = new List<PointLatLng>();
                        points.Add(new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude));

                        foreach (var passenger in assignedPassengers)
                        {
                            points.Add(new PointLatLng(passenger.Latitude, passenger.Longitude));
                        }

                        points.Add(new PointLatLng(destination.Latitude, destination.Longitude));

                        var routeColor = Color.Blue;
                        var route = MapOverlays.CreateRoute(points, "Route", routeColor);
                        routesOverlay.Routes.Add(route);
                    }

                    // Add overlays to map
                    gMapControl.Overlays.Add(routesOverlay);
                    gMapControl.Overlays.Add(vehiclesOverlay);
                    gMapControl.Overlays.Add(passengersOverlay);
                    gMapControl.Overlays.Add(destinationOverlay);

                    // Center map on vehicle location
                    gMapControl.Position = new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude);
                    gMapControl.Zoom = 13;
                }));
            });
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
                routeDetailsTextBox.AppendText($"Start Location: {vehicle.StartAddress}\n");
            else
                routeDetailsTextBox.AppendText($"Start Location: ({vehicle.StartLatitude:F4}, {vehicle.StartLongitude:F4})\n");

            routeDetailsTextBox.AppendText("\n");

            if (assignedPassengers == null || assignedPassengers.Count == 0)
            {
                routeDetailsTextBox.AppendText("No passengers assigned for today.\n");
                return;
            }

            routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
            routeDetailsTextBox.AppendText("Assigned Passengers:\n");
            routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;

            for (int i = 0; i < assignedPassengers.Count; i++)
            {
                var passenger = assignedPassengers[i];
                routeDetailsTextBox.AppendText($"{i + 1}. {passenger.Name}\n");

                if (!string.IsNullOrEmpty(passenger.Address))
                    routeDetailsTextBox.AppendText($"   Address: {passenger.Address}\n");
                else
                    routeDetailsTextBox.AppendText($"   Location: ({passenger.Latitude:F4}, {passenger.Longitude:F4})\n");

                // Display estimated pickup time if available
                if (!string.IsNullOrEmpty(passenger.EstimatedPickupTime))
                {
                    routeDetailsTextBox.AppendText($"   Pickup time: {passenger.EstimatedPickupTime}\n");
                }

                routeDetailsTextBox.AppendText("\n");
            }

            routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
            routeDetailsTextBox.AppendText("Route Summary:\n");
            routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
            routeDetailsTextBox.AppendText($"Total Distance: {vehicle.TotalDistance:F2} km\n");
            routeDetailsTextBox.AppendText($"Total Time: {vehicle.TotalTime:F2} minutes\n");

            if (departureTime.HasValue)
            {
                routeDetailsTextBox.AppendText($"Recommended Departure Time: {departureTime.Value.ToShortTimeString()}\n");
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
                    MessageBox.Show(
                        $"Your availability for tomorrow has been updated to: {(availabilityCheckBox.Checked ? "Available" : "Not Available")}",
                        "Status Updated",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                else
                {
                    MessageBox.Show(
                        "Failed to update your availability. Please try again.",
                        "Update Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    availabilityCheckBox.Checked = vehicle.IsAvailableTomorrow;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error updating availability: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                availabilityCheckBox.Checked = vehicle.IsAvailableTomorrow;
            }
        }
    }
}