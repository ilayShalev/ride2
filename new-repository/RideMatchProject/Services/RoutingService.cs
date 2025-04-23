using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using RideMatchProject.Models;
using RideMatchProject.UI;
using RideMatchProject.Utilities;
using System.Windows.Forms;

namespace RideMatchProject.Services
{
    /// <summary>
    /// Main facade service for routing operations
    /// </summary>
    public class RoutingService
    {
        private readonly MapService _mapService;
        private readonly DestinationInfo _destination;
        private readonly MapDisplayManager _displayManager;
        private readonly RoutingPathCalculator _routeCalculator;
        private readonly SolutionValidator _validator;

        public Dictionary<int, RouteDetails> VehicleRouteDetails { get; private set; }

        public RoutingService(MapService mapService, double destinationLat, double destinationLng)
        {
            _mapService = mapService;
            _destination = new DestinationInfo(destinationLat, destinationLng);
            _displayManager = new MapDisplayManager(_mapService);
            _routeCalculator = new RoutingPathCalculator(_mapService, _destination);
            _validator = new SolutionValidator();
            VehicleRouteDetails = new Dictionary<int, RouteDetails>();
        }

        public void DisplayDataOnMap(GMapControl mapControl, List<Passenger> passengers, List<Vehicle> vehicles)
        {
            _displayManager.DisplayDataOnMap(mapControl, passengers, vehicles, _destination);
        }

        public void DisplaySolutionOnMap(GMapControl mapControl, Solution solution)
        {
            _displayManager.DisplaySolutionOnMap(mapControl, solution, _destination);
        }

        public async Task GetGoogleRoutesAsync(GMapControl mapControl, Solution solution)
        {
            VehicleRouteDetails = await _routeCalculator.GetGoogleRoutesAsync(mapControl, solution);
        }

        public void CalculateEstimatedRouteDetails(Solution solution)
        {
            VehicleRouteDetails = _routeCalculator.CalculateEstimatedRouteDetails(solution);
        }

        public string ValidateSolution(Solution solution, List<Passenger> allPassengers)
        {
            return _validator.ValidateSolution(solution, allPassengers);
        }
    }

    /// <summary>
    /// Stores destination information
    /// </summary>
    public class DestinationInfo
    {
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }

        public DestinationInfo(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }
    }

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
            foreach (var passenger in passengers)
            {
                var marker = MapOverlays.CreatePassengerMarker(passenger);
                passengersOverlay.Markers.Add(marker);
            }
        }

        private void AddVehicleMarkers(GMapOverlay vehiclesOverlay, List<Vehicle> vehicles)
        {
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

                var points = CreateRoutePoints(vehicle, destination);
                var routeColor = colors[i % colors.Length];
                var route = MapOverlays.CreateRoute(points, $"Route {i}", routeColor);

                routesOverlay.Routes.Add(route);
            }
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

            await CreateAndAddRoute(vehicle, routesOverlay, colors, vehicleIndex);
        }

        private async Task CreateAndAddRoute(Vehicle vehicle, GMapOverlay routesOverlay,
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

    /// <summary>
    /// Validates solutions against constraints
    /// </summary>
    public class SolutionValidator
    {
        public string ValidateSolution(Solution solution, List<Passenger> allPassengers)
        {
            if (solution == null)
            {
                return "No solution to validate.";
            }

            var validation = PerformValidation(solution, allPassengers);
            var statistics = CalculateStatistics(solution, validation.AssignedPassengers.Count,
                allPassengers.Count);

            var report = GenerateReport(validation, statistics);
            return report;
        }

        private (HashSet<int> AssignedPassengers, bool CapacityExceeded,
            List<int> PassengersWithMultipleAssignments) PerformValidation(
                Solution solution, List<Passenger> allPassengers)
        {
            var assignedPassengers = new HashSet<int>();
            var capacityExceeded = false;
            var passengersWithMultipleAssignments = new List<int>();

            foreach (var vehicle in solution.Vehicles)
            {
                if (vehicle.AssignedPassengers.Count > vehicle.Capacity)
                {
                    capacityExceeded = true;
                }

                CheckPassengerAssignments(vehicle, assignedPassengers, passengersWithMultipleAssignments);
            }

            return (assignedPassengers, capacityExceeded, passengersWithMultipleAssignments);
        }

        private void CheckPassengerAssignments(Vehicle vehicle, HashSet<int> assignedPassengers,
            List<int> passengersWithMultipleAssignments)
        {
            foreach (var passenger in vehicle.AssignedPassengers)
            {
                if (assignedPassengers.Contains(passenger.Id))
                {
                    passengersWithMultipleAssignments.Add(passenger.Id);
                }
                else
                {
                    assignedPassengers.Add(passenger.Id);
                }
            }
        }

        private (double TotalDistance, double TotalTime, double AverageTime, int UsedVehicles)
            CalculateStatistics(Solution solution, int assignedCount, int totalPassengers)
        {
            double totalDistance = solution.Vehicles.Sum(v => v.TotalDistance);
            double totalTime = solution.Vehicles.Sum(v => v.TotalTime);
            int usedVehicles = solution.Vehicles.Count(v => v.AssignedPassengers.Count > 0);
            double averageTime = usedVehicles > 0 ? totalTime / usedVehicles : 0;

            return (totalDistance, totalTime, averageTime, usedVehicles);
        }

        private string GenerateReport(
            (HashSet<int> AssignedPassengers, bool CapacityExceeded,
            List<int> PassengersWithMultipleAssignments) validation,
            (double TotalDistance, double TotalTime, double AverageTime, int UsedVehicles) statistics)
        {
            StringBuilder report = new StringBuilder();

            bool allAssigned = validation.AssignedPassengers.Count == validation.AssignedPassengers.Count;

            report.AppendLine("Validation Results:");
            report.AppendLine($"All passengers assigned: {allAssigned}");
            report.AppendLine($"Assigned passengers: {validation.AssignedPassengers.Count}/" +
                $"{validation.AssignedPassengers.Count}");
            report.AppendLine($"Capacity exceeded: {validation.CapacityExceeded}");

            AppendMultipleAssignmentsInfo(report, validation.PassengersWithMultipleAssignments);
            AppendStatisticsInfo(report, statistics);

            return report.ToString();
        }

        private void AppendMultipleAssignmentsInfo(StringBuilder report,
            List<int> passengersWithMultipleAssignments)
        {
            if (passengersWithMultipleAssignments.Count > 0)
            {
                report.AppendLine($"Passengers with multiple assignments: " +
                    $"{passengersWithMultipleAssignments.Count}");
                report.AppendLine($"IDs: {string.Join(", ", passengersWithMultipleAssignments)}");
            }
        }

        private void AppendStatisticsInfo(StringBuilder report,
            (double TotalDistance, double TotalTime, double AverageTime, int UsedVehicles) statistics)
        {
            report.AppendLine();
            report.AppendLine("Statistics:");
            report.AppendLine($"Total distance: {statistics.TotalDistance:F2} km");
            report.AppendLine($"Total time: {statistics.TotalTime:F2} minutes");
            report.AppendLine($"Average time per vehicle: {statistics.AverageTime:F2} minutes");
        }
    }
}