using GMap.NET.WindowsForms;
using GMap.NET;
using RideMatchProject.Services;
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
        private readonly MapService mapService;
        private readonly DriverDataManager dataManager;
        private GMapControl mapControl;
        private Label instructionsLabel;
        private bool isSettingLocation;

        public DriverLocationManager(MapService mapService, DriverDataManager dataManager)
        {
            this.mapService = mapService;
            this.dataManager = dataManager;
        }

        public void SetMapControl(GMapControl mapControl)
        {
            this.mapControl = mapControl;
            this.mapControl.MouseClick += MapControl_MouseClick;
        }

        public void SetInstructionLabel(Label instructionsLabel)
        {
            this.instructionsLabel = instructionsLabel;
        }

        public void EnableLocationSelection()
        {
            try
            {
                isSettingLocation = true;

                if (instructionsLabel != null)
                {
                    instructionsLabel.Visible = true;
                }

                if (mapControl != null)
                {
                    mapControl.Cursor = Cursors.Hand;
                }

                MessageBox.Show("Click on the map to set your starting location",
                    "Set Location", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error enabling location selection: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                isSettingLocation = false;
            }
        }

        public async Task SearchAddressAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;

            try
            {
                mapControl.Parent.Cursor = Cursors.WaitCursor;

                var result = await mapService.GeocodeAddressAsync(address);

                if (result.HasValue)
                {
                    mapControl.Position = new PointLatLng(result.Value.Latitude, result.Value.Longitude);
                    mapControl.Zoom = 15;

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
                mapControl.Parent.Cursor = Cursors.Default;
            }
        }

        private void MapControl_MouseClick(object sender, MouseEventArgs e)
        {
            if (!isSettingLocation) return;

            try
            {
                PointLatLng point = mapControl.FromLocalToLatLng(e.X, e.Y);

                Task.Run(async () => {
                    try
                    {
                        string address = await mapService.ReverseGeocodeAsync(point.Lat, point.Lng);

                        mapControl.Invoke(new Action(async () => {
                            await UpdateLocationAsync(point.Lat, point.Lng, address);
                            DisableLocationSelection();
                        }));
                    }
                    catch (Exception ex)
                    {
                        mapControl.Invoke(new Action(() => {
                            MessageBox.Show($"Error getting address: {ex.Message}",
                                "Geocoding Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                            DisableLocationSelection();
                        }));
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

        private void DisableLocationSelection()
        {
            isSettingLocation = false;

            if (instructionsLabel != null)
            {
                instructionsLabel.Visible = false;
            }

            if (mapControl != null)
            {
                mapControl.Cursor = Cursors.Default;
            }
        }

        public async Task UpdateLocationAsync(double latitude, double longitude, string address = null)
        {
            try
            {
                mapControl.Parent.Cursor = Cursors.WaitCursor;

                if (string.IsNullOrEmpty(address))
                {
                    address = await mapService.ReverseGeocodeAsync(latitude, longitude);
                }

                bool success = await dataManager.UpdateVehicleLocationAsync(latitude, longitude, address);

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
                mapControl.Parent.Cursor = Cursors.Default;
            }
        }
    }
}
