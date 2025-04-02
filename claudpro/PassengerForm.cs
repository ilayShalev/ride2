using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using claudpro.Models;
using claudpro.Services;
using claudpro.UI;
using System.Collections.Generic;

namespace claudpro
{
    public partial class PassengerForm : Form
    {
        private readonly DatabaseService dbService;
        private readonly MapService mapService;
        private readonly int userId;
        private readonly string username;
        private Passenger passenger;
        private Vehicle assignedVehicle;
        private DateTime? pickupTime;
        private GMapControl gMapControl;
        private RichTextBox assignmentDetailsTextBox;
        private CheckBox availabilityCheckBox;
        private Button refreshButton;
        private Button logoutButton;

        public PassengerForm(DatabaseService dbService, MapService mapService, int userId, string username)
        {
            this.dbService = dbService;
            this.mapService = mapService;
            this.userId = userId;
            this.username = username;

            InitializeComponent();
            SetupUI();

            this.Load += async (s, e) => await LoadPassengerDataAsync();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "RideMatch - Passenger Interface";
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
                "I need a ride tomorrow",
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

            // Assignment details section
            leftPanel.Controls.Add(ControlExtensions.CreateLabel(
                "Your Ride Details:",
                new Point(20, 110),
                new Size(200, 20),
                new Font("Arial", 10, FontStyle.Bold)
            ));

            assignmentDetailsTextBox = ControlExtensions.CreateRichTextBox(
                new Point(20, 140),
                new Size(310, 380),
                true
            );
            leftPanel.Controls.Add(assignmentDetailsTextBox);

            // Buttons
            refreshButton = ControlExtensions.CreateButton(
                "Refresh",
                new Point(20, 530),
                new Size(150, 30),
                async (s, e) => await LoadPassengerDataAsync()
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

        private async Task LoadPassengerDataAsync()
        {
            refreshButton.Enabled = false;
            assignmentDetailsTextBox.Clear();
            assignmentDetailsTextBox.AppendText("Loading ride details...\n");

            try
            {
                // Load passenger data
                passenger = await dbService.GetPassengerByUserIdAsync(userId);

                if (passenger == null)
                {
                    assignmentDetailsTextBox.Clear();
                    assignmentDetailsTextBox.AppendText("No passenger profile is set up for you.\n");
                    assignmentDetailsTextBox.AppendText("Please contact your administrator to set up your profile.\n");
                    return;
                }

                // Update availability checkbox
                availabilityCheckBox.Checked = passenger.IsAvailableTomorrow;

                // Get today's date in the format used by the database
                string today = DateTime.Now.ToString("yyyy-MM-dd");

                // Get assignment data
                var assignment = await dbService.GetPassengerAssignmentAsync(userId, today);
                assignedVehicle = assignment.AssignedVehicle;
                pickupTime = assignment.PickupTime;

                // Clear map and display the assignment
                DisplayAssignmentOnMap();

                // Update assignment details text
                UpdateAssignmentDetailsText();
            }
            catch (Exception ex)
            {
                assignmentDetailsTextBox.Clear();
                assignmentDetailsTextBox.AppendText($"Error loading data: {ex.Message}\n");
            }
            finally
            {
                refreshButton.Enabled = true;
            }
        }

        private void DisplayAssignmentOnMap()
        {
            gMapControl.Overlays.Clear();

            if (passenger == null)
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

                    // Show passenger marker
                    var passengerMarker = MapOverlays.CreatePassengerMarker(passenger);
                    passengersOverlay.Markers.Add(passengerMarker);

                    // Show vehicle marker if assigned
                    if (assignedVehicle != null)
                    {
                        var vehicleMarker = MapOverlays.CreateVehicleMarker(assignedVehicle);
                        vehiclesOverlay.Markers.Add(vehicleMarker);

                        // Show route from vehicle to passenger to destination
                        var points = new List<PointLatLng>();
                        points.Add(new PointLatLng(assignedVehicle.StartLatitude, assignedVehicle.StartLongitude));
                        points.Add(new PointLatLng(passenger.Latitude, passenger.Longitude));
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

                    // Center map on passenger location
                    gMapControl.Position = new PointLatLng(passenger.Latitude, passenger.Longitude);
                    gMapControl.Zoom = 13;
                }));
            });
        }

        private void UpdateAssignmentDetailsText()
        {
            assignmentDetailsTextBox.Clear();

            if (passenger == null)
            {
                assignmentDetailsTextBox.AppendText("No passenger profile found.\n");
                return;
            }

            assignmentDetailsTextBox.SelectionFont = new Font(assignmentDetailsTextBox.Font, FontStyle.Bold);
            assignmentDetailsTextBox.AppendText("Your Information:\n");
            assignmentDetailsTextBox.SelectionFont = assignmentDetailsTextBox.Font;
            assignmentDetailsTextBox.AppendText($"Name: {passenger.Name}\n");

            if (!string.IsNullOrEmpty(passenger.Address))
                assignmentDetailsTextBox.AppendText($"Address: {passenger.Address}\n");
            else
                assignmentDetailsTextBox.AppendText($"Location: ({passenger.Latitude:F4}, {passenger.Longitude:F4})\n");

            assignmentDetailsTextBox.AppendText("\n");

            if (assignedVehicle == null)
            {
                assignmentDetailsTextBox.AppendText("No ride assigned for today.\n");
                assignmentDetailsTextBox.AppendText("Please check back later or contact your administrator.\n");
                return;
            }

            assignmentDetailsTextBox.SelectionFont = new Font(assignmentDetailsTextBox.Font, FontStyle.Bold);
            assignmentDetailsTextBox.AppendText("Your Ride Assignment:\n");
            assignmentDetailsTextBox.SelectionFont = assignmentDetailsTextBox.Font;

            assignmentDetailsTextBox.AppendText($"Driver: {assignedVehicle.DriverName}\n");

            if (pickupTime.HasValue)
            {
                assignmentDetailsTextBox.SelectionFont = new Font(assignmentDetailsTextBox.Font, FontStyle.Bold);
                assignmentDetailsTextBox.AppendText($"Pickup Time: {pickupTime.Value.ToShortTimeString()}\n");
                assignmentDetailsTextBox.SelectionFont = assignmentDetailsTextBox.Font;
            }
            else
            {
                assignmentDetailsTextBox.AppendText("Pickup Time: Not specified yet\n");
            }

            assignmentDetailsTextBox.AppendText("\n");

            if (!string.IsNullOrEmpty(assignedVehicle.StartAddress))
                assignmentDetailsTextBox.AppendText($"Driver's Starting Location: {assignedVehicle.StartAddress}\n");

            assignmentDetailsTextBox.AppendText("\n");
            assignmentDetailsTextBox.AppendText("Please be ready at the pickup time and location shown on the map.\n");
            assignmentDetailsTextBox.AppendText("If you need to cancel your ride for tomorrow, please uncheck the 'I need a ride tomorrow' box above.\n");
        }

        private async Task UpdateAvailabilityAsync()
        {
            if (passenger == null)
                return;

            try
            {
                bool success = await dbService.UpdatePassengerAvailabilityAsync(passenger.Id, availabilityCheckBox.Checked);
                if (success)
                {
                    passenger.IsAvailableTomorrow = availabilityCheckBox.Checked;
                    MessageBox.Show(
                        $"Your ride request for tomorrow has been {(availabilityCheckBox.Checked ? "activated" : "deactivated")}",
                        "Status Updated",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                else
                {
                    MessageBox.Show(
                        "Failed to update your ride request. Please try again.",
                        "Update Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    availabilityCheckBox.Checked = passenger.IsAvailableTomorrow;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error updating ride request: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                availabilityCheckBox.Checked = passenger.IsAvailableTomorrow;
            }
        }
    }
}