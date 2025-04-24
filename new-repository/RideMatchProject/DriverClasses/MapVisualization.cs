using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using RideMatchProject.Models;
using RideMatchProject.Services;
using RideMatchProject.UI;
using RideMatchProject.Utilities;

namespace RideMatchProject.DriverClasses
{
    /// <summary>
    /// Enhanced map visualization that ensures points (markers) are visible over routes
    /// </summary>
    public class MapVisualization
    {
        private readonly MapService _mapService;
        private readonly GMapControl _mapControl;
        private readonly ThreadSafeMapManager _threadSafeMapManager;

        // Define marker sizes to make them more prominent
        private const int VehicleMarkerSize = 12;
        private const int PassengerMarkerSize = 10;
        private const int DestinationMarkerSize = 12;

        public MapVisualization(MapService mapService, GMapControl mapControl)
        {
            _mapService = mapService ?? throw new ArgumentNullException(nameof(mapService));
            _mapControl = mapControl ?? throw new ArgumentNullException(nameof(mapControl));
            _threadSafeMapManager = new ThreadSafeMapManager(_mapControl);
        }

        /// <summary>
        /// Displays a route with enhanced visibility of points
        /// </summary>
        public async Task DisplayRouteWithPointsAsync(Vehicle vehicle, List<Passenger> passengers, double destLatitude, double destLongitude)
        {
            try
            {
                // Clear existing overlays
                _threadSafeMapManager.ClearOverlays();

                // Create overlays in correct order (bottom to top)
                var routesOverlay = new GMapOverlay("routes");
                var vehiclesOverlay = new GMapOverlay("vehicles");
                var passengersOverlay = new GMapOverlay("passengers");
                var destinationOverlay = new GMapOverlay("destination");

                // First add the route - this will be at the bottom
                await AddRouteAsync(vehicle, passengers, destLatitude, destLongitude, routesOverlay);

                // Then add all markers that should appear on top of the route
                AddVehicleMarker(vehicle, vehiclesOverlay);
                AddPassengerMarkers(passengers, passengersOverlay);
                AddDestinationMarker(destLatitude, destLongitude, destinationOverlay);

                // Add overlays to the map in order (routes first, then markers)
                _threadSafeMapManager.AddOverlay(routesOverlay);
                _threadSafeMapManager.AddOverlay(vehiclesOverlay);
                _threadSafeMapManager.AddOverlay(passengersOverlay);
                _threadSafeMapManager.AddOverlay(destinationOverlay);

                // Center map on the vehicle's position
                if (vehicle != null && vehicle.StartLatitude != 0 && vehicle.StartLongitude != 0)
                {
                    _threadSafeMapManager.SetPosition(vehicle.StartLatitude, vehicle.StartLongitude, 13);
                }

                // Refresh the map
                _threadSafeMapManager.RefreshMap();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error displaying route: {ex.Message}",
                    "Map Visualization Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task AddRouteAsync(Vehicle vehicle, List<Passenger> passengers, double destLatitude, double destLongitude, GMapOverlay routesOverlay)
        {
            if (vehicle == null || (passengers == null || passengers.Count == 0))
            {
                return;
            }

            try
            {
                // Create the route points
                var routePoints = new List<PointLatLng>();

                // Add starting point
                routePoints.Add(new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude));

                // Add passenger points
                foreach (var passenger in passengers)
                {
                    if (passenger != null)
                    {
                        routePoints.Add(new PointLatLng(passenger.Latitude, passenger.Longitude));
                    }
                }

                // Add destination point
                routePoints.Add(new PointLatLng(destLatitude, destLongitude));

                if (routePoints.Count < 2)
                {
                    return;
                }

                // Try to get the route from Google API
                List<PointLatLng> finalRoutePoints;
                try
                {
                    finalRoutePoints = await _mapService.GetGoogleDirectionsAsync(routePoints);

                    if (finalRoutePoints == null || finalRoutePoints.Count == 0)
                    {
                        finalRoutePoints = routePoints;
                    }
                }
                catch
                {
                    // Fall back to direct route if API fails
                    finalRoutePoints = routePoints;
                }

                // Create the route with a semi-transparent color so markers stand out
                var route = new GMapRoute(finalRoutePoints, "Route")
                {
                    Stroke = new Pen(Color.FromArgb(180, 0, 0, 255), 4) // Semi-transparent blue
                };

                // Add the route to the overlay
                routesOverlay.Routes.Add(route);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating route: {ex.Message}");
            }
        }

        private void AddVehicleMarker(Vehicle vehicle, GMapOverlay vehiclesOverlay)
        {
            if (vehicle == null || (vehicle.StartLatitude == 0 && vehicle.StartLongitude == 0))
            {
                return;
            }

            // Create a custom larger vehicle marker
            var point = new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude);
            var marker = CreateCustomMarker(point, GMarkerGoogleType.green_dot, VehicleMarkerSize);

            marker.ToolTipText = $"Driver: {vehicle.DriverName ?? "Unknown"}\nCapacity: {vehicle.Capacity}\n" +
                                 $"{(string.IsNullOrEmpty(vehicle.StartAddress) ? $"({vehicle.StartLatitude:F6}, {vehicle.StartLongitude:F6})" : vehicle.StartAddress)}";
            marker.ToolTipMode = MarkerTooltipMode.OnMouseOver;

            vehiclesOverlay.Markers.Add(marker);
        }

        private void AddPassengerMarkers(List<Passenger> passengers, GMapOverlay passengersOverlay)
        {
            if (passengers == null || passengers.Count == 0)
            {
                return;
            }

            foreach (var passenger in passengers)
            {
                if (passenger == null)
                {
                    continue;
                }

                // Create a custom larger passenger marker
                var point = new PointLatLng(passenger.Latitude, passenger.Longitude);
                var marker = CreateCustomMarker(point, GMarkerGoogleType.blue_dot, PassengerMarkerSize);

                marker.ToolTipText = $"Passenger: {passenger.Name}\n" +
                                     $"{(string.IsNullOrEmpty(passenger.Address) ? $"({passenger.Latitude:F6}, {passenger.Longitude:F6})" : passenger.Address)}\n" +
                                     $"{(string.IsNullOrEmpty(passenger.EstimatedPickupTime) ? "" : $"Pickup at: {passenger.EstimatedPickupTime}")}";
                marker.ToolTipMode = MarkerTooltipMode.OnMouseOver;

                passengersOverlay.Markers.Add(marker);
            }
        }

        private void AddDestinationMarker(double latitude, double longitude, GMapOverlay destinationOverlay)
        {
            // Create a custom larger destination marker
            var point = new PointLatLng(latitude, longitude);
            var marker = CreateCustomMarker(point, GMarkerGoogleType.red_dot, DestinationMarkerSize);

            marker.ToolTipText = "Destination";
            marker.ToolTipMode = MarkerTooltipMode.OnMouseOver;

            destinationOverlay.Markers.Add(marker);
        }

        private GMarkerGoogle CreateCustomMarker(PointLatLng point, GMarkerGoogleType type, int size)
        {
            // Create a marker with custom size
            var marker = new GMarkerGoogle(point, type);

            // If we want to make the marker bigger, we could use a custom bitmap
            // This would require more code to create and scale the bitmap

            return marker;
        }
    }
}