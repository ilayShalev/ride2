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
using RideMatchProject.Utilities;

namespace RideMatchProject.DriverClasses
{
    /// <summary>
    /// Manages UI components and interactions with proper thread safety
    /// </summary>
    public class DriverUIManager
    {
        private readonly Form _parentForm;
        private readonly DriverDataManager _dataManager;
        private readonly DriverMapManager _mapManager;
        private readonly DriverLocationManager _locationManager;
        private readonly string _username;

        // UI Controls
        private Panel _leftPanel;
        private CheckBox _availabilityCheckBox;
        private RichTextBox _routeDetailsTextBox;
        private Button _refreshButton;
        private Button _logoutButton;
        private NumericUpDown _capacityNumericUpDown;
        private Button _updateCapacityButton;
        private Label _locationInstructionsLabel;
        private Button _setLocationButton;
        private TextBox _addressTextBox;
        private Button _searchButton;

        // Control tag identifiers for section management
        private const string TAG_CAPACITY_SECTION = "capacity_section";
        private const string TAG_LOCATION_SECTION = "location_section";

        // State tracking
        private bool _isDataLoaded = false;
        private readonly object _uiLock = new object();
        private bool _isRefreshing = false;

        public GMapControl MapControl { get; private set; }

        public DriverUIManager(Form parentForm, DriverDataManager dataManager,
            DriverMapManager mapManager, DriverLocationManager locationManager, string username)
        {
            _parentForm = parentForm;
            _dataManager = dataManager;
            _mapManager = mapManager;
            _locationManager = locationManager;
            _username = username;
        }

        public void InitializeUI()
        {
            // Execute all UI creation on the UI thread
            ThreadUtils.ExecuteOnUIThread(_parentForm, () => {
                SetFormProperties();
                CreateTitleLabel();
                CreateLeftPanel();
                CreateMapControl();

                _locationManager.SetMapControl(MapControl);

                // Show initial loading message
                UpdateLoadingStatus("Initializing driver interface...");
            });

            // Start data loading process with proper progress reporting
            LoadDriverDataWithProgress();
        }

        // New method for better data loading with progress reporting
        private async void LoadDriverDataWithProgress()
        {
            try
            {
                UpdateLoadingStatus("Connecting to server...");
                await Task.Delay(300); // Small delay to show status message

                UpdateLoadingStatus("Loading driver data...");

                // Fix: Don't expect a boolean return
                await _dataManager.LoadDriverDataAsync();

                // Process the loaded data - assume success if we got here
                ThreadUtils.ExecuteOnUIThread(_parentForm, () => {
                    _isDataLoaded = true;

                    // Update UI based on loaded data
                    UpdateUIBasedOnAvailability();

                    // Update map with vehicle and passenger data
                    _mapManager.DisplayRouteOnMap(_dataManager.Vehicle, _dataManager.AssignedPassengers);
                });
            }
            catch (Exception ex)
            {
                UpdateLoadingStatus($"⚠️ Error loading data: {ex.Message}");
            }
        }

        // Helper method to update loading status safely
        private void UpdateLoadingStatus(string message)
        {
            ThreadUtils.ExecuteOnUIThread(_routeDetailsTextBox, () => {
                _routeDetailsTextBox.Clear();
                _routeDetailsTextBox.AppendText($"{message}\n");
            });
        }

        private void SetFormProperties()
        {
            _parentForm.Text = "RideMatch - Driver Interface";
            _parentForm.Size = new Size(1000, 700);
            _parentForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            _parentForm.StartPosition = FormStartPosition.CenterScreen;
        }

        private void CreateTitleLabel()
        {
            var titleLabel = new Label
            {
                Text = $"Welcome, {_username}",
                Location = new Point(20, 20),
                Size = new Size(960, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 16, FontStyle.Bold)
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

            CreateRouteDetailsSection();
            CreateAvailabilitySection();
            CreateActionButtons();
            // Sections will be added by UpdateUIBasedOnAvailability
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
                DragButton = MouseButtons.Left,
                ShowCenter = false
            };

            _parentForm.Controls.Add(MapControl);
        }

        private void CreateRouteDetailsSection()
        {
            var routeLabel = new Label
            {
                Text = "Your Route Details:",
                Location = new Point(20, 20),
                Size = new Size(200, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            _leftPanel.Controls.Add(routeLabel);

            _routeDetailsTextBox = new RichTextBox
            {
                Location = new Point(20, 50),
                Size = new Size(310, 160),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle
            };

            _leftPanel.Controls.Add(_routeDetailsTextBox);

            // Initial message shows waiting state
            ThreadUtils.ExecuteOnUIThread(_routeDetailsTextBox, () => {
                _routeDetailsTextBox.AppendText("Initializing...\n");
            });
        }

        private void CreateAvailabilitySection()
        {
            var availabilityLabel = new Label
            {
                Text = "Tomorrow's Status:",
                Location = new Point(20, 230),
                Size = new Size(200, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            _leftPanel.Controls.Add(availabilityLabel);

            _availabilityCheckBox = new CheckBox
            {
                Text = "I am available to drive tomorrow",
                Location = new Point(20, 260),
                Size = new Size(300, 30),
                Checked = true // Default state
            };

            _availabilityCheckBox.CheckedChanged += AvailabilityCheckBox_CheckedChanged;
            _leftPanel.Controls.Add(_availabilityCheckBox);
        }

        private void CreateCapacitySection()
        {
            // First check if section already exists (avoid duplicates)
            if (HasSectionWithTag(TAG_CAPACITY_SECTION))
                return;

            var capacityLabel = new Label
            {
                Text = "Vehicle Capacity:",
                Location = new Point(20, 300),
                Size = new Size(150, 20),
                Font = new Font("Arial", 10, FontStyle.Bold),
                Tag = TAG_CAPACITY_SECTION
            };
            _leftPanel.Controls.Add(capacityLabel);

            var seatsLabel = new Label
            {
                Text = "Number of seats:",
                Location = new Point(20, 330),
                Size = new Size(150, 20),
                Tag = TAG_CAPACITY_SECTION
            };
            _leftPanel.Controls.Add(seatsLabel);

            _capacityNumericUpDown = new NumericUpDown
            {
                Location = new Point(180, 330),
                Size = new Size(80, 25),
                Minimum = 1,
                Maximum = 20,
                Value = 4,
                Tag = TAG_CAPACITY_SECTION
            };
            _leftPanel.Controls.Add(_capacityNumericUpDown);

            _updateCapacityButton = new Button
            {
                Text = "Update Capacity",
                Location = new Point(180, 360),
                Size = new Size(150, 30),
                Tag = TAG_CAPACITY_SECTION
            };
            _updateCapacityButton.Click += UpdateCapacityButton_Click;
            _leftPanel.Controls.Add(_updateCapacityButton);

            AddSeparator(250, TAG_CAPACITY_SECTION);
        }

        private void CreateLocationSettingSection()
        {
            // First check if section already exists (avoid duplicates)
            if (HasSectionWithTag(TAG_LOCATION_SECTION))
                return;

            var locationLabel = new Label
            {
                Text = "Set Your Location:",
                Location = new Point(20, 420),
                Size = new Size(150, 20),
                Font = new Font("Arial", 10, FontStyle.Bold),
                Tag = TAG_LOCATION_SECTION
            };
            _leftPanel.Controls.Add(locationLabel);

            _setLocationButton = new Button
            {
                Text = "Set Location on Map",
                Location = new Point(20, 445),
                Size = new Size(150, 30),
                Tag = TAG_LOCATION_SECTION
            };
            _setLocationButton.Click += SetLocationButton_Click;
            _leftPanel.Controls.Add(_setLocationButton);

            var addressLabel = new Label
            {
                Text = "Or Search Address:",
                Location = new Point(20, 478),
                Size = new Size(150, 20),
                Tag = TAG_LOCATION_SECTION
            };
            _leftPanel.Controls.Add(addressLabel);

            _addressTextBox = new TextBox
            {
                Location = new Point(20, 500),
                Size = new Size(220, 25),
                Tag = TAG_LOCATION_SECTION
            };
            _leftPanel.Controls.Add(_addressTextBox);

            _searchButton = new Button
            {
                Text = "Search",
                Location = new Point(250, 500),
                Size = new Size(80, 25),
                Tag = TAG_LOCATION_SECTION
            };
            _searchButton.Click += SearchButton_Click;
            _leftPanel.Controls.Add(_searchButton);

            _locationInstructionsLabel = new Label
            {
                Text = "Click on the map to set your starting location",
                Location = new Point(20, 550),
                Size = new Size(310, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Red,
                Visible = false,
                Tag = TAG_LOCATION_SECTION
            };
            _leftPanel.Controls.Add(_locationInstructionsLabel);

            if (_locationManager != null)
            {
                _locationManager.SetInstructionLabel(_locationInstructionsLabel);
            }
        }

        private void CreateActionButtons()
        {
            _refreshButton = new Button
            {
                Text = "Refresh",
                Location = new Point(20, 530),
                Size = new Size(150, 30)
            };

            _refreshButton.Click += RefreshButton_Click;
            _leftPanel.Controls.Add(_refreshButton);

            _logoutButton = new Button
            {
                Text = "Logout",
                Location = new Point(180, 530),
                Size = new Size(150, 30)
            };

            _logoutButton.Click += (s, e) => _parentForm.Close();
            _leftPanel.Controls.Add(_logoutButton);
        }

        private void AddSeparator(int yPosition, string tag)
        {
            var separatorPanel = new Panel
            {
                Location = new Point(20, yPosition),
                Size = new Size(310, 2),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Gray,
                Tag = tag
            };
            _leftPanel.Controls.Add(separatorPanel);
        }

        // Helper method to check if a section exists by tag
        private bool HasSectionWithTag(string tag)
        {
            foreach (Control control in _leftPanel.Controls)
            {
                if (control.Tag != null && control.Tag.ToString() == tag)
                {
                    return true;
                }
            }
            return false;
        }

        // Removes all controls with specified tag
        private void RemoveSectionByTag(string tag)
        {
            var controlsToRemove = new List<Control>();

            foreach (Control control in _leftPanel.Controls)
            {
                if (control.Tag != null && control.Tag.ToString() == tag)
                {
                    controlsToRemove.Add(control);
                }
            }

            foreach (Control control in controlsToRemove)
            {
                _leftPanel.Controls.Remove(control);
                control.Dispose();
            }

            // Reset appropriate references based on the tag
            if (tag == TAG_CAPACITY_SECTION)
            {
                _capacityNumericUpDown = null;
                _updateCapacityButton = null;
            }
            else if (tag == TAG_LOCATION_SECTION)
            {
                _setLocationButton = null;
                _addressTextBox = null;
                _searchButton = null;
                _locationInstructionsLabel = null;
            }
        }

        // Main method to update UI based on availability state
        private void UpdateUIBasedOnAvailability()
        {
            ThreadUtils.ExecuteOnUIThread(_leftPanel, () => {
                bool isAvailable = false;

                // Get availability from data manager if vehicle exists
                if (_dataManager.Vehicle != null)
                {
                    isAvailable = _dataManager.Vehicle.IsAvailableTomorrow;

                    // Update checkbox without triggering event
                    _availabilityCheckBox.CheckedChanged -= AvailabilityCheckBox_CheckedChanged;
                    _availabilityCheckBox.Checked = isAvailable;
                    _availabilityCheckBox.CheckedChanged += AvailabilityCheckBox_CheckedChanged;

                    // Set capacity value if control exists
                    if (_capacityNumericUpDown != null)
                    {
                        _capacityNumericUpDown.Value = _dataManager.Vehicle.Capacity;
                    }
                }
                else
                {
                    // Use checkbox's current state if no vehicle data
                    isAvailable = _availabilityCheckBox.Checked;
                }

                // Always ensure sections match the availability state
                if (isAvailable)
                {
                    CreateCapacitySection();
                    CreateLocationSettingSection();
                }
                else
                {
                    RemoveSectionByTag(TAG_CAPACITY_SECTION);
                    RemoveSectionByTag(TAG_LOCATION_SECTION);
                }

                // Update location manager
                if (_locationInstructionsLabel != null)
                {
                    _locationManager.SetInstructionLabel(_locationInstructionsLabel);
                }

                UpdateRouteDetailsText();
            });
        }

        public void RefreshUI()
        {
            UpdateUIBasedOnAvailability();
        }

        // Update the route details text with better error handling
        private void UpdateRouteDetailsText()
        {
            ThreadUtils.ExecuteOnUIThread(_routeDetailsTextBox, () => {
                _routeDetailsTextBox.Clear();

                if (!_isDataLoaded)
                {
                    _routeDetailsTextBox.AppendText("Waiting for data to load...\n");
                    return;
                }

                if (_dataManager.Vehicle == null)
                {
                    _routeDetailsTextBox.AppendText("No vehicle assigned.\nPlease contact support if you believe this is an error.\n");
                    return;
                }

                try
                {
                    DisplayVehicleDetails();
                    DisplayPassengerDetails();
                }
                catch (Exception ex)
                {
                    _routeDetailsTextBox.Clear();
                    _routeDetailsTextBox.AppendText($"Error displaying route details: {ex.Message}\n");
                    _routeDetailsTextBox.AppendText("Please try refreshing the data.\n");
                }
            });
        }

        private void DisplayVehicleDetails()
        {
            ThreadUtils.ExecuteOnUIThread(_routeDetailsTextBox, () => {
                _routeDetailsTextBox.SelectionFont = new Font(_routeDetailsTextBox.Font, FontStyle.Bold);
                _routeDetailsTextBox.AppendText("Your Vehicle Details:\n");
                _routeDetailsTextBox.SelectionFont = _routeDetailsTextBox.Font;
                _routeDetailsTextBox.AppendText($"Capacity: {_dataManager.Vehicle.Capacity} seats\n");

                DisplayLocationInfo();

                if (!string.IsNullOrEmpty(_dataManager.Vehicle.DepartureTime))
                {
                    _routeDetailsTextBox.SelectionFont = new Font(_routeDetailsTextBox.Font, FontStyle.Bold);
                    _routeDetailsTextBox.AppendText($"\nDeparture Time: {_dataManager.Vehicle.DepartureTime}\n");
                    _routeDetailsTextBox.SelectionFont = _routeDetailsTextBox.Font;
                }

                // Add total route time if available
                if (_dataManager.Vehicle.TotalTime > 0)
                {
                    _routeDetailsTextBox.SelectionFont = new Font(_routeDetailsTextBox.Font, FontStyle.Bold);
                    _routeDetailsTextBox.AppendText($"Total Route Time: {TimeFormatter.FormatMinutesWithUnits(_dataManager.Vehicle.TotalTime)}\n");
                    _routeDetailsTextBox.SelectionFont = _routeDetailsTextBox.Font;
                }
            });
        }

        private void DisplayLocationInfo()
        {
            ThreadUtils.ExecuteOnUIThread(_routeDetailsTextBox, () => {
                var vehicle = _dataManager.Vehicle;

                if (vehicle.StartLatitude == 0 && vehicle.StartLongitude == 0)
                {
                    _routeDetailsTextBox.AppendText("Starting Location: Not set\n\n");
                    _routeDetailsTextBox.AppendText("Please set your starting location using the options below.\n");
                    return;
                }

                if (!string.IsNullOrEmpty(vehicle.StartAddress))
                {
                    _routeDetailsTextBox.AppendText($"Starting Location: {vehicle.StartAddress}\n");
                    return;
                }

                _routeDetailsTextBox.AppendText(
                    $"Starting Location: ({vehicle.StartLatitude:F6}, {vehicle.StartLongitude:F6})\n");
            });
        }

        private void DisplayPassengerDetails()
        {
            ThreadUtils.ExecuteOnUIThread(_routeDetailsTextBox, () => {
                var passengers = _dataManager.AssignedPassengers;

                if (passengers == null || passengers.Count == 0)
                {
                    _routeDetailsTextBox.AppendText("\nNo passengers assigned for today's route.\n");
                    return;
                }

                _routeDetailsTextBox.SelectionFont = new Font(_routeDetailsTextBox.Font, FontStyle.Bold);
                _routeDetailsTextBox.AppendText("Assigned Passengers:\n");
                _routeDetailsTextBox.SelectionFont = _routeDetailsTextBox.Font;

                for (int i = 0; i < passengers.Count; i++)
                {
                    var passenger = passengers[i];
                    if (passenger == null) continue;

                    _routeDetailsTextBox.AppendText($"{i + 1}. {passenger.Name}\n");
                    DisplayPassengerAddress(passenger);
                    DisplayPickupTime(passenger, i);
                    _routeDetailsTextBox.AppendText("\n");
                }
            });
        }

        private void DisplayPassengerAddress(Passenger passenger)
        {
            ThreadUtils.ExecuteOnUIThread(_routeDetailsTextBox, () => {
                if (!string.IsNullOrEmpty(passenger.Address))
                {
                    _routeDetailsTextBox.AppendText($"   Pick-up: {passenger.Address}\n");
                    return;
                }

                _routeDetailsTextBox.AppendText(
                    $"   Pick-up: ({passenger.Latitude:F6}, {passenger.Longitude:F6})\n");
            });
        }

        private void DisplayPickupTime(Passenger passenger, int index)
        {
            ThreadUtils.ExecuteOnUIThread(_routeDetailsTextBox, () => {
                _routeDetailsTextBox.SelectionFont = new Font(_routeDetailsTextBox.Font, FontStyle.Bold);

                if (!string.IsNullOrEmpty(passenger.EstimatedPickupTime))
                {
                    _routeDetailsTextBox.AppendText($"   Pick-up Time: {passenger.EstimatedPickupTime}\n");
                }
                else if (index == 0 && _dataManager.PickupTime.HasValue)
                {
                    _routeDetailsTextBox.AppendText(
                        $"   Pick-up Time: {_dataManager.PickupTime.Value.ToString("HH:mm")}\n");
                }

                _routeDetailsTextBox.SelectionFont = _routeDetailsTextBox.Font;
            });
        }

        private async void AvailabilityCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                // Disable control during processing
                ThreadUtils.UpdateControlEnabled(_availabilityCheckBox, false);

                // Immediately update UI based on new state
                if (_availabilityCheckBox.Checked)
                {
                    ThreadUtils.ExecuteOnUIThread(_leftPanel, () => {
                        CreateCapacitySection();
                        CreateLocationSettingSection();

                        // Update location manager
                        if (_locationInstructionsLabel != null)
                        {
                            _locationManager.SetInstructionLabel(_locationInstructionsLabel);
                        }
                    });
                }
                else
                {
                    ThreadUtils.ExecuteOnUIThread(_leftPanel, () => {
                        RemoveSectionByTag(TAG_CAPACITY_SECTION);
                        RemoveSectionByTag(TAG_LOCATION_SECTION);
                    });
                }

                // Update database
                bool success = await _dataManager.UpdateVehicleAvailabilityAsync(_availabilityCheckBox.Checked);

                if (success)
                {
                    string message = _availabilityCheckBox.Checked ?
                        "You are now marked as available to drive tomorrow." :
                        "You are now marked as unavailable to drive tomorrow.";

                    ThreadUtils.ShowInfoMessage(_parentForm, message, "Availability Updated");
                }
                else
                {
                    ThreadUtils.ShowErrorMessage(_parentForm,
                        "Failed to update availability. Please try again.",
                        "Update Failed");

                    // Revert checkbox and UI
                    RevertAvailabilityCheckbox();
                }
            }
            catch (Exception ex)
            {
                ThreadUtils.ShowErrorMessage(_parentForm,
                    $"Error updating availability: {ex.Message}",
                    "Error");

                // Revert checkbox and UI
                RevertAvailabilityCheckbox();
            }
            finally
            {
                // Re-enable control
                ThreadUtils.UpdateControlEnabled(_availabilityCheckBox, true);
            }
        }

        private void RevertAvailabilityCheckbox()
        {
            bool originalState = _dataManager.Vehicle?.IsAvailableTomorrow ?? true;

            ThreadUtils.ExecuteOnUIThread(_availabilityCheckBox, () => {
                _availabilityCheckBox.CheckedChanged -= AvailabilityCheckBox_CheckedChanged;
                _availabilityCheckBox.Checked = originalState;
                _availabilityCheckBox.CheckedChanged += AvailabilityCheckBox_CheckedChanged;

                // Update UI to match reverted state
                UpdateUIBasedOnAvailability();
            });
        }

        private async void UpdateCapacityButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Disable button to prevent multiple clicks during processing
                ThreadUtils.UpdateControlEnabled(_updateCapacityButton, false);

                // Get capacity value safely from UI thread
                int newCapacity = (int)ThreadUtils.ExecuteOnUIThread(_capacityNumericUpDown,
                    () => _capacityNumericUpDown.Value);

                // Update capacity in the database
                bool success = await _dataManager.UpdateVehicleCapacityAsync(newCapacity);

                if (success)
                {
                    ThreadUtils.ShowInfoMessage(_parentForm,
                        $"Vehicle capacity updated to {newCapacity} seats.",
                        "Capacity Updated");

                    UpdateRouteDetailsText();
                }
                else
                {
                    ThreadUtils.ShowErrorMessage(_parentForm,
                        "Failed to update vehicle capacity. Please try again.",
                        "Update Failed");

                    // Reset capacity control
                    ThreadUtils.ExecuteOnUIThread(_capacityNumericUpDown, () => {
                        _capacityNumericUpDown.Value = _dataManager.Vehicle?.Capacity ?? 4;
                    });
                }
            }
            catch (Exception ex)
            {
                ThreadUtils.ShowErrorMessage(_parentForm,
                    $"Error updating vehicle capacity: {ex.Message}",
                    "Error");

                // Reset capacity control
                ThreadUtils.ExecuteOnUIThread(_capacityNumericUpDown, () => {
                    _capacityNumericUpDown.Value = _dataManager.Vehicle?.Capacity ?? 4;
                });
            }
            finally
            {
                // Re-enable button after processing
                ThreadUtils.UpdateControlEnabled(_updateCapacityButton, true);
            }
        }

        private void SetLocationButton_Click(object sender, EventArgs e)
        {
            _locationManager.EnableLocationSelection();
        }

        private async void SearchButton_Click(object sender, EventArgs e)
        {
            // Get address text safely from UI thread
            string addressText = ThreadUtils.ExecuteOnUIThread(_addressTextBox,
                () => _addressTextBox.Text);

            if (string.IsNullOrWhiteSpace(addressText)) return;

            // Disable search controls during processing
            ThreadUtils.UpdateControlEnabled(_searchButton, false);
            ThreadUtils.UpdateControlEnabled(_addressTextBox, false);

            try
            {
                await _locationManager.SearchAddressAsync(addressText);
            }
            finally
            {
                // Re-enable search controls after processing
                ThreadUtils.UpdateControlEnabled(_searchButton, true);
                ThreadUtils.UpdateControlEnabled(_addressTextBox, true);
            }
        }

        private async void RefreshButton_Click(object sender, EventArgs e)
        {
            // Use lock to prevent multiple concurrent refreshes
            lock (_uiLock)
            {
                if (_isRefreshing)
                {
                    return;
                }
                _isRefreshing = true;
            }

            try
            {
                // Disable refresh button during processing
                ThreadUtils.UpdateControlEnabled(_refreshButton, false);

                // Show refresh status
                UpdateLoadingStatus("Refreshing data...");

                // Fix: Handle task without boolean return
                Task loadTask = _dataManager.LoadDriverDataAsync();

                // Add a timeout for the load operation
                bool completed = await Task.WhenAny(loadTask, Task.Delay(15000)) == loadTask;

                if (!completed)
                {
                    UpdateLoadingStatus("⚠️ Refresh timed out. Please try again later.");
                    return;
                }

                // If we got here, assume success
                _isDataLoaded = true;

                // Update UI with loaded data
                UpdateUIBasedOnAvailability();

                // Update map with vehicle and passenger data
                _mapManager.DisplayRouteOnMap(_dataManager.Vehicle, _dataManager.AssignedPassengers);

                UpdateLoadingStatus("✓ Data refreshed successfully!");
                await Task.Delay(1000); // Show success message briefly
                UpdateRouteDetailsText(); // Then show normal route details
            }
            catch (Exception ex)
            {
                // Show error in route details text box
                UpdateLoadingStatus($"⚠️ Error refreshing data: {ex.Message}");
            }
            finally
            {
                // Re-enable refresh button after processing
                ThreadUtils.UpdateControlEnabled(_refreshButton, true);

                lock (_uiLock)
                {
                    _isRefreshing = false;
                }
            }
        }
    }
}