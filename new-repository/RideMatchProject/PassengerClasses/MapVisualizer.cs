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
    /// Manages map visualization and location services for displaying passengers, destinations, and handling map interactions
    /// in a ride-matching application. Utilizes GMap.NET for map rendering and integrates with map and database services.
    /// </summary>
    public class MapVisualizer
    {
        /// <summary>
        /// The map service used for initializing the map and performing geocoding operations.
        /// </summary>
        private readonly MapService _mapService;

        /// <summary>
        /// The database service used for retrieving destination data.
        /// </summary>
        private readonly DatabaseService _databaseService;

        /// <summary>
        /// The GMap.NET control used for rendering the map in the Windows Forms UI.
        /// </summary>
        private readonly GMapControl _mapControl;

        /// <summary>
        /// Occurs when the map is clicked, providing the clicked location as a <see cref="PointLatLng"/>.
        /// </summary>
        public event EventHandler<PointLatLng> MapClicked;

        /// <summary>
        /// Initializes a new instance of the <see cref="MapVisualizer"/> class.
        /// </summary>
        /// <param name="mapService">The <see cref="MapService"/> instance for map initialization and geocoding.</param>
        /// <param name="databaseService">The <see cref="DatabaseService"/> instance for retrieving destination data.</param>
        /// <param name="mapControl">The <see cref="GMapControl"/> instance for rendering the map.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mapService"/> or <paramref name="databaseService"/> is null.</exception>
        public MapVisualizer(MapService mapService, DatabaseService databaseService, GMapControl mapControl)
        {
            _mapService = mapService ?? throw new ArgumentNullException(nameof(mapService), "MapService cannot be null.");
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService), "DatabaseService cannot be null.");
            _mapControl = mapControl; // mapControl can be null, handled in methods
        }

        /// <summary>
        /// Initializes the map control asynchronously, setting default position and zoom level.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// The map is centered on Tel Aviv (32.0741, 34.7922) with a zoom level of 12 by default.
        /// If the map control is null, the method does nothing.
        /// </remarks>
        public async Task InitializeMapAsync()
        {
            if (_mapControl != null)
            {
                EnsureUIThread(() =>
                {
                    _mapService.InitializeGoogleMaps(_mapControl);
                    _mapControl.Position = new PointLatLng(32.0741, 34.7922); // Default to Tel Aviv
                    _mapControl.Zoom = 12;
                });
            }
            await Task.CompletedTask; // Ensure async signature
        }

        /// <summary>
        /// Enables or disables location selection mode on the map, changing the cursor and click behavior.
        /// </summary>
        /// <param name="enable">If <c>true</c>, enables location selection with a hand cursor and click handling; otherwise, disables it.</param>
        /// <remarks>
        /// When enabled, clicking the map triggers the <see cref="MapClicked"/> event with the clicked coordinates.
        /// If the map control is null, the method does nothing.
        /// </remarks>
        public void EnableLocationSelection(bool enable)
        {
            if (_mapControl == null)
            {
                return;
            }

            EnsureUIThread(() =>
            {
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

        /// <summary>
        /// Handles map click events, converting the click location to geographic coordinates and raising the <see cref="MapClicked"/> event.
        /// </summary>
        /// <param name="sender">The source of the event (the <see cref="GMapControl"/>).</param>
        /// <param name="e">The <see cref="MouseEventArgs"/> containing click details.</param>
        private void HandleMapClick(object sender, MouseEventArgs e)
        {
            if (_mapControl == null) return;

            PointLatLng point = _mapControl.FromLocalToLatLng(e.X, e.Y);
            MapClicked?.Invoke(this, point);
        }

        /// <summary>
        /// Converts a textual address to geographic coordinates (latitude and longitude) asynchronously.
        /// </summary>
        /// <param name="address">The address to geocode.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a nullable tuple of (Latitude, Longitude) if successful; otherwise, <c>null</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="address"/> is null.</exception>
        public async Task<(double Latitude, double Longitude)?> GeocodeAddressAsync(string address)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address), "Address cannot be null.");

            return await _mapService.GeocodeAddressAsync(address);
        }

        /// <summary>
        /// Converts geographic coordinates to a textual address asynchronously.
        /// </summary>
        /// <param name="latitude">The latitude of the location.</param>
        /// <param name="longitude">The longitude of the location.</param>
        /// <returns>A <see cref="Task"/> containing the address as a string.</returns>
        public async Task<string> ReverseGeocodeAsync(double latitude, double longitude)
        {
            return await _mapService.ReverseGeocodeAsync(latitude, longitude);
        }

        /// <summary>
        /// Displays a passenger's location on the map, optionally including a destination marker if a vehicle is provided.
        /// </summary>
        /// <param name="passenger">The <see cref="Passenger"/> to display, containing location data.</param>
        /// <param name="vehicle">The <see cref="Vehicle"/> associated with the passenger, if any. If provided, a destination marker is displayed.</param>
        /// <remarks>
        /// Clears existing overlays, adds a passenger marker, and centers the map on the passenger's location with a zoom level of 15.
        /// If the map control or passenger is null, the method does nothing.
        /// </remarks>
        public void DisplayPassenger(Passenger passenger, Vehicle vehicle)
        {
            if (_mapControl == null || passenger == null)
            {
                return;
            }

            EnsureUIThread(() =>
            {
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

        /// <summary>
        /// Displays a destination marker on the map by retrieving destination coordinates from the database.
        /// </summary>
        /// <remarks>
        /// Runs asynchronously to fetch destination data and updates the UI on the UI thread.
        /// Catches and logs exceptions to prevent UI crashes.
        /// </remarks>
        private void DisplayDestination()
        {
            Task.Run(async () =>
            {
                var destination = await GetDestinationAsync();

                EnsureUIThread(() =>
                {
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

        /// <summary>
        /// Retrieves destination coordinates from the database asynchronously.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> containing a tuple of (Latitude, Longitude) representing the destination.
        /// </returns>
        private async Task<(double Latitude, double Longitude)> GetDestinationAsync()
        {
            var destination = await _databaseService.GetDestinationAsync();
            return (destination.Latitude, destination.Longitude);
        }

        /// <summary>
        /// Ensures that the provided action is executed on the UI thread.
        /// </summary>
        /// <param name="action">The <see cref="Action"/> to execute.</param>
        /// <remarks>
        /// If the map control requires invocation (i.e., the call is from a non-UI thread), the action is invoked on the UI thread.
        /// Handles <see cref="ObjectDisposedException"/> and <see cref="InvalidOperationException"/> to prevent crashes if the control is disposed or not ready.
        /// </remarks>
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