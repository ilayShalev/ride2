using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using RideMatchProject.Models;
using RideMatchProject.Services;
using RideMatchProject.UI;

namespace RideMatchProject.DriverClasses
{
    /// <summary>
    /// Manages map-related operations for the driver interface
    /// </summary>
    public class DriverMapManager
    {
        private readonly MapService mapService;
        private readonly DatabaseService dbService;
        private GMapControl mapControl;

        public DriverMapManager(MapService mapService, DatabaseService dbService)
        {
            this.mapService = mapService ?? throw new ArgumentNullException(nameof(mapService));
            this.dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
        }

        public void InitializeMap(GMapControl mapControl, double latitude, double longitude)
        {
            this.mapControl = mapControl ?? throw new ArgumentNullException(nameof(mapControl));
            mapService.InitializeGoogleMaps(this.mapControl, latitude, longitude);
        }

        public async Task DisplayRouteOnMapAsync(Vehicle vehicle, List<Passenger> passengers)
        {
            if (mapControl == null || vehicle == null)
            {
                return;
            }

            try
            {
                mapControl.Overlays.Clear();

                // Create all overlays
                var vehiclesOverlay = new GMapOverlay("vehicles");
                var passengersOverlay = new GMapOverlay("passengers");
                var routesOverlay = new GMapOverlay("routes");
                var destinationOverlay = new GMapOverlay("destination");

                // Add markers for vehicle and passengers
                AddVehicleToMap(vehicle, vehiclesOverlay);
                AddPassengersToMap(passengers, passengersOverlay);

                // Add all overlays to map
                mapControl.Overlays.Add(vehiclesOverlay);
                mapControl.Overlays.Add(passengersOverlay);
                mapControl.Overlays.Add(routesOverlay);
                mapControl.Overlays.Add(destinationOverlay);

                // Get destination and add route
                var destination = await dbService.GetDestinationAsync();

                // Add destination marker
                var destMarker = MapOverlays.CreateDestinationMarker(
                    destination.Latitude, destination.Longitude);
                destinationOverlay.Markers.Add(destMarker);

                // Create and add route
                await CreateAndAddRouteAsync(vehicle, passengers, destination, routesOverlay);

                // Force map refresh
                mapControl.Zoom = mapControl.Zoom;
                mapControl.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error displaying route: {ex.Message}",
                    "Map Display Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AddVehicleToMap(Vehicle vehicle, GMapOverlay vehiclesOverlay)
        {
            if (vehicle.StartLatitude == 0 && vehicle.StartLongitude == 0)
            {
                return;
            }

            var vehicleMarker = MapOverlays.CreateVehicleMarker(vehicle);
            vehiclesOverlay.Markers.Add(vehicleMarker);

            mapControl.Position = new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude);
            mapControl.Zoom = 12;
        }

        private void AddPassengersToMap(List<Passenger> passengers, GMapOverlay passengersOverlay)
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

                var passengerMarker = MapOverlays.CreatePassengerMarker(passenger);
                passengersOverlay.Markers.Add(passengerMarker);
            }
        }

        private async Task CreateAndAddRouteAsync(
            Vehicle vehicle,
            List<Passenger> passengers,
            dynamic destination,
            GMapOverlay routesOverlay)
        {
            try
            {
                List<PointLatLng> routePoints = new List<PointLatLng>();

                // Add vehicle starting point
                routePoints.Add(new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude));

                // Add passenger points
                if (passengers != null)
                {
                    foreach (var passenger in passengers)
                    {
                        if (passenger != null)
                        {
                            routePoints.Add(new PointLatLng(passenger.Latitude, passenger.Longitude));
                        }
                    }
                }

                // Add destination point
                routePoints.Add(new PointLatLng(destination.Latitude, destination.Longitude));

                if (routePoints.Count < 2)
                {
                    return;
                }

                // Try to get detailed route from Google API
                List<PointLatLng> finalRoutePoints;
                try
                {
                    finalRoutePoints = await mapService.GetGoogleDirectionsAsync(routePoints);

                    if (finalRoutePoints == null || finalRoutePoints.Count == 0)
                    {
                        finalRoutePoints = routePoints;
                    }
                }
                catch (Exception ex)
                {
                    // If Google API fails, use direct route between points
                    finalRoutePoints = routePoints;
                    Console.WriteLine($"Using direct route. Google API error: {ex.Message}");
                }

                // Create and add route to overlay
                var route = MapOverlays.CreateRoute(finalRoutePoints, "DriverRoute", Color.Blue, 4);
                routesOverlay.Routes.Add(route);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating route: {ex.Message}",
                    "Route Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void DisplayRouteOnMap(Vehicle vehicle, List<Passenger> passengers)
        {
            // Synchronous wrapper for backward compatibility
            Task.Run(async () =>
            {
                try
                {
                    await DisplayRouteOnMapAsync(vehicle, passengers);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in DisplayRouteOnMap: {ex.Message}");
                }
            });
        }
    }
}