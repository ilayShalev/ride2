using GMap.NET.WindowsForms;
using GMap.NET;
using RideMatchProject.Models;
using RideMatchProject.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.RoutingServiceClasses
{
    /// <summary>
    /// Calculates and creates routes for vehicles
    /// </summary>
    public class RoutingPathCalculator
    {
        private readonly MapService _mapService;
        private readonly DestinationInfo _destination;

        public RoutingPathCalculator(MapService mapService, DestinationInfo destination)
        {
            _mapService = mapService;
            _destination = destination;
        }

        public async Task<Dictionary<int, RouteDetails>> GetGoogleRoutesAsync(
            GMapControl mapControl, Solution solution)
        {
            if (solution == null)
            {
                return new Dictionary<int, RouteDetails>();
            }

            var routeDetails = new Dictionary<int, RouteDetails>();
            GMapOverlay routesOverlay = null;

            if (mapControl != null)
            {
                routesOverlay = PrepareMapOverlay(mapControl);
            }

            var colors = mapControl != null ? MapOverlays.GetRouteColors() : null;

            for (int i = 0; i < solution.Vehicles.Count; i++)
            {
                var vehicle = solution.Vehicles[i];

                if (vehicle.AssignedPassengers.Count == 0)
                {
                    continue;
                }

                await ProcessVehicleRoute(vehicle, routeDetails, routesOverlay, colors, i);
            }

            if (mapControl != null)
            {
                RefreshMap(mapControl);
            }

            return routeDetails;
        }

        private GMapOverlay PrepareMapOverlay(GMapControl mapControl)
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

        private async Task ProcessVehicleRoute(Vehicle vehicle, Dictionary<int, RouteDetails> routeDetails,
            GMapOverlay routesOverlay, Color[] colors, int vehicleIndex)
        {
            var routeDetail = await _mapService.GetRouteDetailsAsync(
                vehicle, _destination.Latitude, _destination.Longitude);

            if (routeDetail != null)
            {
                routeDetails[vehicle.Id] = routeDetail;
                vehicle.TotalDistance = routeDetail.TotalDistance;
                vehicle.TotalTime = routeDetail.TotalTime;
            }

            if (routesOverlay == null)
            {
                return;
            }

            var points = await CreateAndAddRoute(vehicle, routesOverlay, colors, vehicleIndex);

            // Store the route path in the route details
            if (routeDetail != null && points != null && points.Count > 0)
            {
                routeDetail.RoutePath = points;
            }
        }

        private async Task<List<PointLatLng>> CreateAndAddRoute(Vehicle vehicle, GMapOverlay routesOverlay,
            Color[] colors, int vehicleIndex)
        {
            var points = CreateInitialRoutePoints(vehicle);
            var routePoints = await _mapService.GetGoogleDirectionsAsync(points);

            if (routePoints != null && routePoints.Count > 0)
            {
                points = routePoints;
            }

            var routeColor = colors[vehicleIndex % colors.Length];
            var route = MapOverlays.CreateRoute(points, $"Route {vehicleIndex}", routeColor);
            routesOverlay.Routes.Add(route);

            return points; // Return the points so they can be stored
        }
        private List<PointLatLng> CreateInitialRoutePoints(Vehicle vehicle)
        {
            var points = new List<PointLatLng>
            {
                new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude)
            };

            foreach (var passenger in vehicle.AssignedPassengers)
            {
                points.Add(new PointLatLng(passenger.Latitude, passenger.Longitude));
            }

            points.Add(new PointLatLng(_destination.Latitude, _destination.Longitude));

            return points;
        }

        private void RefreshMap(GMapControl mapControl)
        {
            mapControl.Zoom = mapControl.Zoom; // Force refresh
        }

        public Dictionary<int, RouteDetails> CalculateEstimatedRouteDetails(Solution solution)
        {
            if (solution == null)
            {
                return new Dictionary<int, RouteDetails>();
            }

            var routeDetails = new Dictionary<int, RouteDetails>();

            foreach (var vehicle in solution.Vehicles)
            {
                if (vehicle.AssignedPassengers.Count == 0)
                {
                    continue;
                }

                var detail = _mapService.EstimateRouteDetails(
                    vehicle, _destination.Latitude, _destination.Longitude);

                if (detail != null)
                {
                    routeDetails[vehicle.Id] = detail;
                    vehicle.TotalDistance = detail.TotalDistance;
                    vehicle.TotalTime = detail.TotalTime;
                }
            }

            return routeDetails;
        }
    }
}
