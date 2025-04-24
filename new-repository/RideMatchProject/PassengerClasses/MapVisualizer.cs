using GMap.NET.WindowsForms;
using GMap.NET;
using RideMatchProject.Models;
using RideMatchProject.Services;
using RideMatchProject.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.PassengerClasses
{
    /// <summary>
    /// Manages map visualization and location services
    /// </summary>
    public class MapVisualizer
    {
        private readonly MapService _mapService;
        private readonly DatabaseService _databaseService;
        private readonly GMapControl _mapControl;

        public event EventHandler<PointLatLng> MapClicked;

        public MapVisualizer(MapService mapService, DatabaseService databaseService, GMapControl mapControl)
        {
            _mapService = mapService;
            _databaseService = databaseService;
            _mapControl = mapControl;
        }

        public async Task InitializeMapAsync()
        {
            if (_mapControl != null)
            {
                EnsureUIThread(() => {
                    _mapService.InitializeGoogleMaps(_mapControl);
                    _mapControl.Position = new PointLatLng(32.0741, 34.7922); // Default to Tel Aviv
                    _mapControl.Zoom = 12;
                });
            }
        }

        public void EnableLocationSelection(bool enable)
        {
            if (_mapControl == null)
            {
                return;
            }

            EnsureUIThread(() => {
                _mapControl.Cursor = enable ? Cursors.Hand : Cursors.Default;

                if (enable)
                {
                    _mapControl.MouseClick += HandleMapClick;
                }
                else
                {
                    _mapControl.MouseClick -= HandleMapClick;
                }
            });
        }

        private void HandleMapClick(object sender, MouseEventArgs e)
        {
            if (_mapControl == null) return;

            PointLatLng point = _mapControl.FromLocalToLatLng(e.X, e.Y);
            MapClicked?.Invoke(this, point);
        }

        public async Task<(double Latitude, double Longitude)?> GeocodeAddressAsync(string address)
        {
            return await _mapService.GeocodeAddressAsync(address);
        }

        public async Task<string> ReverseGeocodeAsync(double latitude, double longitude)
        {
            return await _mapService.ReverseGeocodeAsync(latitude, longitude);
        }

        public void DisplayPassenger(Passenger passenger, Vehicle vehicle)
        {
            if (_mapControl == null || passenger == null)
            {
                return;
            }

            EnsureUIThread(() => {
                _mapControl.Overlays.Clear();

                var passengersOverlay = new GMapOverlay("passengers");
                var marker = MapOverlays.CreatePassengerMarker(passenger);
                passengersOverlay.Markers.Add(marker);
                _mapControl.Overlays.Add(passengersOverlay);

                if (vehicle != null)
                {
                    DisplayDestination();
                }

                _mapControl.Position = new PointLatLng(passenger.Latitude, passenger.Longitude);
                _mapControl.Zoom = 15;
                _mapControl.Refresh();
            });
        }

        private void DisplayDestination()
        {
            Task.Run(async () => {
                var destination = await GetDestinationAsync();

                EnsureUIThread(() => {
                    try
                    {
                        var destinationOverlay = new GMapOverlay("destination");
                        var destinationMarker = MapOverlays.CreateDestinationMarker(
                            destination.Latitude, destination.Longitude);

                        destinationOverlay.Markers.Add(destinationMarker);
                        _mapControl.Overlays.Add(destinationOverlay);
                        _mapControl.Refresh();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating destination marker: {ex.Message}");
                    }
                });
            });
        }

        private async Task<(double Latitude, double Longitude)> GetDestinationAsync()
        {
            var destination = await _databaseService.GetDestinationAsync();
            return (destination.Latitude, destination.Longitude);
        }

        /// <summary>
        /// Ensures that code runs on the UI thread
        /// </summary>
        private void EnsureUIThread(Action action)
        {
            if (_mapControl.InvokeRequired)
            {
                try
                {
                    _mapControl.Invoke(action);
                }
                catch (ObjectDisposedException)
                {
                    // Control may have been disposed if form is closing
                }
                catch (InvalidOperationException)
                {
                    // Handle case where handle isn't created yet
                }
            }
            else
            {
                action();
            }
        }
    }

}
