using GMap.NET.WindowsForms;
using RideMatchProject.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using RideMatchProject.Utilities;

namespace RideMatchProject.PassengerClasses
{
    /// <summary>
    /// Manages the user interface components for the passenger dashboard in the RideMatch application.
    /// Provides functionality for displaying passenger details, map controls, and location settings.
    /// </summary>
    public class PassengerUIManager
    {
        private readonly Form _parentForm; // The main form hosting the UI components
        private readonly string _username; // Username of the logged-in passenger

        private GMapControl _mapControl; // Map control for displaying and interacting with maps
        private CheckBox _availabilityCheckBox; // Checkbox for toggling ride availability
        private RichTextBox _detailsTextBox; // Text box for displaying passenger and ride details
        private Button _refreshButton; // Button to refresh ride information
        private Button _logoutButton; // Button to log out and close the form
        private Button _setLocationButton; // Button to set pickup location on map
        private TextBox _addressTextBox; // Text box for entering address to search
        private Button _searchButton; // Button to initiate address search
        private Label _instructionsLabel; // Label for location selection instructions
        private Panel _leftPanel; // Panel containing UI controls on the left side

        // Tag to identify location section controls
        private const string TAG_LOCATION_SECTION = "location_section";

        // Events for handling UI interactions
        public event EventHandler<bool> AvailabilityChanged; // Triggered when availability changes
        public event EventHandler RefreshRequested; // Triggered when refresh is requested
        public event EventHandler SetLocationRequested; // Triggered when setting location on map
        public event EventHandler<string> AddressSearchRequested; // Triggered when searching address

        /// <summary>
        /// Initializes a new instance of the PassengerUIManager class.
        /// </summary>
        /// <param name="parentForm">The parent form that hosts the UI components.</param>
        /// <param name="username">The username of the logged-in passenger.</param>
        public PassengerUIManager(Form parentForm, string username)
        {
            _parentForm = parentForm ?? throw new ArgumentNullException(nameof(parentForm));
            _username = username ?? throw new ArgumentNullException(nameof(username));

            InitializeUI();
        }

        /// <summary>
        /// Sets up the entire user interface by creating and arranging all UI components.
        /// </summary>
        public void InitializeUI()
        {
            CreateTitleLabel();
            CreateLeftPanel();
            CreateMapControl();
            CreateDetailsSection();
            CreateAvailabilitySection();

            if (_availabilityCheckBox.Checked)
            {
                CreateLocationSection();
            }

            CreateActionButtons();
        }

        /// <summary>
        /// Creates a welcome label displaying the username at the top of the form.
        /// </summary>
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

        /// <summary>
        /// Creates a panel on the left side to hold UI controls like details and buttons.
        /// </summary>
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

        /// <summary>
        /// Creates and configures the map control for displaying geographical data.
        /// </summary>
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

        /// <summary>
        /// Creates the section for displaying passenger and ride details.
        /// </summary>
        private void CreateDetailsSection()
        {
            var detailsLabel = new Label
            {
                Text = "Your Ride Details:",
                Location = new Point(20, 20),
                Size = new Size(200, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            _leftPanel.Controls.Add(detailsLabel);

            _detailsTextBox = new RichTextBox
            {
                Location = new Point(20, 50),
                Size = new Size(310, 200),
                ReadOnly = true
            };
            _leftPanel.Controls.Add(_detailsTextBox);

            var divider = new Panel
            {
                Location = new Point(20, 260),
                Size = new Size(310, 2),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Gray
            };
            _leftPanel.Controls.Add(divider);
        }

        /// <summary>
        /// Creates the section for toggling ride availability for the next day.
        /// </summary>
        private void CreateAvailabilitySection()
        {
            var statusLabel = new Label
            {
                Text = "Tomorrow's Status:",
                Location = new Point(20, 270),
                Size = new Size(150, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            _leftPanel.Controls.Add(statusLabel);

            _availabilityCheckBox = new CheckBox
            {
                Text = "I need a ride tomorrow",
                Location = new Point(20, 300),
                Size = new Size(300, 30),
                Checked = true
            };

            _availabilityCheckBox.CheckedChanged += AvailabilityCheckBox_CheckedChanged;
            _leftPanel.Controls.Add(_availabilityCheckBox);

            var divider = new Panel
            {
                Location = new Point(20, 340),
                Size = new Size(310, 2),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Gray
            };
            _leftPanel.Controls.Add(divider);
        }

        /// <summary>
        /// Handles changes to the availability checkbox, updating UI and triggering events.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event data.</param>
        private void AvailabilityCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            InvokeOnUIThread(() => {
                if (_availabilityCheckBox.Checked)
                {
                    if (!HasLocationSection())
                    {
                        CreateLocationSection();
                    }
                }
                else
                {
                    RemoveLocationSection();
                }
            });

            AvailabilityChanged?.Invoke(this, _availabilityCheckBox.Checked);
        }

        /// <summary>
        /// Creates the section for setting the passenger's pickup location.
        /// </summary>
        private void CreateLocationSection()
        {
            if (HasLocationSection())
                return;

            var locationLabel = new Label
            {
                Text = "Set Your Pickup Location:",
                Location = new Point(20, 350),
                Size = new Size(200, 20),
                Font = new Font("Arial", 10, FontStyle.Bold),
                Tag = TAG_LOCATION_SECTION
            };
            _leftPanel.Controls.Add(locationLabel);

            _setLocationButton = new Button
            {
                Text = "Set Location on Map",
                Location = new Point(20, 380),
                Size = new Size(150, 30),
                Tag = TAG_LOCATION_SECTION
            };
            _setLocationButton.Click += (s, e) => SetLocationRequested?.Invoke(this, EventArgs.Empty);
            _leftPanel.Controls.Add(_setLocationButton);

            var searchLabel = new Label
            {
                Text = "Or Search Address:",
                Location = new Point(20, 420),
                Size = new Size(150, 20),
                Tag = TAG_LOCATION_SECTION
            };
            _leftPanel.Controls.Add(searchLabel);

            _addressTextBox = new TextBox
            {
                Location = new Point(20, 445),
                Size = new Size(220, 25),
                Tag = TAG_LOCATION_SECTION
            };
            _leftPanel.Controls.Add(_addressTextBox);

            _searchButton = new Button
            {
                Text = "Search",
                Location = new Point(250, 445),
                Size = new Size(80, 25),
                Tag = TAG_LOCATION_SECTION
            };
            _searchButton.Click += (s, e) => AddressSearchRequested?.Invoke(this, _addressTextBox.Text);
            _leftPanel.Controls.Add(_searchButton);

            _instructionsLabel = new Label
            {
                Text = "Click on the map to set your pickup location",
                Location = new Point(20, 480),
                Size = new Size(310, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Red,
                Visible = false,
                Tag = TAG_LOCATION_SECTION
            };
            _leftPanel.Controls.Add(_instructionsLabel);
        }

        /// <summary>
        /// Checks if the location section already exists in the UI.
        /// </summary>
        /// <returns>True if the location section exists, false otherwise.</returns>
        private bool HasLocationSection()
        {
            foreach (Control control in _leftPanel.Controls)
            {
                if (control.Tag != null && control.Tag.ToString() == TAG_LOCATION_SECTION)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Removes the location section controls from the UI.
        /// </summary>
        private void RemoveLocationSection()
        {
            var controlsToRemove = new List<Control>();

            foreach (Control control in _leftPanel.Controls)
            {
                if (control.Tag != null && control.Tag.ToString() == TAG_LOCATION_SECTION)
                {
                    controlsToRemove.Add(control);
                }
            }

            foreach (Control control in controlsToRemove)
            {
                _leftPanel.Controls.Remove(control);
                control.Dispose();
            }

            _setLocationButton = null;
            _addressTextBox = null;
            _searchButton = null;
            _instructionsLabel = null;
        }

        /// <summary>
        /// Creates action buttons for refreshing data and logging out.
        /// </summary>
        private void CreateActionButtons()
        {
            _refreshButton = new Button
            {
                Text = "Refresh",
                Location = new Point(20, 530),
                Size = new Size(150, 30)
            };
            _refreshButton.Click += (s, e) => RefreshRequested?.Invoke(this, EventArgs.Empty);
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

        /// <summary>
        /// Displays a loading message in the details text box and disables the refresh button.
        /// </summary>
        /// <param name="message">The message to display.</param>
        public void ShowLoadingMessage(string message)
        {
            InvokeOnUIThread(() => {
                _detailsTextBox.Clear();
                _detailsTextBox.AppendText(message);
                _refreshButton.Enabled = false;
            });
        }

        /// <summary>
        /// Displays passenger and ride details in the details text box.
        /// </summary>
        /// <param name="passenger">The passenger object containing user details.</param>
        /// <param name="vehicle">The vehicle object containing ride details, if available.</param>
        /// <param name="pickupTime">The scheduled pickup time, if available.</param>
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

        /// <summary>
        /// Appends passenger information to the details text box.
        /// </summary>
        /// <param name="passenger">The passenger object containing user details.</param>
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

        /// <summary>
        /// Appends vehicle and ride information to the details text box.
        /// </summary>
        /// <param name="vehicle">The vehicle object containing ride details.</param>
        /// <param name="passenger">The passenger object for context.</param>
        /// <param name="pickupTime">The scheduled pickup time, if available.</param>
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

            if (vehicle.TotalTime > 0)
            {
                _detailsTextBox.AppendText($"Estimated Trip Time: {TimeFormatter.FormatMinutesWithUnits(vehicle.TotalTime)}\n");
            }

            if (!string.IsNullOrEmpty(vehicle.StartAddress))
            {
                _detailsTextBox.AppendText($"Driver Starting From: {vehicle.StartAddress}\n");
            }
        }

        /// <summary>
        /// Appends pickup time information to the details text box.
        /// </summary>
        /// <param name="pickupTime">The scheduled pickup time, if available.</param>
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

        /// <summary>
        /// Appends a message indicating no ride is scheduled.
        /// </summary>
        private void AppendNoVehicleMessage()
        {
            _detailsTextBox.SelectionFont = new Font(_detailsTextBox.Font, FontStyle.Bold);
            _detailsTextBox.AppendText("No Ride Scheduled Yet\n");
            _detailsTextBox.SelectionFont = _detailsTextBox.Font;
            _detailsTextBox.AppendText("Rides for tomorrow will be assigned by the system overnight.\n");
            _detailsTextBox.AppendText("Please check back tomorrow morning for your ride details.\n");
        }

        /// <summary>
        /// Displays a message indicating no passenger profile exists.
        /// </summary>
        public void ShowNoProfileMessage()
        {
            InvokeOnUIThread(() => {
                _detailsTextBox.Clear();
                _detailsTextBox.AppendText("No passenger profile found. Set your location to create a profile.\n");
                _refreshButton.Enabled = true;
            });
        }

        /// <summary>
        /// Displays an error message in a message box.
        /// </summary>
        /// <param name="message">The error message to display.</param>
        public void ShowErrorMessage(string message)
        {
            InvokeOnUIThread(() => {
                MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            });
        }

        /// <summary>
        /// Displays a message confirming the pickup location update.
        /// </summary>
        /// <param name="address">The updated address.</param>
        public void ShowLocationUpdatedMessage(string address)
        {
            InvokeOnUIThread(() => {
                MessageBox.Show($"Your pickup location has been set to:\n{address}",
                    "Location Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        /// <summary>
        /// Displays a message confirming the ride request submission.
        /// </summary>
        public void ShowRideRequestMessage()
        {
            InvokeOnUIThread(() => {
                MessageBox.Show("Your ride request has been submitted. A driver will be assigned soon.",
                    "Status Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        /// <summary>
        /// Updates the availability checkbox and related UI elements.
        /// </summary>
        /// <param name="isAvailable">The new availability state.</param>
        public void UpdateAvailabilityControl(bool isAvailable)
        {
            InvokeOnUIThread(() => {
                _availabilityCheckBox.CheckedChanged -= AvailabilityCheckBox_CheckedChanged;

                _availabilityCheckBox.Checked = isAvailable;

                if (isAvailable)
                {
                    if (!HasLocationSection())
                    {
                        CreateLocationSection();
                    }
                }
                else
                {
                    RemoveLocationSection();
                }

                _availabilityCheckBox.CheckedChanged += AvailabilityCheckBox_CheckedChanged;
            });
        }

        /// <summary>
        /// Shows or hides the location selection instructions label.
        /// </summary>
        /// <param name="visible">Whether the instructions should be visible.</param>
        public void ShowLocationSelectionInstructions(bool visible)
        {
            InvokeOnUIThread(() => {
                if (_instructionsLabel != null)
                {
                    _instructionsLabel.Visible = visible;
                }
            });
        }

        /// <summary>
        /// Enables or disables the address search controls.
        /// </summary>
        /// <param name="enabled">Whether the controls should be enabled.</param>
        public void SetSearchControlsEnabled(bool enabled)
        {
            InvokeOnUIThread(() => {
                if (_addressTextBox != null)
                {
                    _addressTextBox.Enabled = enabled;
                }

                if (_searchButton != null)
                {
                    _searchButton.Enabled = enabled;
                }

                _parentForm.Cursor = enabled ? Cursors.Default : Cursors.WaitCursor;
            });
        }

        /// <summary>
        /// Sets the form's cursor to indicate a busy or normal state.
        /// </summary>
        /// <param name="busy">Whether the busy state should be shown.</param>
        public void ShowBusyState(bool busy)
        {
            InvokeOnUIThread(() => {
                _parentForm.Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            });
        }

        /// <summary>
        /// Gets the map control instance.
        /// </summary>
        /// <returns>The GMapControl instance.</returns>
        public GMapControl GetMapControl()
        {
            return _mapControl;
        }

        /// <summary>
        /// Ensures that the provided action runs on the UI thread.
        /// </summary>
        /// <param name="action">The action to execute.</param>
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