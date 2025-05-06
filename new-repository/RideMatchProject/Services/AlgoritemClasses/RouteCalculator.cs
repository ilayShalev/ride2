using RideMatchProject.Models;
using RideMatchProject.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.AlgoritemClasses
{
    /// <summary>
    /// A utility class for calculating route metrics for vehicles and passengers in a ride-matching system.
    /// This class provides methods to compute distances for vehicle routes, additional distances for new passengers,
    /// and exact metrics for a given solution. It relies on geographic coordinates and uses the GeoCalculator
    /// utility for distance calculations.
    /// </summary>
    public class RouteCalculator
    {
        private readonly ProblemData _problemData;

        /// <summary>
        /// Initializes a new instance of the <see cref="RouteCalculator"/> class with the specified problem data.
        /// </summary>
        /// <param name="problemData">The problem data containing vehicles, destination coordinates, and other relevant information.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="problemData"/> is null.</exception>
        public RouteCalculator(ProblemData problemData)
        {
            _problemData = problemData ?? throw new ArgumentNullException(nameof(problemData));
        }

        /// <summary>
        /// Calculates the total distance for a vehicle's route, including pickups for all assigned passengers
        /// and the final trip to the destination.
        /// </summary>
        /// <param name="vehicle">The vehicle for which to calculate the route metrics.</param>
        /// <returns>
        /// The total distance in kilometers (or the unit used by <see cref="GeoCalculator.CalculateDistance"/>)
        /// for the vehicle's route. Returns 0 if the vehicle has no assigned passengers.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="vehicle"/> is null.</exception>
        public double CalculateRouteMetrics(Vehicle vehicle)
        {
            if (vehicle == null)
                throw new ArgumentNullException(nameof(vehicle));

            if (vehicle.AssignedPassengers.Count == 0)
            {
                return 0;
            }

            double totalDistance = 0;
            double currentLat = vehicle.StartLatitude;
            double currentLng = vehicle.StartLongitude;

            // Calculate distance for picking up each passenger sequentially
            totalDistance = AddDistanceForPassengerPickups(vehicle, currentLat, currentLng, totalDistance);

            // Add the distance from the last passenger to the destination
            totalDistance = AddDistanceToDestination(vehicle, totalDistance);

            return totalDistance;
        }

        /// <summary>
        /// Calculates the cumulative distance for picking up all passengers assigned to a vehicle,
        /// starting from the vehicle's current position.
        /// </summary>
        /// <param name="vehicle">The vehicle whose passenger pickup distances are calculated.</param>
        /// <param name="currentLat">The current latitude of the vehicle or last visited point.</param>
        /// <param name="currentLng">The current longitude of the vehicle or last visited point.</param>
        /// <param name="totalDistance">The initial total distance to which pickup distances are added.</param>
        /// <returns>The updated total distance after adding distances for all passenger pickups.</returns>
        /// <remarks>
        /// This method iterates through each assigned passenger, calculating the distance from the current
        /// position to the passenger's location, and updates the current position to the passenger's coordinates.
        /// </remarks>
        private double AddDistanceForPassengerPickups(Vehicle vehicle, double currentLat, double currentLng, double totalDistance)
        {
            double result = totalDistance;
            double lat = currentLat;
            double lng = currentLng;

            foreach (var passenger in vehicle.AssignedPassengers)
            {
                if (passenger == null)
                    continue;

                // Calculate distance from current position to passenger's location
                double legDistance = GeoCalculator.CalculateDistance(lat, lng, passenger.Latitude, passenger.Longitude);
                result += legDistance;

                // Update current position to the passenger's location
                lat = passenger.Latitude;
                lng = passenger.Longitude;
            }

            return result;
        }

        /// <summary>
        /// Calculates the distance from the last passenger's location to the final destination.
        /// </summary>
        /// <param name="vehicle">The vehicle whose route is being calculated.</param>
        /// <param name="totalDistance">The current total distance to which the destination distance is added.</param>
        /// <returns>The updated total distance after adding the distance to the destination.</returns>
        /// <remarks>
        /// If the vehicle has no assigned passengers, the total distance is returned unchanged.
        /// The method uses the last passenger's coordinates as the starting point for the final leg to the destination.
        /// </remarks>
        private double AddDistanceToDestination(Vehicle vehicle, double totalDistance)
        {
            if (vehicle.AssignedPassengers.Count == 0)
            {
                return totalDistance;
            }

            var lastPassenger = vehicle.AssignedPassengers.Last();
            if (lastPassenger == null)
                return totalDistance;

            // Calculate distance from the last passenger to the destination
            double destDistance = GeoCalculator.CalculateDistance(
                lastPassenger.Latitude, lastPassenger.Longitude,
                _problemData.DestinationLat, _problemData.DestinationLng);

            return totalDistance + destDistance;
        }

        /// <summary>
        /// Calculates the additional distance incurred by adding a new passenger to a vehicle's route.
        /// </summary>
        /// <param name="vehicle">The vehicle to which the passenger might be added.</param>
        /// <param name="passenger">The passenger to be added to the vehicle's route.</param>
        /// <returns>
        /// The additional distance in kilometers (or the unit used by <see cref="GeoCalculator.CalculateDistance"/>)
        /// required to include the new passenger in the route.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="vehicle"/> or <paramref name="passenger"/> is null.</exception>
        /// <remarks>
        /// If the vehicle has no assigned passengers, the method calculates the distance for an empty vehicle.
        /// Otherwise, it calculates the additional distance by comparing the current route to a route that includes the new passenger.
        /// </remarks>
        public double CalculateAdditionalDistance(Vehicle vehicle, Passenger passenger)
        {
            if (vehicle == null)
                throw new ArgumentNullException(nameof(vehicle));
            if (passenger == null)
                throw new ArgumentNullException(nameof(passenger));

            if (vehicle.AssignedPassengers.Count == 0)
            {
                return CalculateDistanceForEmptyVehicle(vehicle, passenger);
            }
            else
            {
                return CalculateDistanceWithPassenger(vehicle, passenger);
            }
        }

        /// <summary>
        /// Calculates the total distance for an empty vehicle to pick up a passenger and reach the destination.
        /// </summary>
        /// <param name="vehicle">The vehicle with no assigned passengers.</param>
        /// <param name="passenger">The passenger to be picked up.</param>
        /// <returns>The total distance from the vehicle's starting point to the passenger and then to the destination.</returns>
        /// <remarks>
        /// This method is used when a vehicle has no passengers and needs to calculate the full route distance
        /// for picking up a single passenger and proceeding to the destination.
        /// </remarks>
        private double CalculateDistanceForEmptyVehicle(Vehicle vehicle, Passenger passenger)
        {
            // Distance from vehicle's start to passenger
            double toPassenger = GeoCalculator.CalculateDistance(
                vehicle.StartLatitude, vehicle.StartLongitude,
                passenger.Latitude, passenger.Longitude);

            // Distance from passenger to destination
            double toDestination = GeoCalculator.CalculateDistance(
                passenger.Latitude, passenger.Longitude,
                _problemData.DestinationLat, _problemData.DestinationLng);

            return toPassenger + toDestination;
        }

        /// <summary>
        /// Calculates the additional distance required to include a new passenger in a vehicle that already has passengers.
        /// </summary>
        /// <param name="vehicle">The vehicle with one or more assigned passengers.</param>
        /// <param name="passenger">The new passenger to be added to the route.</param>
        /// <returns>The additional distance incurred by adding the new passenger to the route.</returns>
        /// <remarks>
        /// The method compares the current route's final leg (from the last passenger to the destination)
        /// with a new route that includes the new passenger before reaching the destination.
        /// </remarks>
        private double CalculateDistanceWithPassenger(Vehicle vehicle, Passenger passenger)
        {
            var lastPassenger = vehicle.AssignedPassengers.Last();
            if (lastPassenger == null)
                return 0;

            // Current distance from the last passenger to the destination
            double currentDistance = GeoCalculator.CalculateDistance(
                lastPassenger.Latitude, lastPassenger.Longitude,
                _problemData.DestinationLat, _problemData.DestinationLng);

            // New distance: from last passenger to new passenger, then to destination
            double newDistance = GeoCalculator.CalculateDistance(
                lastPassenger.Latitude, lastPassenger.Longitude,
                passenger.Latitude, passenger.Longitude) +
                GeoCalculator.CalculateDistance(
                passenger.Latitude, passenger.Longitude,
                _problemData.DestinationLat, _problemData.DestinationLng);

            // Additional distance is the difference between the new and current distances
            return newDistance - currentDistance;
        }

        /// <summary>
        /// Calculates the exact route metrics for all vehicles in a given solution and updates their TotalDistance properties.
        /// </summary>
        /// <param name="solution">The solution containing vehicles with assigned passengers.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="solution"/> is null.</exception>
        /// <remarks>
        /// This method iterates through all vehicles in the solution and updates their <see cref="Vehicle.TotalDistance"/>
        /// property by calling <see cref="CalculateRouteMetrics"/> for vehicles with assigned passengers.
        /// </remarks>
        public void CalculateExactMetrics(Solution solution)
        {
            if (solution == null)
                throw new ArgumentNullException(nameof(solution));

            foreach (var vehicle in solution.Vehicles)
            {
                if (vehicle == null)
                    continue;

                if (vehicle.AssignedPassengers.Count > 0)
                {
                    vehicle.TotalDistance = CalculateRouteMetrics(vehicle);
                }
                else
                {
                    vehicle.TotalDistance = 0;
                }
            }
        }

        /// <summary>
        /// Creates a deep copy of the vehicles in the problem data, excluding their assigned passengers.
        /// </summary>
        /// <returns>A list of new <see cref="Vehicle"/> objects with the same properties as the original vehicles,
        /// but with empty passenger lists and zero total distance.</returns>
        /// <remarks>
        /// This method is useful for creating a clean slate of vehicles for testing or optimization purposes,
        /// preserving the original vehicle data while resetting their passenger assignments and distances.
        /// </remarks>
        public List<Vehicle> DeepCopyVehicles()
        {
            return _problemData.Vehicles.Select(v => new Vehicle
            {
                Id = v.Id,
                Capacity = v.Capacity,
                StartLatitude = v.StartLatitude,
                StartLongitude = v.StartLongitude,
                StartAddress = v.StartAddress,
                DriverName = v.DriverName,
                AssignedPassengers = new List<Passenger>(),
                TotalDistance = 0
            }).ToList();
        }
    }
}