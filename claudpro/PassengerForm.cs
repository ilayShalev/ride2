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
        // Fields for location setting functionality
        private bool isSettingLocation = false;
        private Label locationInstructionsLabel;
        private TextBox addressTextBox;

        public PassengerForm(DatabaseService dbService, MapService mapService, int userId, string username)
        {
            this.dbService = dbService;
            this.mapService = mapService;
            this.userId = userId;
            this.username = username;

            // Use the designer-generated InitializeComponent
            InitializeComponent();

            // Setup UI manually
            SetupUI();

            this.Load += async (s, e) => await LoadPassengerDataAsync();
        }

        private void SetupUI()
        {
            // Set form properties - this can be moved to the designer
            this.Text = "RideMatch - Passenger Interface";
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
            // Add location setting controls
            AddLocationSettingControls();
        }

        private async Task LoadPassengerDataAsync()
        {
            // Implementation preserved for brevity
            refreshButton.Enabled = false;
            assignmentDetailsTextBox.Clear();
            assignmentDetailsTextBox.AppendText("Loading ride details...\n");

            try
            {
                // Load passenger data
                passenger = await dbService.GetPassengerByUserIdAsync(userId);
                // Rest of implementation
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
            // Implementation preserved for brevity
            gMapControl.Overlays.Clear();

            if (passenger == null)
                return;

            // Create overlays
            var vehiclesOverlay = new GMapOverlay("vehicles");
            var passengersOverlay = new GMapOverlay("passengers");
            var routesOverlay = new GMapOverlay("routes");
            var destinationOverlay = new GMapOverlay("destination");

            // Rest of implementation
        }

        private void UpdateAssignmentDetailsText()
        {
            // Implementation preserved for brevity
            assignmentDetailsTextBox.Clear();

            if (passenger == null)
            {
                assignmentDetailsTextBox.AppendText("No passenger profile found.\n");
                return;
            }
            // Rest of implementation
        }

        private async Task UpdateAvailabilityAsync()
        {
            // Implementation preserved for brevity
            if (passenger == null)
                return;

            try
            {
                bool success = await dbService.UpdatePassengerAvailabilityAsync(passenger.Id, availabilityCheckBox.Checked);
                // Rest of implementation
            }
            catch (Exception ex)
            {
                // Exception handling
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // PassengerForm
            // 
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Name = "PassengerForm";
            this.Load += new System.EventHandler(this.PassengerForm_Load);
            this.ResumeLayout(false);

        }

        private void PassengerForm_Load(object sender, EventArgs e)
        {

        }


        private void AddLocationSettingControls()
        {
            // Method implementation
        }

        private void EnableMapLocationSelection()
        {
            // Method implementation
        }

        private void MapClickToSetLocation(object sender, MouseEventArgs e)
        {
            // Method implementation
        }

        private async Task SearchAddressAsync(string address)
        {
            // Method implementation
        }

        private async void UpdatePassengerLocation(double latitude, double longitude)
        {
            // Method implementation
        }

        private void DisplayPassengerOnMap()
        {
            // Method implementation
        }
    }
}