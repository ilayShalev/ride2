using RideMatchProject.Models;
using RideMatchProject.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.AdminClasses
{
    /// <summary>
    /// Service to handle scheduling operations
    /// </summary>
    public class SchedulingService
    {
        private readonly DatabaseService _dbService;
        private readonly MapService _mapService;
        private readonly AdminDataManager _dataManager;

        public SchedulingService(
            DatabaseService dbService,
            MapService mapService,
            AdminDataManager dataManager)
        {
            _dbService = dbService;
            _mapService = mapService;
            _dataManager = dataManager;
        }

        public async Task RunSchedulerAsync()
        {
            try
            {
                // Get destination information
                var destination = await _dbService.GetDestinationAsync();

                // Get available vehicles and passengers
                var vehicles = await _dbService.GetAvailableVehiclesAsync();
                var passengers = await _dbService.GetAvailablePassengersAsync();

                // Only run if there are passengers and vehicles
                if (passengers.Count == 0 || vehicles.Count == 0)
                {
                    await LogSkippedRunAsync(passengers.Count, vehicles.Count);
                    return;
                }

                // Run the algorithm
                var solution = await RunRoutingAlgorithmAsync(
                    passengers,
                    vehicles,
                    destination
                );

                if (solution == null)
                {
                    await _dbService.LogSchedulingRunAsync(
                        DateTime.Now,
                        "Failed",
                        0,
                        0,
                        "Algorithm failed to find a valid solution"
                    );
                    throw new Exception("Algorithm failed to find a valid solution");
                }

                // Calculate routes
                await CalculateRoutesAsync(solution, destination);

                // Save the solution
                await SaveSolutionAsync(solution);
            }
            catch (Exception ex)
            {
                // Log exception
                await LogErrorAsync(ex);
                throw;
            }
        }

        private async Task LogSkippedRunAsync(int passengerCount, int vehicleCount)
        {
            await _dbService.LogSchedulingRunAsync(
                DateTime.Now,
                "Skipped",
                0,
                0,
                $"Insufficient participants: {passengerCount} passengers, {vehicleCount} vehicles"
            );

            throw new Exception(
                $"No routes generated: {passengerCount} passengers, {vehicleCount} vehicles available"
            );
        }

        private async Task<Solution> RunRoutingAlgorithmAsync(
            List<Passenger> passengers,
            List<Vehicle> vehicles,
            (int Id, string Name, double Latitude, double Longitude, string Address, string TargetTime) destination)
        {
            // Create the solver
            var solver = new RideSharingGenetic(
                passengers,
                vehicles,
                200, // Population size
                destination.Latitude,
                destination.Longitude,
                GetTargetTimeInMinutes(destination.TargetTime)
            );

            // Run the algorithm
            return solver.Solve(150); // Generations
        }

        private async Task CalculateRoutesAsync(Solution solution,
          (int Id, string Name, double Latitude, double Longitude, string Address, string TargetTime) destination)
        {
            // Create a routing service
            var routingService = new RoutingService(
                _mapService,
                destination.Latitude,
                destination.Longitude
            );

            // First calculate estimated routes
            routingService.CalculateEstimatedRouteDetails(solution);

            // Always use Google API when running the scheduler
            try
            {
                // Get routes from Google API
                await routingService.GetGoogleRoutesAsync(null, solution);

                // After getting Google routes, transfer route paths to vehicles
                foreach (var vehicle in solution.Vehicles)
                {
                    if (routingService.VehicleRouteDetails.TryGetValue(vehicle.Id, out RouteDetails details) &&
                        details.RoutePath != null && details.RoutePath.Count > 0)
                    {
                        vehicle.RoutePath = details.RoutePath;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with estimated routes
                MessageDisplayer.ShowWarning(
                    $"Google API request failed: {ex.Message}. Using estimated routes instead.",
                    "API Error"
                );
            }

            // Calculate pickup times based on target arrival
            await CalculatePickupTimesAsync(solution, destination.TargetTime, routingService);
        }

        private async Task CalculatePickupTimesAsync(
            Solution solution,
            string targetTimeString,
            RoutingService routingService)
        {
            // Parse target arrival time
            if (!TimeSpan.TryParse(targetTimeString, out TimeSpan targetTime))
            {
                targetTime = new TimeSpan(8, 0, 0); // Default to 8:00 AM
            }

            // Get the target time as DateTime for today (we'll use just the time portion)
            DateTime targetDateTime = DateTime.Today.Add(targetTime);

            foreach (var vehicle in solution.Vehicles)
            {
                if (vehicle.AssignedPassengers == null || vehicle.AssignedPassengers.Count == 0)
                {
                    continue;
                }

                RouteDetails routeDetails = null;
                if (routingService.VehicleRouteDetails.ContainsKey(vehicle.Id))
                {
                    routeDetails = routingService.VehicleRouteDetails[vehicle.Id];
                }

                if (routeDetails == null)
                {
                    continue;
                }

                CalculateVehicleTimings(vehicle, routeDetails, targetDateTime);
            }
        }

        private void CalculateVehicleTimings(
            Vehicle vehicle,
            RouteDetails routeDetails,
            DateTime targetDateTime)
        {
            // Get total trip time from start to destination in minutes
            double totalTripTime = routeDetails.TotalTime;

            // Calculate when driver needs to start to arrive at destination at target time
            DateTime driverStartTime = targetDateTime.AddMinutes(-totalTripTime);

            // Store the driver's departure time
            vehicle.DepartureTime = driverStartTime.ToString("HH:mm");

            // Calculate each passenger's pickup time
            CalculatePassengerPickupTimes(vehicle, routeDetails, driverStartTime);
        }

        private void CalculatePassengerPickupTimes(
            Vehicle vehicle,
            RouteDetails routeDetails,
            DateTime driverStartTime)
        {
            for (int i = 0; i < vehicle.AssignedPassengers.Count; i++)
            {
                var passenger = vehicle.AssignedPassengers[i];

                // Find corresponding stop detail
                var stopDetail = routeDetails.StopDetails.FirstOrDefault(
                    s => s.PassengerId == passenger.Id
                );

                if (stopDetail != null)
                {
                    double cumulativeTime = stopDetail.CumulativeTime;

                    // Calculate pickup time based on driver start time plus cumulative time
                    DateTime pickupTime = driverStartTime.AddMinutes(cumulativeTime);
                    passenger.EstimatedPickupTime = pickupTime.ToString("HH:mm");
                }
            }
        }

        private async Task SaveSolutionAsync(Solution solution)
        {
            // Save the solution to database for tomorrow's date
            string tomorrowDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");
            int routeId = await _dbService.SaveSolutionAsync(solution, tomorrowDate);

            // Count assigned passengers and used vehicles
            int assignedPassengers = solution.Vehicles.Sum(
                v => v.AssignedPassengers?.Count ?? 0
            );
            int usedVehicles = solution.Vehicles.Count(
                v => v.AssignedPassengers?.Count > 0
            );

            // Log the scheduling run
            await _dbService.LogSchedulingRunAsync(
                DateTime.Now,
                "Success",
                usedVehicles,
                assignedPassengers,
                $"Created routes for {tomorrowDate}"
            );
        }

        private async Task LogErrorAsync(Exception ex)
        {
            try
            {
                await _dbService.LogSchedulingRunAsync(
                    DateTime.Now,
                    "Error",
                    0,
                    0,
                    ex.Message
                );
            }
            catch
            {
                // Just in case writing to the database also fails
            }
        }

        private int GetTargetTimeInMinutes(string targetTime)
        {
            // Convert a time string like "08:00:00" to minutes from midnight
            if (TimeSpan.TryParse(targetTime, out TimeSpan time))
            {
                return (int)time.TotalMinutes;
            }

            // Default to 8:00 AM (480 minutes)
            return 480;
        }
    }
}
