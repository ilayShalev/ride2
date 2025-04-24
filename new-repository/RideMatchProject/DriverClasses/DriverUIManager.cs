using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using RideMatchProject.Models;
using RideMatchProject.Services;
using RideMatchProject.UI;

namespace RideMatchProject.DriverClasses
{
   /// <summary>
    /// Manages UI components and interactions
    /// </summary>
    public class DriverUIManager
    {
        private readonly Form parentForm;
        private readonly DriverDataManager dataManager;
        private readonly DriverMapManager mapManager;
        private readonly DriverLocationManager locationManager;
        private readonly string username;

        // UI Controls
        private Panel leftPanel;
        private CheckBox availabilityCheckBox;
        private RichTextBox routeDetailsTextBox;
        private Button refreshButton;
        private Button logoutButton;
        private NumericUpDown capacityNumericUpDown;
        private Button updateCapacityButton;
        private Label locationInstructionsLabel;
        private Button setLocationButton;
        private TextBox addressTextBox;

        public GMapControl MapControl { get; private set; }

        public DriverUIManager(Form parentForm, DriverDataManager dataManager,
            DriverMapManager mapManager, DriverLocationManager locationManager, string username)
        {
            this.parentForm = parentForm;
            this.dataManager = dataManager;
            this.mapManager = mapManager;
            this.locationManager = locationManager;
            this.username = username;
        }

        public void InitializeUI()
        {
            SetFormProperties();
            CreateTitleLabel();
            CreateLeftPanel();
            CreateMapControl();

            locationManager.SetMapControl(MapControl);
            locationManager.SetInstructionLabel(locationInstructionsLabel);
        }

        private void SetFormProperties()
        {
            parentForm.Text = "RideMatch - Driver Interface";
            parentForm.Size = new Size(1000, 700);
            parentForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            parentForm.StartPosition = FormStartPosition.CenterScreen;
        }

        private void CreateTitleLabel()
        {
            var titleLabel = new Label
            {
                Text = $"Welcome, {username}",
                Location = new Point(20, 20),
                Size = new Size(960, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 16, FontStyle.Bold)
            };

            parentForm.Controls.Add(titleLabel);
        }

        private void CreateLeftPanel()
        {
            leftPanel = new Panel
            {
                Location = new Point(20, 70),
                Size = new Size(350, 580),
                BorderStyle = BorderStyle.FixedSingle
            };

            parentForm.Controls.Add(leftPanel);

            CreateAvailabilitySection();
            CreateCapacitySection();
            CreateRouteDetailsSection();
            CreateLocationSettingSection();
            CreateActionButtons();
        }

        private void CreateAvailabilitySection()
        {
            var availabilityLabel = new Label
            {
                Text = "Tomorrow's Status:",
                Location = new Point(20, 20),
                Size = new Size(150, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            leftPanel.Controls.Add(availabilityLabel);

            availabilityCheckBox = new CheckBox
            {
                Text = "I am available to drive tomorrow",
                Location = new Point(20, 50),
                Size = new Size(300, 30),
                Checked = true
            };

            availabilityCheckBox.CheckedChanged += AvailabilityCheckBox_CheckedChanged;
            leftPanel.Controls.Add(availabilityCheckBox);
        }

        private void CreateCapacitySection()
        {
            var capacityLabel = new Label
            {
                Text = "Vehicle Capacity:",
                Location = new Point(20, 90),
                Size = new Size(150, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            leftPanel.Controls.Add(capacityLabel);

            var seatsLabel = new Label
            {
                Text = "Number of seats:",
                Location = new Point(20, 120),
                Size = new Size(150, 20)
            };

            leftPanel.Controls.Add(seatsLabel);

            capacityNumericUpDown = new NumericUpDown
            {
                Location = new Point(180, 120),
                Size = new Size(80, 25),
                Minimum = 1,
                Maximum = 20,
                Value = 4
            };

            leftPanel.Controls.Add(capacityNumericUpDown);

            updateCapacityButton = new Button
            {
                Text = "Update Capacity",
                Location = new Point(180, 150),
                Size = new Size(150, 30)
            };

            updateCapacityButton.Click += UpdateCapacityButton_Click;
            leftPanel.Controls.Add(updateCapacityButton);

            AddSeparator(190);
        }

        private void CreateRouteDetailsSection()
        {
            var routeLabel = new Label
            {
                Text = "Your Route Details:",
                Location = new Point(20, 210),
                Size = new Size(200, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            leftPanel.Controls.Add(routeLabel);

            routeDetailsTextBox = new RichTextBox
            {
                Location = new Point(20, 240),
                Size = new Size(310, 160),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle
            };

            leftPanel.Controls.Add(routeDetailsTextBox);
            routeDetailsTextBox.AppendText("Loading driver data...\n");

            AddSeparator(410);
        }

        private void CreateLocationSettingSection()
        {
            var locationLabel = new Label
            {
                Text = "Set Your Starting Location:",
                Location = new Point(20, 420),
                Size = new Size(200, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            leftPanel.Controls.Add(locationLabel);

            setLocationButton = new Button
            {
                Text = "Set Location on Map",
                Location = new Point(20, 445),
                Size = new Size(150, 30)
            };

            setLocationButton.Click += SetLocationButton_Click;
            leftPanel.Controls.Add(setLocationButton);

            var addressLabel = new Label
            {
                Text = "Or Search Address:",
                Location = new Point(20, 472),
                Size = new Size(150, 20)
            };

            leftPanel.Controls.Add(addressLabel);

            addressTextBox = new TextBox
            {
                Location = new Point(20, 500),
                Size = new Size(220, 25)
            };

            leftPanel.Controls.Add(addressTextBox);

            var searchButton = new Button
            {
                Text = "Search",
                Location = new Point(250, 500),
                Size = new Size(80, 25)
            };

            searchButton.Click += SearchButton_Click;
            leftPanel.Controls.Add(searchButton);

            locationInstructionsLabel = new Label
            {
                Text = "Click on the map to set your starting location",
                Location = new Point(20, 550),
                Size = new Size(310, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Red,
                Visible = false
            };

            leftPanel.Controls.Add(locationInstructionsLabel);
        }

        private void CreateActionButtons()
        {
            refreshButton = new Button
            {
                Text = "Refresh",
                Location = new Point(20, 530),
                Size = new Size(150, 30)
            };

            refreshButton.Click += RefreshButton_Click;
            leftPanel.Controls.Add(refreshButton);

            logoutButton = new Button
            {
                Text = "Logout",
                Location = new Point(180, 530),
                Size = new Size(150, 30)
            };

            logoutButton.Click += (s, e) => parentForm.Close();
            leftPanel.Controls.Add(logoutButton);
        }

        private void CreateMapControl()
        {
            MapControl = new GMapControl
            {
                Location = new Point(390, 70),
                Size = new Size(580, 580),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 13,
                DragButton = MouseButtons.Left
            };

            parentForm.Controls.Add(MapControl);
        }

        private void AddSeparator(int yPosition)
        {
            var separatorPanel = new Panel
            {
                Location = new Point(20, yPosition),
                Size = new Size(310, 2),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Gray
            };

            leftPanel.Controls.Add(separatorPanel);
        }

        public void RefreshUI()
        {
            if (dataManager.Vehicle != null)
            {
                availabilityCheckBox.Checked = dataManager.Vehicle.IsAvailableTomorrow;
                capacityNumericUpDown.Value = dataManager.Vehicle.Capacity;
            }

            UpdateRouteDetailsText();
        }

        private void UpdateRouteDetailsText()
        {
            routeDetailsTextBox.Clear();

            if (dataManager.Vehicle == null)
            {
                routeDetailsTextBox.AppendText("No vehicle assigned.\n");
                return;
            }

            DisplayVehicleDetails();
            DisplayPassengerDetails();
        }

        private void DisplayVehicleDetails()
        {
            routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
            routeDetailsTextBox.AppendText("Your Vehicle Details:\n");
            routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
            routeDetailsTextBox.AppendText($"Capacity: {dataManager.Vehicle.Capacity} seats\n");

            DisplayLocationInfo();

            if (!string.IsNullOrEmpty(dataManager.Vehicle.DepartureTime))
            {
                routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
                routeDetailsTextBox.AppendText($"\nDeparture Time: {dataManager.Vehicle.DepartureTime}\n\n");
                routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
            }
        }

        private void DisplayLocationInfo()
        {
            var vehicle = dataManager.Vehicle;

            if (vehicle.StartLatitude == 0 && vehicle.StartLongitude == 0)
            {
                routeDetailsTextBox.AppendText("Starting Location: Not set\n\n");
                routeDetailsTextBox.AppendText("Please set your starting location using the options below.\n");
                return;
            }

            if (!string.IsNullOrEmpty(vehicle.StartAddress))
            {
                routeDetailsTextBox.AppendText($"Starting Location: {vehicle.StartAddress}\n");
                return;
            }

            routeDetailsTextBox.AppendText(
                $"Starting Location: ({vehicle.StartLatitude:F6}, {vehicle.StartLongitude:F6})\n");
        }

        private void DisplayPassengerDetails()
        {
            var passengers = dataManager.AssignedPassengers;

            if (passengers == null || passengers.Count == 0)
            {
                routeDetailsTextBox.AppendText("\nNo passengers assigned for today's route.\n");
                return;
            }

            routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
            routeDetailsTextBox.AppendText("Assigned Passengers:\n");
            routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;

            for (int i = 0; i < passengers.Count; i++)
            {
                var passenger = passengers[i];
                if (passenger == null) continue;

                routeDetailsTextBox.AppendText($"{i + 1}. {passenger.Name}\n");
                DisplayPassengerAddress(passenger);
                DisplayPickupTime(passenger, i);
                routeDetailsTextBox.AppendText("\n");
            }
        }

        private void DisplayPassengerAddress(Passenger passenger)
        {
            if (!string.IsNullOrEmpty(passenger.Address))
            {
                routeDetailsTextBox.AppendText($"   Pick-up: {passenger.Address}\n");
                return;
            }

            routeDetailsTextBox.AppendText(
                $"   Pick-up: ({passenger.Latitude:F6}, {passenger.Longitude:F6})\n");
        }

        private void DisplayPickupTime(Passenger passenger, int index)
        {
            routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);

            if (!string.IsNullOrEmpty(passenger.EstimatedPickupTime))
            {
                routeDetailsTextBox.AppendText($"   Pick-up Time: {passenger.EstimatedPickupTime}\n");
            }
            else if (index == 0 && dataManager.PickupTime.HasValue)
            {
                routeDetailsTextBox.AppendText(
                    $"   Pick-up Time: {dataManager.PickupTime.Value.ToString("HH:mm")}\n");
            }

            routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
        }

        private async void AvailabilityCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                bool success = await dataManager.UpdateVehicleAvailabilityAsync(availabilityCheckBox.Checked);

                if (success)
                {
                    string message = availabilityCheckBox.Checked ?
                        "You are now marked as available to drive tomorrow." :
                        "You are now marked as unavailable to drive tomorrow.";

                    MessageBox.Show(message, "Availability Updated",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                MessageBox.Show("Failed to update availability. Please try again.",
                    "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                RevertAvailabilityCheckbox();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating availability: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                RevertAvailabilityCheckbox();
            }
        }

        private void RevertAvailabilityCheckbox()
        {
            availabilityCheckBox.CheckedChanged -= AvailabilityCheckBox_CheckedChanged;
            availabilityCheckBox.Checked = dataManager.Vehicle?.IsAvailableTomorrow ?? true;
            availabilityCheckBox.CheckedChanged += AvailabilityCheckBox_CheckedChanged;
        }

        private async void UpdateCapacityButton_Click(object sender, EventArgs e)
        {
            try
            {
                int newCapacity = (int)capacityNumericUpDown.Value;
                bool success = await dataManager.UpdateVehicleCapacityAsync(newCapacity);

                if (success)
                {
                    MessageBox.Show($"Vehicle capacity updated to {newCapacity} seats.",
                        "Capacity Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    UpdateRouteDetailsText();
                    return;
                }

                MessageBox.Show("Failed to update vehicle capacity. Please try again.",
                    "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                capacityNumericUpDown.Value = dataManager.Vehicle?.Capacity ?? 4;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating vehicle capacity: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                capacityNumericUpDown.Value = dataManager.Vehicle?.Capacity ?? 4;
            }
        }

        private void SetLocationButton_Click(object sender, EventArgs e)
        {
            locationManager.EnableLocationSelection();
        }

        private async void SearchButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(addressTextBox.Text)) return;

            await locationManager.SearchAddressAsync(addressTextBox.Text);
        }

        private async void RefreshButton_Click(object sender, EventArgs e)
        {
            refreshButton.Enabled = false;
            routeDetailsTextBox.Clear();
            routeDetailsTextBox.AppendText("Loading route data...\n");

            try
            {
                await dataManager.LoadDriverDataAsync();
                RefreshUI();
                mapManager.DisplayRouteOnMap(dataManager.Vehicle, dataManager.AssignedPassengers);
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
    }
}
