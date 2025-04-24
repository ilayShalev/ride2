﻿using GMap.NET.WindowsForms;
using RideMatchProject.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.PassengerClasses
{
    /// <summary>
    /// Manages the user interface components
    /// </summary>
    public class PassengerUIManager
    {
        private readonly Form _parentForm;
        private readonly string _username;

        private GMapControl _mapControl;
        private CheckBox _availabilityCheckBox;
        private RichTextBox _detailsTextBox;
        private Button _refreshButton;
        private Button _logoutButton;
        private Button _setLocationButton;
        private TextBox _addressTextBox;
        private Button _searchButton;
        private Label _instructionsLabel;
        private Panel _leftPanel;

        public event EventHandler<bool> AvailabilityChanged;
        public event EventHandler RefreshRequested;
        public event EventHandler SetLocationRequested;
        public event EventHandler<string> AddressSearchRequested;

        public PassengerUIManager(Form parentForm, string username)
        {
            _parentForm = parentForm;
            _username = username;

            InitializeUI();
        }

        public void InitializeUI()
        {
            CreateTitleLabel();
            CreateLeftPanel();
            CreateMapControl();

            CreateAvailabilitySection();
            CreateDetailsSection();
            CreateLocationSection();
            CreateActionButtons();
        }

        private void CreateTitleLabel()
        {
            var titleLabel = new Label
            {
                Text = $"Welcome, {_username}",
                Location = new Point(20, 20),
                Size = new Size(960, 30),
                Font = new Font("Arial", 16, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _parentForm.Controls.Add(titleLabel);
        }

        private void CreateLeftPanel()
        {
            _leftPanel = new Panel
            {
                Location = new Point(20, 70),
                Size = new Size(350, 580),
                BorderStyle = BorderStyle.FixedSingle
            };
            _parentForm.Controls.Add(_leftPanel);
        }

        private void CreateMapControl()
        {
            _mapControl = new GMapControl
            {
                Location = new Point(390, 70),
                Size = new Size(580, 580),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 13,
                DragButton = MouseButtons.Left
            };
            _parentForm.Controls.Add(_mapControl);
        }

        private void CreateAvailabilitySection()
        {
            var statusLabel = new Label
            {
                Text = "Tomorrow's Status:",
                Location = new Point(20, 20),
                Size = new Size(150, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            _leftPanel.Controls.Add(statusLabel);

            _availabilityCheckBox = new CheckBox
            {
                Text = "I need a ride tomorrow",
                Location = new Point(20, 50),
                Size = new Size(300, 30),
                Checked = true
            };

            _availabilityCheckBox.CheckedChanged += (s, e) =>
                AvailabilityChanged?.Invoke(this, _availabilityCheckBox.Checked);

            _leftPanel.Controls.Add(_availabilityCheckBox);

            var divider = new Panel
            {
                Location = new Point(20, 90),
                Size = new Size(310, 2),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Gray
            };
            _leftPanel.Controls.Add(divider);
        }

        private void CreateDetailsSection()
        {
            var detailsLabel = new Label
            {
                Text = "Your Ride Details:",
                Location = new Point(20, 110),
                Size = new Size(200, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            _leftPanel.Controls.Add(detailsLabel);

            _detailsTextBox = new RichTextBox
            {
                Location = new Point(20, 140),
                Size = new Size(310, 200),
                ReadOnly = true
            };
            _leftPanel.Controls.Add(_detailsTextBox);
        }

        private void CreateLocationSection()
        {
            var divider = new Panel
            {
                Location = new Point(20, 350),
                Size = new Size(310, 2),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Gray
            };
            _leftPanel.Controls.Add(divider);

            var locationLabel = new Label
            {
                Text = "Set Your Pickup Location:",
                Location = new Point(20, 360),
                Size = new Size(200, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            _leftPanel.Controls.Add(locationLabel);

            // Create button without event handler first
            _setLocationButton = new Button
            {
                Text = "Set Location on Map",
                Location = new Point(20, 390),
                Size = new Size(150, 30)
            };
            // Add event handler separately
            _setLocationButton.Click += (s, e) => SetLocationRequested?.Invoke(this, EventArgs.Empty);
            _leftPanel.Controls.Add(_setLocationButton);

            var searchLabel = new Label
            {
                Text = "Or Search Address:",
                Location = new Point(20, 430),
                Size = new Size(150, 20)
            };
            _leftPanel.Controls.Add(searchLabel);

            _addressTextBox = new TextBox
            {
                Location = new Point(20, 455),
                Size = new Size(220, 25)
            };

            // Create button without event handler first
            _searchButton = new Button
            {
                Text = "Search",
                Location = new Point(250, 455),
                Size = new Size(80, 25)
            };
            // Add event handler separately
            _searchButton.Click += (s, e) => AddressSearchRequested?.Invoke(this, _addressTextBox.Text);
            _leftPanel.Controls.Add(_addressTextBox);
            _leftPanel.Controls.Add(_searchButton);

            _instructionsLabel = new Label
            {
                Text = "Click on the map to set your pickup location",
                Location = new Point(20, 490),
                Size = new Size(310, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Red,
                Visible = false
            };
            _leftPanel.Controls.Add(_instructionsLabel);
        }

        private void CreateActionButtons()
        {
            // Create button without event handler first
            _refreshButton = new Button
            {
                Text = "Refresh",
                Location = new Point(20, 530),
                Size = new Size(150, 30)
            };
            // Add event handler separately
            _refreshButton.Click += (s, e) => RefreshRequested?.Invoke(this, EventArgs.Empty);
            _leftPanel.Controls.Add(_refreshButton);

            // Create button without event handler first
            _logoutButton = new Button
            {
                Text = "Logout",
                Location = new Point(180, 530),
                Size = new Size(150, 30)
            };
            // Add event handler separately
            _logoutButton.Click += (s, e) => _parentForm.Close();
            _leftPanel.Controls.Add(_logoutButton);
        }

        public void ShowLoadingMessage(string message)
        {
            InvokeOnUIThread(() => {
                _detailsTextBox.Clear();
                _detailsTextBox.AppendText(message);
                _refreshButton.Enabled = false;
            });
        }

        public void DisplayPassengerDetails(Passenger passenger, Vehicle vehicle, DateTime? pickupTime)
        {
            InvokeOnUIThread(() => {
                if (_detailsTextBox == null)
                {
                    return;
                }

                _detailsTextBox.Clear();

                AppendPassengerInfo(passenger);

                if (vehicle != null)
                {
                    AppendVehicleInfo(vehicle, passenger, pickupTime);
                }
                else
                {
                    AppendNoVehicleMessage();
                }

                _refreshButton.Enabled = true;
            });
        }

        private void AppendPassengerInfo(Passenger passenger)
        {
            _detailsTextBox.SelectionFont = new Font(_detailsTextBox.Font, FontStyle.Bold);
            _detailsTextBox.AppendText("Your Information:\n");
            _detailsTextBox.SelectionFont = _detailsTextBox.Font;
            _detailsTextBox.AppendText($"Name: {passenger.Name}\n");

            if (!string.IsNullOrEmpty(passenger.Address))
            {
                _detailsTextBox.AppendText($"Pickup Location: {passenger.Address}\n\n");
            }
            else
            {
                _detailsTextBox.AppendText(
                    $"Pickup Location: ({passenger.Latitude:F6}, {passenger.Longitude:F6})\n\n");
            }
        }

        private void AppendVehicleInfo(Vehicle vehicle, Passenger passenger, DateTime? pickupTime)
        {
            _detailsTextBox.SelectionFont = new Font(_detailsTextBox.Font, FontStyle.Bold);
            _detailsTextBox.AppendText("Your Scheduled Ride:\n");
            _detailsTextBox.SelectionFont = _detailsTextBox.Font;

            string driverName = !string.IsNullOrEmpty(vehicle.DriverName)
                ? vehicle.DriverName
                : $"Driver #{vehicle.Id}";

            _detailsTextBox.AppendText($"Driver: {driverName}\n");

            if (!string.IsNullOrEmpty(vehicle.Model))
            {
                _detailsTextBox.AppendText($"Vehicle: {vehicle.Model}\n");
            }

            if (!string.IsNullOrEmpty(vehicle.Color))
            {
                _detailsTextBox.AppendText($"Color: {vehicle.Color}\n");
            }

            if (!string.IsNullOrEmpty(vehicle.LicensePlate))
            {
                _detailsTextBox.AppendText($"License Plate: {vehicle.LicensePlate}\n");
            }

            AppendPickupTimeInfo(pickupTime);

            if (!string.IsNullOrEmpty(vehicle.StartAddress))
            {
                _detailsTextBox.AppendText($"Driver Starting From: {vehicle.StartAddress}\n");
            }
        }

        private void AppendPickupTimeInfo(DateTime? pickupTime)
        {
            if (pickupTime.HasValue)
            {
                _detailsTextBox.SelectionFont = new Font(_detailsTextBox.Font, FontStyle.Bold);
                _detailsTextBox.AppendText($"Pickup Time: {pickupTime.Value.ToString("HH:mm")}\n");
                _detailsTextBox.SelectionFont = _detailsTextBox.Font;
            }

            else
            {
                _detailsTextBox.AppendText("Pickup Time: Not yet scheduled\n");
            }
        }

        private void AppendNoVehicleMessage()
        {
            _detailsTextBox.SelectionFont = new Font(_detailsTextBox.Font, FontStyle.Bold);
            _detailsTextBox.AppendText("No Ride Scheduled Yet\n");
            _detailsTextBox.SelectionFont = _detailsTextBox.Font;
            _detailsTextBox.AppendText("Rides for tomorrow will be assigned by the system overnight.\n");
            _detailsTextBox.AppendText("Please check back tomorrow morning for your ride details.\n");
        }

        public void ShowNoProfileMessage()
        {
            InvokeOnUIThread(() => {
                _detailsTextBox.Clear();
                _detailsTextBox.AppendText("No passenger profile found. Set your location to create a profile.\n");
                _refreshButton.Enabled = true;
            });
        }

        public void ShowErrorMessage(string message)
        {
            InvokeOnUIThread(() => {
                MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            });
        }

        public void ShowLocationUpdatedMessage(string address)
        {
            InvokeOnUIThread(() => {
                MessageBox.Show($"Your pickup location has been set to:\n{address}",
                    "Location Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        public void ShowRideRequestMessage()
        {
            InvokeOnUIThread(() => {
                MessageBox.Show("Your ride request has been submitted. A driver will be assigned soon.",
                    "Status Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        public void UpdateAvailabilityControl(bool isAvailable)
        {
            InvokeOnUIThread(() => {
                _availabilityCheckBox.CheckedChanged -= (s, e) =>
                    AvailabilityChanged?.Invoke(this, _availabilityCheckBox.Checked);

                _availabilityCheckBox.Checked = isAvailable;

                _availabilityCheckBox.CheckedChanged += (s, e) =>
                    AvailabilityChanged?.Invoke(this, _availabilityCheckBox.Checked);
            });
        }

        public void ShowLocationSelectionInstructions(bool visible)
        {
            InvokeOnUIThread(() => {
                _instructionsLabel.Visible = visible;
            });
        }

        public void SetSearchControlsEnabled(bool enabled)
        {
            InvokeOnUIThread(() => {
                _addressTextBox.Enabled = enabled;
                _searchButton.Enabled = enabled;
                _parentForm.Cursor = enabled ? Cursors.Default : Cursors.WaitCursor;
            });
        }

        public void ShowBusyState(bool busy)
        {
            InvokeOnUIThread(() => {
                _parentForm.Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            });
        }

        public GMapControl GetMapControl()
        {
            return _mapControl;
        }

        /// <summary>
        /// Ensures that the provided action runs on the UI thread
        /// </summary>
        private void InvokeOnUIThread(Action action)
        {
            if (_parentForm.InvokeRequired)
            {
                try
                {
                    _parentForm.Invoke(action);
                }
                catch (ObjectDisposedException)
                {
                    // Form may have been closed
                }
                catch (InvalidOperationException)
                {
                    // Handle case where form handle isn't created yet
                }
            }
            else
            {
                action();
            }
        }
    }
}
