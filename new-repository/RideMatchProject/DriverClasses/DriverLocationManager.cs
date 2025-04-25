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
    /// Manages location operations with proper thread safety
    /// </summary>
    public class DriverLocationManager
    {
        private readonly MapService _mapService;
        private readonly DriverDataManager _dataManager;
        private GMapControl _mapControl;
        private Label _instructionsLabel;
        private bool _isSettingLocation;

        public DriverLocationManager(MapService mapService, DriverDataManager dataManager)
        {
            _mapService = mapService;
            _dataManager = dataManager;
            _isSettingLocation = false;
        }

        public void SetMapControl(GMapControl mapControl)
        {
            _mapControl = mapControl;

            // Using UI thread for event handler registration
            ThreadUtils.ExecuteOnUIThread(_mapControl, () => {
                _mapControl.MouseClick += MapControl_MouseClick;
            });
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

                // Update instruction label visibility on UI thread
                if (_instructionsLabel != null)
                {
                    ThreadUtils.UpdateControlVisibility(_instructionsLabel, true);
                }

                // Update map cursor on UI thread
                if (_mapControl != null)
                {
                    ThreadUtils.ExecuteOnUIThread(_mapControl, () => {
                        _mapControl.Cursor = Cursors.Hand;
                    });

                    // Show message on UI thread
                    ThreadUtils.ExecuteOnUIThread(_mapControl, () => {
                        MessageBox.Show(_mapControl,
                            "Click on the map to set your starting location",
                            "Set Location",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                ThreadUtils.ShowErrorMessage(_mapControl,
                    $"Error enabling location selection: {ex.Message}",
                    "Error");

                _isSettingLocation = false;
            }
        }

        public async Task SearchAddressAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address) || _mapControl == null)
            {
                return;
            }

            try
            {
                // Set cursor to wait on UI thread
                ThreadUtils.ExecuteOnUIThread(_mapControl, () => {
                    if (_mapControl.Parent != null)
                    {
                        _mapControl.Parent.Cursor = Cursors.WaitCursor;
                    }
                });

                // Perform geocoding on background thread
                var result = await _mapService.GeocodeAddressAsync(address);

                if (result.HasValue)
                {
                    // Update map position on UI thread
                    _mapControl.SetMapPositionSafe(result.Value.Latitude, result.Value.Longitude, 15);

                    // Update location data
                    await UpdateLocationAsync(result.Value.Latitude, result.Value.Longitude);
                }
                else
                {
                    ThreadUtils.ShowErrorMessage(_mapControl,
                        "Address not found. Please try again.",
                        "Search Error");
                }
            }
            catch (Exception ex)
            {
                ThreadUtils.ShowErrorMessage(_mapControl,
                    $"Error searching: {ex.Message}",
                    "Error");
            }
            finally
            {
                // Reset cursor on UI thread
                ThreadUtils.ExecuteOnUIThread(_mapControl, () => {
                    if (_mapControl.Parent != null)
                    {
                        _mapControl.Parent.Cursor = Cursors.Default;
                    }
                });
            }
        }

        private void MapControl_MouseClick(object sender, MouseEventArgs e)
        {
            if (!_isSettingLocation || _mapControl == null)
            {
                return;
            }

            try
            {
                PointLatLng point = _mapControl.FromLocalToLatLng(e.X, e.Y);

                // Use SafeTaskRun to handle the async operation properly
                ThreadUtils.SafeTaskRun(async () => {
                    try
                    {
                        // Get address from coordinates
                        string address = await _mapService.ReverseGeocodeAsync(point.Lat, point.Lng);

                        // Update location data and UI
                        await ProcessLocationUpdate(point.Lat, point.Lng, address);
                    }
                    catch (Exception ex)
                    {
                        ThreadUtils.ShowErrorMessage(_mapControl,
                            $"Error getting address: {ex.Message}",
                            "Geocoding Error");

                        await ThreadUtils.ExecuteOnUIThreadAsync(_mapControl, async () => {
                            await Task.Delay(0); // Ensure we're on UI thread
                            DisableLocationSelection();
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                ThreadUtils.ShowErrorMessage(_mapControl,
                    $"Error processing map click: {ex.Message}",
                    "Error");

                DisableLocationSelection();
            }
        }

        private async Task ProcessLocationUpdate(double latitude, double longitude, string address)
        {
            // Update location and disable selection mode on UI thread
            await ThreadUtils.ExecuteOnUIThreadAsync(_mapControl, async () => {
                await UpdateLocationAsync(latitude, longitude, address);
                DisableLocationSelection();
            });
        }

        private void DisableLocationSelection()
        {
            _isSettingLocation = false;

            // Update instruction label visibility on UI thread
            if (_instructionsLabel != null)
            {
                ThreadUtils.UpdateControlVisibility(_instructionsLabel, false);
            }

            // Update map cursor on UI thread
            if (_mapControl != null)
            {
                ThreadUtils.ExecuteOnUIThread(_mapControl, () => {
                    _mapControl.Cursor = Cursors.Default;
                });
            }
        }

        public async Task UpdateLocationAsync(double latitude, double longitude, string address = null)
        {
            try
            {
                // Set cursor to wait on UI thread
                if (_mapControl?.Parent != null)
                {
                    ThreadUtils.ExecuteOnUIThread(_mapControl.Parent, () => {
                        _mapControl.Parent.Cursor = Cursors.WaitCursor;
                    });
                }

                // If address not provided, get it from coordinates
                if (string.IsNullOrEmpty(address))
                {
                    address = await _mapService.ReverseGeocodeAsync(latitude, longitude);
                }

                // Update vehicle location in the database
                bool success = await _dataManager.UpdateVehicleLocationAsync(latitude, longitude, address);

                // Show appropriate message on UI thread
                if (success)
                {
                    ThreadUtils.ShowInfoMessage(_mapControl,
                        $"Your starting location has been set to:\n{address}",
                        "Location Updated");
                }
                else
                {
                    ThreadUtils.ShowErrorMessage(_mapControl,
                        "Failed to update location. Please try again.",
                        "Update Failed");
                }
            }
            catch (Exception ex)
            {
                ThreadUtils.ShowErrorMessage(_mapControl,
                    $"Error updating location: {ex.Message}",
                    "Error");
            }
            finally
            {
                // Reset cursor on UI thread
                if (_mapControl?.Parent != null)
                {
                    ThreadUtils.ExecuteOnUIThread(_mapControl.Parent, () => {
                        _mapControl.Parent.Cursor = Cursors.Default;
                    });
                }
            }
        }
    }
}