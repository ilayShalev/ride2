using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using RideMatchProject.Models;
using RideMatchProject.Services;
using RideMatchProject.Utilities;

namespace RideMatchProject.DriverClasses
{
    /// <summary>
    /// Manages map-related operations for the driver interface with thread safety
    /// </summary>
    public class DriverMapManager
    {
        private readonly MapService _mapService;
        private readonly DatabaseService _dbService;
        private GMapControl _mapControl;
        private readonly object _syncLock = new object();

        public DriverMapManager(MapService mapService, DatabaseService dbService)
        {
            _mapService = mapService ?? throw new ArgumentNullException(nameof(mapService));
            _dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
        }

        public void InitializeMap(GMapControl mapControl, double latitude, double longitude)
        {
            _mapControl = mapControl ?? throw new ArgumentNullException(nameof(mapControl));

            // Initialize map on UI thread using our extensions
            ThreadUtils.ExecuteOnUIThread(_mapControl, () => {
                // Disable the default center marker (black dot)
                _mapControl.ShowCenter = false;

                // Initialize the map with Google Maps provider
                _mapService.InitializeGoogleMaps(_mapControl, latitude, longitude);

                // Ensure required features are enabled
                _mapControl.MarkersEnabled = true;
                _mapControl.PolygonsEnabled = true;
                _mapControl.RoutesEnabled = true;
            });
        }

        public async Task DisplayRouteOnMapAsync(Vehicle vehicle, List<Passenger> passengers)
        {
            if (_mapControl == null || vehicle == null)
            {
                return;
            }

            try
            {
                // Clear existing overlays
                _mapControl.ClearOverlaysSafe();

                // Get destination from database
                var destinationTuple = await _dbService.GetDestinationAsync();

                // Extract destination coordinates
                double destLatitude = destinationTuple.Latitude;
                double destLongitude = destinationTuple.Longitude;

                // Create overlays for map elements
                await ThreadUtils.ExecuteOnUIThreadAsync(_mapControl, async () => {
                    await CreateAndDisplayMapElements(vehicle, passengers, destLatitude, destLongitude);
                });
            }
            catch (Exception ex)
            {
                ThreadUtils.ShowErrorMessage(_mapControl,
                    $"Error displaying route: {ex.Message}",
                    "Map Display Error");
            }
        }

        private async Task CreateAndDisplayMapElements(Vehicle vehicle, List<Passenger> passengers,
            double destLatitude, double destLongitude)
        {
            // Create overlays for different map elements
            var routesOverlay = new GMapOverlay("routes");
            var vehiclesOverlay = new GMapOverlay("vehicles");
            var passengersOverlay = new GMapOverlay("passengers");
            var destinationOverlay = new GMapOverlay("destination");

            // Create waypoints for the route
            var routePoints = CreateRoutePoints(vehicle, passengers, destLatitude, destLongitude);

            // Try to get route from Google API
            List<PointLatLng> routePath = null;
            try
            {
                routePath = await _mapService.GetGoogleDirectionsAsync(routePoints);
            }
            catch (Exception ex)
            {
                // Fall back to direct route if API fails
                routePath = routePoints;
                ThreadUtils.ShowErrorMessage(_mapControl,
                    $"Could not get detailed route. Using direct route instead. Error: {ex.Message}",
                    "Route Error");
            }

            // Add the route to the overlay
            if (routePath != null && routePath.Count >= 2)
            {
                var route = new GMapRoute(routePath, "Driver Route")
                {
                    Stroke = new System.Drawing.Pen(System.Drawing.Color.FromArgb(180, 0, 0, 255), 4)
                };
                routesOverlay.Routes.Add(route);
            }

            // Add vehicle marker
            var vehicleMarker = new GMarkerGoogle(
                new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude),
                GMarkerGoogleType.green_dot)
            {
                ToolTipText = $"Your starting location" +
                    (!string.IsNullOrEmpty(vehicle.StartAddress)
                        ? $"\n{vehicle.StartAddress}"
                        : $"\n({vehicle.StartLatitude:F4}, {vehicle.StartLongitude:F4})"),
                ToolTipMode = MarkerTooltipMode.OnMouseOver
            };
            vehiclesOverlay.Markers.Add(vehicleMarker);

            // Add passenger markers
            if (passengers != null)
            {
                foreach (var passenger in passengers)
                {
                    if (passenger == null) continue;

                    var marker = new GMarkerGoogle(
                        new PointLatLng(passenger.Latitude, passenger.Longitude),
                        GMarkerGoogleType.blue_dot)
                    {
                        ToolTipText = $"Passenger: {passenger.Name}" +
                            (!string.IsNullOrEmpty(passenger.Address)
                                ? $"\n{passenger.Address}"
                                : $"\n({passenger.Latitude:F4}, {passenger.Longitude:F4})") +
                            (!string.IsNullOrEmpty(passenger.EstimatedPickupTime)
                                ? $"\nPickup at: {passenger.EstimatedPickupTime}"
                                : ""),
                        ToolTipMode = MarkerTooltipMode.OnMouseOver
                    };
                    passengersOverlay.Markers.Add(marker);
                }
            }

            // Add destination marker
            var destMarker = new GMarkerGoogle(
                new PointLatLng(destLatitude, destLongitude),
                GMarkerGoogleType.red_dot)
            {
                ToolTipText = "Destination",
                ToolTipMode = MarkerTooltipMode.OnMouseOver
            };
            destinationOverlay.Markers.Add(destMarker);

            // Add all overlays to map in order (routes first, then markers on top)
            _mapControl.Overlays.Add(routesOverlay);
            _mapControl.Overlays.Add(passengersOverlay);
            _mapControl.Overlays.Add(vehiclesOverlay);
            _mapControl.Overlays.Add(destinationOverlay);

            // Refresh the map
            _mapControl.RefreshMapSafe();
        }

        private List<PointLatLng> CreateRoutePoints(Vehicle vehicle, List<Passenger> passengers,
            double destLatitude, double destLongitude)
        {
            var points = new List<PointLatLng>();

            // Add vehicle starting point
            points.Add(new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude));

            // Add passenger pickup points
            if (passengers != null)
            {
                foreach (var passenger in passengers)
                {
                    if (passenger != null)
                    {
                        points.Add(new PointLatLng(passenger.Latitude, passenger.Longitude));
                    }
                }
            }

            // Add destination point
            points.Add(new PointLatLng(destLatitude, destLongitude));

            return points;
        }

        public void DisplayRouteOnMap(Vehicle vehicle, List<Passenger> passengers)
        {
            // Use SafeTaskRun to properly handle the async operation
            ThreadUtils.SafeTaskRun(async () => {
                await DisplayRouteOnMapAsync(vehicle, passengers);
            },
            ex => {
                ThreadUtils.ShowErrorMessage(_mapControl,
                    $"Error displaying route: {ex.Message}",
                    "Map Error");
            });
        }
    }
}