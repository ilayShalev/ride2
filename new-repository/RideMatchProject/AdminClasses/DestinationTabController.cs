using GMap.NET.WindowsForms;
using RideMatchProject.Services;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.AdminClasses
{
    /// <summary>
    /// Controls the UI and logic for managing the destination tab in the admin panel.
    /// </summary>
    public class DestinationTabController : TabControllerBase
    {
        // UI Elements
        private TextBox _nameTextBox;
        private TextBox _timeTextBox;
        private TextBox _latTextBox;
        private TextBox _lngTextBox;
        private TextBox _addressTextBox;
        private Button _updateTimeButton;
        private Button _searchButton;
        private Button _saveButton;
        private GMapControl _mapControl;

        /// <summary>
        /// Constructor that receives the required services for database and maps.
        /// </summary>
        public DestinationTabController(DatabaseService dbService, MapService mapService)
            : base(dbService, mapService) { }

        /// <summary>
        /// Called when the tab is first created. Initializes the tab UI.
        /// </summary>
        public override void InitializeTab(TabPage tabPage)
        {
            CreateDestinationPanel(tabPage);
        }

        /// <summary>
        /// Builds and adds the destination management panel to the tab.
        /// </summary>
        private void CreateDestinationPanel(TabPage tabPage)
        {
            var panel = AdminUIFactory.CreatePanel(new Point(10, 50), new Size(1140, 660), BorderStyle.FixedSingle);

            // Destination name field
            panel.Controls.Add(AdminUIFactory.CreateLabel("Destination Name:", new Point(20, 20), new Size(150, 25)));
            _nameTextBox = AdminUIFactory.CreateTextBox(new Point(180, 20), new Size(300, 25));
            panel.Controls.Add(_nameTextBox);

            // Arrival time field
            panel.Controls.Add(AdminUIFactory.CreateLabel("Target Arrival Time:", new Point(20, 60), new Size(150, 25)));
            _timeTextBox = AdminUIFactory.CreateTextBox(new Point(180, 60), new Size(150, 25), "08:00:00");
            panel.Controls.Add(_timeTextBox);

            // Button to update only the time
            _updateTimeButton = AdminUIFactory.CreateButton("Update Arrival Time", new Point(340, 60), new Size(150, 25), UpdateTimeButtonClick);
            panel.Controls.Add(_updateTimeButton);

            // Google Map display
            _mapControl = new GMapControl
            {
                Location = new Point(20, 120),
                Size = new Size(800, 500),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 12,
                DragButton = MouseButtons.Left
            };
            panel.Controls.Add(_mapControl);
            MapService.InitializeGoogleMaps(_mapControl);

            // Inputs for latitude, longitude, address, and related buttons
            CreateLocationInputFields(panel);

            tabPage.Controls.Add(panel);
        }

        /// <summary>
        /// Creates the input fields and buttons for geolocation (lat, lng, address).
        /// </summary>
        private void CreateLocationInputFields(Panel panel)
        {
            panel.Controls.Add(AdminUIFactory.CreateLabel("Latitude:", new Point(850, 120), new Size(80, 25)));
            _latTextBox = AdminUIFactory.CreateTextBox(new Point(940, 120), new Size(180, 25));
            panel.Controls.Add(_latTextBox);

            panel.Controls.Add(AdminUIFactory.CreateLabel("Longitude:", new Point(850, 155), new Size(80, 25)));
            _lngTextBox = AdminUIFactory.CreateTextBox(new Point(940, 155), new Size(180, 25));
            panel.Controls.Add(_lngTextBox);

            panel.Controls.Add(AdminUIFactory.CreateLabel("Address:", new Point(850, 190), new Size(80, 25)));
            _addressTextBox = AdminUIFactory.CreateTextBox(new Point(940, 190), new Size(180, 60), "", true);
            panel.Controls.Add(_addressTextBox);

            _searchButton = AdminUIFactory.CreateButton("Search Address", new Point(940, 260), new Size(180, 30), SearchButtonClick);
            panel.Controls.Add(_searchButton);

            _saveButton = AdminUIFactory.CreateButton("Save Destination", new Point(940, 310), new Size(180, 30), SaveButtonClick);
            panel.Controls.Add(_saveButton);
        }

        /// <summary>
        /// Updates only the arrival time in the database.
        /// </summary>
        private async void UpdateTimeButtonClick(object sender, EventArgs e)
        {
            await UpdateTargetTimeAsync(_timeTextBox.Text);
        }

        /// <summary>
        /// Geocodes the address and updates the map and coordinates.
        /// </summary>
        private async void SearchButtonClick(object sender, EventArgs e)
        {
            await SearchAddressAsync();
        }

        /// <summary>
        /// Saves all destination details to the database.
        /// </summary>
        private async void SaveButtonClick(object sender, EventArgs e)
        {
            await SaveDestinationAsync();
        }

        /// <summary>
        /// Validates and updates the target arrival time in the DB.
        /// </summary>
        private async Task UpdateTargetTimeAsync(string timeString)
        {
            try
            {
                if (!TimeSpan.TryParse(timeString, out _))
                {
                    MessageDisplayer.ShowWarning("Please enter a valid time in format HH:MM:SS", "Invalid Time Format");
                    return;
                }

                var dest = await DbService.GetDestinationAsync();
                bool success = await DbService.UpdateDestinationAsync(dest.Name, dest.Latitude, dest.Longitude, timeString, dest.Address);

                if (success)
                    MessageDisplayer.ShowInfo("Target arrival time updated successfully.", "Success");
                else
                    MessageDisplayer.ShowError("Failed to update target arrival time.", "Error");
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError($"Error updating target time: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// Uses the address input to get coordinates and display them on the map.
        /// </summary>
        private async Task SearchAddressAsync()
        {
            try
            {
                var result = await MapService.GeocodeAddressAsync(_addressTextBox.Text);
                if (result.HasValue)
                {
                    _latTextBox.Text = result.Value.Latitude.ToString();
                    _lngTextBox.Text = result.Value.Longitude.ToString();

                    _mapControl.Position = new GMap.NET.PointLatLng(result.Value.Latitude, result.Value.Longitude);
                    _mapControl.Zoom = 15;

                    DisplayMarkerAtLocation(result.Value.Latitude, result.Value.Longitude);
                }
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError($"Error searching address: {ex.Message}", "Search Error");
            }
        }

        /// <summary>
        /// Displays a red marker on the map at the given coordinates.
        /// </summary>
        private void DisplayMarkerAtLocation(double latitude, double longitude)
        {
            _mapControl.Overlays.Clear();
            var overlay = new GMapOverlay("destinationMarker");
            var marker = new GMap.NET.WindowsForms.Markers.GMarkerGoogle(
                new GMap.NET.PointLatLng(latitude, longitude),
                GMap.NET.WindowsForms.Markers.GMarkerGoogleType.red
            );
            overlay.Markers.Add(marker);
            _mapControl.Overlays.Add(overlay);
        }

        /// <summary>
        /// Validates and saves all destination information to the database.
        /// </summary>
        private async Task SaveDestinationAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
                {
                    MessageDisplayer.ShowWarning("Please enter a destination name.", "Validation Error");
                    return;
                }

                if (!double.TryParse(_latTextBox.Text, out double lat) ||
                    !double.TryParse(_lngTextBox.Text, out double lng))
                {
                    MessageDisplayer.ShowWarning("Please enter valid coordinates.", "Validation Error");
                    return;
                }

                bool success = await DbService.UpdateDestinationAsync(_nameTextBox.Text, lat, lng, _timeTextBox.Text, _addressTextBox.Text);

                if (success)
                    MessageDisplayer.ShowInfo("Destination updated successfully.", "Success");
                else
                    MessageDisplayer.ShowError("Failed to update destination.", "Error");
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError($"Error saving destination: {ex.Message}", "Save Error");
            }
        }

        /// <summary>
        /// Refreshes the UI with the latest destination data from the database.
        /// </summary>
        public override async Task RefreshTabAsync()
        {
            try
            {
                var dest = await DbService.GetDestinationAsync();

                _nameTextBox.Text = dest.Name;
                _timeTextBox.Text = dest.TargetTime;
                _latTextBox.Text = dest.Latitude.ToString();
                _lngTextBox.Text = dest.Longitude.ToString();
                _addressTextBox.Text = dest.Address;

                _mapControl.Position = new GMap.NET.PointLatLng(dest.Latitude, dest.Longitude);
                _mapControl.Zoom = 15;

                DisplayMarkerAtLocation(dest.Latitude, dest.Longitude);
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError($"Error loading destination: {ex.Message}", "Loading Error");
            }
        }
    }
}
