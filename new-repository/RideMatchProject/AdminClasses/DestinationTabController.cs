using GMap.NET.WindowsForms;
using RideMatchProject.Services;
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
    /// Controller for the Destination tab
    /// </summary>
    public class DestinationTabController : TabControllerBase
    {
        private TextBox _nameTextBox;
        private TextBox _timeTextBox;
        private TextBox _latTextBox;
        private TextBox _lngTextBox;
        private TextBox _addressTextBox;
        private Button _updateTimeButton;
        private Button _searchButton;
        private Button _saveButton;
        private GMapControl _mapControl;

        public DestinationTabController(
            DatabaseService dbService,
            MapService mapService)
            : base(dbService, mapService)
        {
        }

        public override void InitializeTab(TabPage tabPage)
        {
            CreateDestinationPanel(tabPage);
        }

        private void CreateDestinationPanel(TabPage tabPage)
        {
            // Main panel
            var panel = AdminUIFactory.CreatePanel(
                new Point(10, 50),
                new Size(1140, 660),
                BorderStyle.FixedSingle
            );

            // Name label and textbox
            panel.Controls.Add(AdminUIFactory.CreateLabel(
                "Destination Name:",
                new Point(20, 20),
                new Size(150, 25)
            ));
            _nameTextBox = AdminUIFactory.CreateTextBox(
                new Point(180, 20),
                new Size(300, 25)
            );
            panel.Controls.Add(_nameTextBox);

            // Target time label and textbox
            panel.Controls.Add(AdminUIFactory.CreateLabel(
                "Target Arrival Time:",
                new Point(20, 60),
                new Size(150, 25)
            ));
            _timeTextBox = AdminUIFactory.CreateTextBox(
                new Point(180, 60),
                new Size(150, 25),
                "08:00:00"
            );
            panel.Controls.Add(_timeTextBox);

            // Update time button
            _updateTimeButton = AdminUIFactory.CreateButton(
                "Update Arrival Time",
                new Point(340, 60),
                new Size(150, 25),
                UpdateTimeButtonClick
            );
            panel.Controls.Add(_updateTimeButton);

            // Map control
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

            // Location input fields
            CreateLocationInputFields(panel);

            // Add panel to tab
            tabPage.Controls.Add(panel);
        }

        private void CreateLocationInputFields(Panel panel)
        {
            // Latitude
            panel.Controls.Add(AdminUIFactory.CreateLabel(
                "Latitude:",
                new Point(850, 120),
                new Size(80, 25)
            ));
            _latTextBox = AdminUIFactory.CreateTextBox(
                new Point(940, 120),
                new Size(180, 25)
            );
            panel.Controls.Add(_latTextBox);

            // Longitude
            panel.Controls.Add(AdminUIFactory.CreateLabel(
                "Longitude:",
                new Point(850, 155),
                new Size(80, 25)
            ));
            _lngTextBox = AdminUIFactory.CreateTextBox(
                new Point(940, 155),
                new Size(180, 25)
            );
            panel.Controls.Add(_lngTextBox);

            // Address
            panel.Controls.Add(AdminUIFactory.CreateLabel(
                "Address:",
                new Point(850, 190),
                new Size(80, 25)
            ));
            _addressTextBox = AdminUIFactory.CreateTextBox(
                new Point(940, 190),
                new Size(180, 60),
                "",
                true
            );
            panel.Controls.Add(_addressTextBox);

            // Search button
            _searchButton = AdminUIFactory.CreateButton(
                "Search Address",
                new Point(940, 260),
                new Size(180, 30),
                SearchButtonClick
            );
            panel.Controls.Add(_searchButton);

            // Save button
            _saveButton = AdminUIFactory.CreateButton(
                "Save Destination",
                new Point(940, 310),
                new Size(180, 30),
                SaveButtonClick
            );
            panel.Controls.Add(_saveButton);
        }

        private async void UpdateTimeButtonClick(object sender, EventArgs e)
        {
            await UpdateTargetTimeAsync(_timeTextBox.Text);
        }

        private async void SearchButtonClick(object sender, EventArgs e)
        {
            await SearchAddressAsync();
        }

        private async void SaveButtonClick(object sender, EventArgs e)
        {
            await SaveDestinationAsync();
        }

        private async Task UpdateTargetTimeAsync(string timeString)
        {
            try
            {
                // Validate time format (HH:MM:SS)
                if (!TimeSpan.TryParse(timeString, out TimeSpan _))
                {
                    MessageDisplayer.ShowWarning(
                        "Please enter a valid time in format HH:MM:SS",
                        "Invalid Time Format"
                    );
                    return;
                }

                // Get current destination values
                var dest = await DbService.GetDestinationAsync();

                // Update just the target time
                bool success = await DbService.UpdateDestinationAsync(
                    dest.Name,
                    dest.Latitude,
                    dest.Longitude,
                    timeString,
                    dest.Address
                );

                if (success)
                {
                    MessageDisplayer.ShowInfo(
                        "Target arrival time updated successfully.",
                        "Success"
                    );
                }
                else
                {
                    MessageDisplayer.ShowError(
                        "Failed to update target arrival time.",
                        "Error"
                    );
                }
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error updating target time: {ex.Message}",
                    "Error"
                );
            }
        }

        private async Task SearchAddressAsync()
        {
            try
            {
                var result = await MapService.GeocodeAddressAsync(_addressTextBox.Text);
                if (result.HasValue)
                {
                    _latTextBox.Text = result.Value.Latitude.ToString();
                    _lngTextBox.Text = result.Value.Longitude.ToString();
                    _mapControl.Position = new GMap.NET.PointLatLng(
                        result.Value.Latitude,
                        result.Value.Longitude
                    );
                    _mapControl.Zoom = 15;

                    // Show marker
                    DisplayMarkerAtLocation(
                        result.Value.Latitude,
                        result.Value.Longitude
                    );
                }
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error searching address: {ex.Message}",
                    "Search Error"
                );
            }
        }

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

        private async Task SaveDestinationAsync()
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
                {
                    MessageDisplayer.ShowWarning(
                        "Please enter a destination name.",
                        "Validation Error"
                    );
                    return;
                }

                if (!double.TryParse(_latTextBox.Text, out double lat) ||
                    !double.TryParse(_lngTextBox.Text, out double lng))
                {
                    MessageDisplayer.ShowWarning(
                        "Please enter valid coordinates.",
                        "Validation Error"
                    );
                    return;
                }

                // Save destination
                bool success = await DbService.UpdateDestinationAsync(
                    _nameTextBox.Text,
                    lat,
                    lng,
                    _timeTextBox.Text,
                    _addressTextBox.Text
                );

                if (success)
                {
                    MessageDisplayer.ShowInfo(
                        "Destination updated successfully.",
                        "Success"
                    );
                }
                else
                {
                    MessageDisplayer.ShowError(
                        "Failed to update destination.",
                        "Error"
                    );
                }
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error saving destination: {ex.Message}",
                    "Save Error"
                );
            }
        }

        public override async Task RefreshTabAsync()
        {
            try
            {
                var dest = await DbService.GetDestinationAsync();

                // Update UI with destination info
                _nameTextBox.Text = dest.Name;
                _timeTextBox.Text = dest.TargetTime;
                _latTextBox.Text = dest.Latitude.ToString();
                _lngTextBox.Text = dest.Longitude.ToString();
                _addressTextBox.Text = dest.Address;

                // Show on map
                _mapControl.Position = new GMap.NET.PointLatLng(
                    dest.Latitude,
                    dest.Longitude
                );
                _mapControl.Zoom = 15;

                // Show marker
                DisplayMarkerAtLocation(dest.Latitude, dest.Longitude);
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error loading destination: {ex.Message}",
                    "Loading Error"
                );
            }
        }
    }

}
