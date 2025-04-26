using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GMap.NET;
using System.Windows.Forms;
using GMap.NET.WindowsForms;
using RideMatchProject.Models;
using RideMatchProject.UI;

namespace RideMatchProject.Services.RoutingServiceClasses
{
    /// <summary>
    /// Manages map display operations
    /// </summary>
    public class MapDisplayManager
    {
        private readonly MapService _mapService;

        public MapDisplayManager(MapService mapService)
        {
            _mapService = mapService;
        }

        /// <summary>
        /// Displays data points (passengers, vehicles, destination) on the map
        /// </summary>
        public void DisplayDataOnMap(GMapControl mapControl, List<Passenger> passengers,
            List<Vehicle> vehicles, DestinationInfo destination)
        {
            if (mapControl == null)
            {
                return;
            }

            ClearAndPrepareOverlays(mapControl, out var passengersOverlay,
                out var vehiclesOverlay, out var destinationOverlay, out var routesOverlay);

            AddDestinationMarker(destinationOverlay, destination);
            AddPassengerMarkers(passengersOverlay, passengers);
            AddVehicleMarkers(vehiclesOverlay, vehicles);

            AddOverlaysToMap(mapControl, routesOverlay, passengersOverlay,
                vehiclesOverlay, destinationOverlay);

            RefreshMap(mapControl);
        }

        private void ClearAndPrepareOverlays(GMapControl mapControl,
            out GMapOverlay passengersOverlay, out GMapOverlay vehiclesOverlay,
            out GMapOverlay destinationOverlay, out GMapOverlay routesOverlay)
        {
            mapControl.Overlays.Clear();

            passengersOverlay = new GMapOverlay("passengers");
            vehiclesOverlay = new GMapOverlay("vehicles");
            destinationOverlay = new GMapOverlay("destination");
            routesOverlay = new GMapOverlay("routes");
        }

        private void AddDestinationMarker(GMapOverlay destinationOverlay, DestinationInfo destination)
        {
            var destinationMarker = MapOverlays.CreateDestinationMarker(
                destination.Latitude, destination.Longitude);
            destinationOverlay.Markers.Add(destinationMarker);
        }

        private void AddPassengerMarkers(GMapOverlay passengersOverlay, List<Passenger> passengers)
        {
            if (passengers == null)
                return;

            foreach (var passenger in passengers)
            {
                var marker = MapOverlays.CreatePassengerMarker(passenger);
                passengersOverlay.Markers.Add(marker);
            }
        }

        private void AddVehicleMarkers(GMapOverlay vehiclesOverlay, List<Vehicle> vehicles)
        {
            if (vehicles == null)
                return;

            foreach (var vehicle in vehicles)
            {
                var marker = MapOverlays.CreateVehicleMarker(vehicle);
                vehiclesOverlay.Markers.Add(marker);
            }
        }

        private void AddOverlaysToMap(GMapControl mapControl, params GMapOverlay[] overlays)
        {
            foreach (var overlay in overlays)
            {
                mapControl.Overlays.Add(overlay);
            }
        }

        private void RefreshMap(GMapControl mapControl)
        {
            mapControl.Zoom = mapControl.Zoom; // Force refresh
        }

        // Make sure to implement DisplaySolutionOnMap method as well
        public void DisplaySolutionOnMap(GMapControl mapControl, Solution solution,
            DestinationInfo destination)
        {
            if (mapControl == null || solution == null)
            {
                return;
            }

            try
            {
                DisplayDataOnMap(mapControl, new List<Passenger>(), solution.Vehicles, destination);

                var routesOverlay = GetOrCreateRoutesOverlay(mapControl);
                var colors = MapOverlays.GetRouteColors();

                AddVehicleRoutes(routesOverlay, solution, colors, destination);

                RefreshMap(mapControl);
            }
            catch (Exception ex)
            {
                HandleDisplayError(ex);
            }
        }

        private GMapOverlay GetOrCreateRoutesOverlay(GMapControl mapControl)
        {
            var routesOverlay = mapControl.Overlays.FirstOrDefault(o => o.Id == "routes");

            if (routesOverlay == null)
            {
                routesOverlay = new GMapOverlay("routes");
                mapControl.Overlays.Add(routesOverlay);
            }
            else
            {
                routesOverlay.Routes.Clear();
            }

            return routesOverlay;
        }

        private void AddVehicleRoutes(GMapOverlay routesOverlay, Solution solution,
            Color[] colors, DestinationInfo destination)
        {
            for (int i = 0; i < solution.Vehicles.Count; i++)
            {
                var vehicle = solution.Vehicles[i];

                if (vehicle == null || vehicle.AssignedPassengers == null ||
                    vehicle.AssignedPassengers.Count == 0)
                {
                    continue;
                }

                // Use saved route path if available
                if (vehicle.RoutePath != null && vehicle.RoutePath.Count > 1)
                {
                    AddDetailedRoute(routesOverlay, vehicle.RoutePath, i, colors);
                }
                else
                {
                    // Fall back to simple waypoint-to-waypoint route
                    var simplePoints = CreateRoutePoints(vehicle, destination);
                    AddSimpleRoute(routesOverlay, simplePoints, i, colors);
                }
            }
        }

        private void AddDetailedRoute(GMapOverlay routesOverlay, List<PointLatLng> routePath,
            int vehicleIndex, Color[] colors)
        {
            var routeColor = colors[vehicleIndex % colors.Length];
            var route = MapOverlays.CreateRoute(routePath, $"Route {vehicleIndex}", routeColor);
            routesOverlay.Routes.Add(route);
        }

        private void AddSimpleRoute(GMapOverlay routesOverlay, List<PointLatLng> points,
            int vehicleIndex, Color[] colors)
        {
            var routeColor = colors[vehicleIndex % colors.Length];
            var route = MapOverlays.CreateRoute(points, $"Route {vehicleIndex} (simple)", routeColor);
            routesOverlay.Routes.Add(route);
        }

        private List<PointLatLng> CreateRoutePoints(Vehicle vehicle, DestinationInfo destination)
        {
            var points = new List<PointLatLng>
            {
                new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude)
            };

            foreach (var passenger in vehicle.AssignedPassengers)
            {
                if (passenger != null)
                {
                    points.Add(new PointLatLng(passenger.Latitude, passenger.Longitude));
                }
            }

            points.Add(new PointLatLng(destination.Latitude, destination.Longitude));

            return points;
        }

        private void HandleDisplayError(Exception ex)
        {
            MessageBox.Show($"Error displaying solution on map: {ex.Message}",
                "Map Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}