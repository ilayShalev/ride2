using GMap.NET.WindowsForms;
using GMap.NET;
using RideMatchProject.Services;
using RideMatchProject.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.DriverClasses
{
    /// <summary>
    /// Manages location operations
    /// </summary>
    public class DriverLocationManager
    {
        private readonly MapService _mapService;
        private readonly DriverDataManager _dataManager;
        private GMapControl _mapControl;
        private Label _instructionsLabel;
        private bool _isSettingLocation;
        private ThreadSafeMapManager _threadSafeMapManager;

        public DriverLocationManager(MapService mapService, DriverDataManager dataManager)
        {
            _mapService = mapService;
            _dataManager = dataManager;
        }

        public void SetMapControl(GMapControl mapControl)
        {
            _mapControl = mapControl;
            _threadSafeMapManager = new ThreadSafeMapManager(_mapControl);
            _mapControl.MouseClick += MapControl_MouseClick;
        }

        public void SetInstructionLabel(Label instructionsLabel)
        {
            _instructionsLabel = instructionsLabel;
        }

        public void EnableLocationSelection()
        {
            try
            {
                _isSettingLocation = true;

                if (_instructionsLabel != null)
                {
                    ShowInstructionsLabel(true);
                }

                if (_mapControl != null)
                {
                    SetMapCursor(Cursors.Hand);
                }

                MessageBox.Show("Click on the map to set your starting location",
                    "Set Location", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error enabling location selection: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                _isSettingLocation = false;
            }
        }

        private void ShowInstructionsLabel(bool visible)
        {
            if (_instructionsLabel.InvokeRequired)
            {
                _instructionsLabel.Invoke(new Action(() => _instructionsLabel.Visible = visible));
            }
            else
            {
                _instructionsLabel.Visible = visible;
            }
        }

        private void SetMapCursor(Cursor cursor)
        {
            _threadSafeMapManager.ExecuteOnUIThread(() => {
                _mapControl.Cursor = cursor;
            });
        }

        public async Task SearchAddressAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;

            try
            {
                SetParentCursor(Cursors.WaitCursor);

                var result = await _mapService.GeocodeAddressAsync(address);

                if (result.HasValue)
                {
                    _threadSafeMapManager.SetPosition(result.Value.Latitude, result.Value.Longitude, 15);
                    await UpdateLocationAsync(result.Value.Latitude, result.Value.Longitude);
                }
                else
                {
                    MessageBox.Show("Address not found. Please try again.",
                        "Search Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error searching: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetParentCursor(Cursors.Default);
            }
        }

        private void SetParentCursor(Cursor cursor)
        {
            if (_mapControl.Parent != null)
            {
                if (_mapControl.Parent.InvokeRequired)
                {
                    _mapControl.Parent.Invoke(new Action(() => _mapControl.Parent.Cursor = cursor));
                }
                else
                {
                    _mapControl.Parent.Cursor = cursor;
                }
            }
        }

        private void MapControl_MouseClick(object sender, MouseEventArgs e)
        {
            if (!_isSettingLocation) return;

            try
            {
                PointLatLng point = _mapControl.FromLocalToLatLng(e.X, e.Y);

                Task.Run(async () => {
                    try
                    {
                        string address = await _mapService.ReverseGeocodeAsync(point.Lat, point.Lng);

                        SafeProcessLocationUpdate(point.Lat, point.Lng, address);
                    }
                    catch (Exception ex)
                    {
                        ShowMapClickError(ex);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing map click: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                DisableLocationSelection();
            }
        }

        private void SafeProcessLocationUpdate(double latitude, double longitude, string address)
        {
            if (_mapControl.InvokeRequired)
            {
                _mapControl.Invoke(new Action(async () => {
                    await UpdateLocationAsync(latitude, longitude, address);
                    DisableLocationSelection();
                }));
            }
            else
            {
                // Already on UI thread
                Task.Run(async () => {
                    await UpdateLocationAsync(latitude, longitude, address);
                    DisableLocationSelection();
                });
            }
        }

        private void ShowMapClickError(Exception ex)
        {
            if (_mapControl.InvokeRequired)
            {
                _mapControl.Invoke(new Action(() => {
                    MessageBox.Show($"Error getting address: {ex.Message}",
                        "Geocoding Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    DisableLocationSelection();
                }));
            }
            else
            {
                MessageBox.Show($"Error getting address: {ex.Message}",
                    "Geocoding Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                DisableLocationSelection();
            }
        }

        private void DisableLocationSelection()
        {
            _isSettingLocation = false;

            if (_instructionsLabel != null)
            {
                ShowInstructionsLabel(false);
            }

            if (_mapControl != null)
            {
                SetMapCursor(Cursors.Default);
            }
        }

        public async Task UpdateLocationAsync(double latitude, double longitude, string address = null)
        {
            try
            {
                SetParentCursor(Cursors.WaitCursor);

                if (string.IsNullOrEmpty(address))
                {
                    address = await _mapService.ReverseGeocodeAsync(latitude, longitude);
                }

                bool success = await _dataManager.UpdateVehicleLocationAsync(latitude, longitude, address);

                if (success)
                {
                    MessageBox.Show($"Your starting location has been set to:\n{address}",
                        "Location Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                SetParentCursor(Cursors.Default);
            }
        }
    }
}