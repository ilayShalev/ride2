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
    /// Manages the generation and evolution of a population of solutions for the ride-matching problem.
    /// This class is responsible for creating initial solutions, assigning passengers to vehicles, and
    /// generating diverse solutions using different strategies (random, greedy, and even distribution).
    /// </summary>
    public class PopulationManager
    {
        private readonly ProblemData _problemData;
        private readonly RouteCalculator _routeCalculator;
        private readonly SolutionEvaluator _evaluator;
        private readonly Random _random = new Random();

        /// <summary>
        /// Initializes a new instance of the <see cref="PopulationManager"/> class.
        /// </summary>
        /// <param name="problemData">The problem data containing passengers, vehicles, and destination details.</param>
        /// <param name="routeCalculator">The utility for calculating routes and distances for vehicles.</param>
        /// <param name="evaluator">The evaluator for assessing the quality of a solution.</param>
        /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
        public PopulationManager(ProblemData problemData, RouteCalculator routeCalculator, SolutionEvaluator evaluator)
        {
            _problemData = problemData ?? throw new ArgumentNullException(nameof(problemData));
            _routeCalculator = routeCalculator ?? throw new ArgumentNullException(nameof(routeCalculator));
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }

        /// <summary>
        /// Generates an initial population of solutions for the ride-matching problem.
        /// The population includes one greedy solution, one even-distribution solution, and additional random solutions
        /// to reach the specified population size.
        /// </summary>
        /// <param name="populationSize">The desired number of solutions in the population.</param>
        /// <returns>A list of <see cref="Solution"/> objects representing the initial population.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="populationSize"/> is less than 2.</exception>
        public List<Solution> GenerateInitialPopulation(int populationSize)
        {
            if (populationSize < 2)
                throw new ArgumentOutOfRangeException(nameof(populationSize), "Population size must be at least 2.");

            var result = new List<Solution>();

            // Add a greedy solution to ensure at least one high-quality solution
            result.Add(CreateGreedySolution());
            // Add an even-distribution solution to balance passenger assignments
            result.Add(CreateEvenDistributionSolution());

            // Generate additional random solutions to fill the population
            while (result.Count < populationSize)
            {
                var solution = new Solution { Vehicles = _routeCalculator.DeepCopyVehicles() };
                // Randomly shuffle passengers to create diversity
                var unassigned = _problemData.Passengers.OrderBy(x => _random.Next()).ToList();

                // Assign passengers randomly to vehicles
                AssignPassengersToVehicles(solution, unassigned);
                // Assign any remaining passengers to minimize unassigned passengers
                AssignRemainingPassengers(solution, unassigned);

                // Evaluate the solution's quality
                solution.Score = _evaluator.Evaluate(solution);
                result.Add(solution);
            }

            return result;
        }

        /// <summary>
        /// Assigns passengers to vehicles randomly, respecting vehicle capacity constraints.
        /// </summary>
        /// <param name="solution">The solution containing vehicles to assign passengers to.</param>
        /// <param name="unassigned">The list of unassigned passengers.</param>
        /// <remarks>
        /// This method randomly determines how many passengers to assign to each vehicle, up to the vehicle's capacity
        /// or the number of remaining unassigned passengers. It removes assigned passengers from the unassigned list.
        /// </remarks>
        private void AssignPassengersToVehicles(Solution solution, List<Passenger> unassigned)
        {
            foreach (var vehicle in solution.Vehicles)
            {
                // Determine the maximum number of passengers that can be assigned
                int maxToAssign = Math.Min(vehicle.Capacity, unassigned.Count);
                // Randomly choose how many passengers to assign (0 to maxToAssign)
                int toAssign = _random.Next(0, maxToAssign + 1);

                // Assign the chosen number of passengers
                for (int j = 0; j < toAssign && unassigned.Count > 0; j++)
                {
                    var passenger = unassigned[0];
                    vehicle.AssignedPassengers.Add(passenger);
                    unassigned.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Assigns any remaining unassigned passengers to vehicles, prioritizing vehicles with fewer passengers
        /// and minimizing additional travel distance.
        /// </summary>
        /// <param name="solution">The solution containing vehicles to assign passengers to.</param>
        /// <param name="unassigned">The list of unassigned passengers.</param>
        /// <remarks>
        /// This method ensures all passengers are assigned by selecting the vehicle with the fewest assigned passengers
        /// and the least additional distance for each passenger.
        /// </remarks>
        private void AssignRemainingPassengers(Solution solution, List<Passenger> unassigned)
        {
            foreach (var passenger in unassigned.ToList())
            {
                // Find the best vehicle based on passenger count and additional distance
                var vehicle = solution.Vehicles
                    .OrderBy(v => v.AssignedPassengers.Count)
                    .ThenBy(v => _routeCalculator.CalculateAdditionalDistance(v, passenger))
                    .FirstOrDefault();

                if (vehicle != null)
                {
                    vehicle.AssignedPassengers.Add(passenger);
                    unassigned.Remove(passenger);
                }
            }
        }

        /// <summary>
        /// Creates a greedy solution by assigning passengers to vehicles based on proximity and vehicle capacity.
        /// </summary>
        /// <returns>A <see cref="Solution"/> representing the greedy assignment of passengers.</returns>
        /// <remarks>
        /// Passengers are sorted by their distance to the destination (farthest first) and assigned to the closest
        /// available vehicle with capacity. If no vehicles have capacity, passengers are assigned to the least
        /// overloaded vehicle.
        /// </remarks>
        public Solution CreateGreedySolution()
        {
            var solution = new Solution { Vehicles = _routeCalculator.DeepCopyVehicles() };
            var remainingPassengers = SortPassengersByDistanceToDestination();

            foreach (var passenger in remainingPassengers)
            {
                var availableVehicles = solution.Vehicles
                    .Where(v => v.AssignedPassengers.Count < v.Capacity)
                    .ToList();

                if (availableVehicles.Count == 0)
                {
                    // No vehicles have capacity; assign to the least overloaded vehicle
                    AssignToLeastOverloaded(solution, passenger);
                }
                else
                {
                    // Assign to the closest vehicle with available capacity
                    AssignToClosestVehicle(availableVehicles, passenger);
                }
            }

            // Evaluate the solution's quality
            solution.Score = _evaluator.Evaluate(solution);
            return solution;
        }

        /// <summary>
        /// Sorts passengers by their distance to the destination in descending order.
        /// </summary>
        /// <returns>A list of <see cref="Passenger"/> objects sorted by distance to the destination.</returns>
        /// <remarks>
        /// This method uses the <see cref="GeoCalculator.CalculateDistance"/> utility to compute distances
        /// between each passenger's location and the destination.
        /// </remarks>
        private List<Passenger> SortPassengersByDistanceToDestination()
        {
            return _problemData.Passengers
                .OrderByDescending(p => GeoCalculator.CalculateDistance(
                    p.Latitude, p.Longitude,
                    _problemData.DestinationLat, _problemData.DestinationLng))
                .ToList();
        }

        /// <summary>
        /// Assigns a passenger to the vehicle with the fewest assigned passengers.
        /// </summary>
        /// <param name="solution">The solution containing vehicles.</param>
        /// <param name="passenger">The passenger to assign.</param>
        /// <remarks>
        /// This method is used when no vehicles have remaining capacity, ensuring all passengers are assigned
        /// by overloading the least busy vehicle.
        /// </remarks>
        private void AssignToLeastOverloaded(Solution solution, Passenger passenger)
        {
            var vehicle = solution.Vehicles.OrderBy(v => v.AssignedPassengers.Count).First();
            vehicle.AssignedPassengers.Add(passenger);
        }

        /// <summary>
        /// Assigns a passenger to the closest vehicle among those with available capacity.
        /// </summary>
        /// <param name="availableVehicles">The list of vehicles with available capacity.</param>
        /// <param name="passenger">The passenger to assign.</param>
        /// <remarks>
        /// Proximity is determined using the <see cref="GeoCalculator.CalculateDistance"/> utility to compute
        /// the distance between the vehicle's starting location and the passenger's location.
        /// </remarks>
        private void AssignToClosestVehicle(List<Vehicle> availableVehicles, Passenger passenger)
        {
            var closestVehicle = availableVehicles
                .OrderBy(v => GeoCalculator.CalculateDistance(
                    v.StartLatitude, v.StartLongitude, passenger.Latitude, passenger.Longitude))
                .First();

            closestVehicle.AssignedPassengers.Add(passenger);
        }

        /// <summary>
        /// Creates a solution that distributes passengers as evenly as possible across vehicles.
        /// </summary>
        /// <returns>A <see cref="Solution"/> with passengers distributed evenly among vehicles.</returns>
        /// <remarks>
        /// This method calculates an ideal number of passengers per vehicle and assigns the nearest passengers
        /// to each vehicle. Remaining passengers are assigned to vehicles with capacity or the least loaded vehicles.
        /// </remarks>
        public Solution CreateEvenDistributionSolution()
        {
            var solution = new Solution { Vehicles = _routeCalculator.DeepCopyVehicles() };
            var remainingPassengers = _problemData.Passengers.ToList();

            // Calculate the target number of passengers per vehicle
            int passengersPerVehicle = CalculatePassengersPerVehicle();

            // Assign passengers to each vehicle
            foreach (var vehicle in solution.Vehicles)
            {
                AssignNearestPassengersToVehicle(vehicle, remainingPassengers, passengersPerVehicle);
            }

            // Handle any remaining passengers
            AssignRemainingPassengersToAvailableVehicles(solution, remainingPassengers);

            // Evaluate the solution's quality
            solution.Score = _evaluator.Evaluate(solution);
            return solution;
        }

        /// <summary>
        /// Calculates the ideal number of passengers to assign to each vehicle for even distribution.
        /// </summary>
        /// <returns>The number of passengers per vehicle.</returns>
        /// <remarks>
        /// This method considers the total number of passengers, total vehicle capacity, and the minimum
        /// vehicle capacity to determine a fair distribution.
        /// </remarks>
        private int CalculatePassengersPerVehicle()
        {
            int totalCapacity = _problemData.Vehicles.Sum(v => v.Capacity);
            return Math.Min(
                _problemData.Passengers.Count / _problemData.Vehicles.Count,
                Math.Min(
                    totalCapacity / _problemData.Vehicles.Count,
                    _problemData.Vehicles.Min(v => v.Capacity)));
        }

        /// <summary>
        /// Assigns the nearest passengers to a vehicle up to a specified number.
        /// </summary>
        /// <param name="vehicle">The vehicle to assign passengers to.</param>
        /// <param name="remainingPassengers">The list of unassigned passengers.</param>
        /// <param name="passengersPerVehicle">The target number of passengers to assign.</param>
        /// <remarks>
        /// Passengers are sorted by proximity to the vehicle's starting location, and the closest ones are assigned.
        /// Assigned passengers are removed from the remaining passengers list.
        /// </remarks>
        private void AssignNearestPassengersToVehicle(Vehicle vehicle, List<Passenger> remainingPassengers,
            int passengersPerVehicle)
        {
            var nearestPassengers = remainingPassengers
                .OrderBy(p => GeoCalculator.CalculateDistance(
                    vehicle.StartLatitude, vehicle.StartLongitude, p.Latitude, p.Longitude))
                .Take(passengersPerVehicle)
                .ToList();

            foreach (var passenger in nearestPassengers)
            {
                vehicle.AssignedPassengers.Add(passenger);
                remainingPassengers.Remove(passenger);
            }
        }

        /// <summary>
        /// Assigns any remaining passengers to vehicles, first to those with capacity and then to the least loaded.
        /// </summary>
        /// <param name="solution">The solution containing vehicles.</param>
        /// <param name="remainingPassengers">The list of unassigned passengers.</param>
        /// <remarks>
        /// This method ensures all passengers are assigned by prioritizing vehicles with available capacity
        /// and then distributing any remaining passengers to the least loaded vehicles.
        /// </remarks>
        private void AssignRemainingPassengersToAvailableVehicles(Solution solution, List<Passenger> remainingPassengers)
        {
            AssignToVehiclesWithCapacity(solution, remainingPassengers);
            AssignToLeastLoadedVehicles(solution, remainingPassengers);
        }

        /// <summary>
        /// Assigns passengers to vehicles that still have available capacity.
        /// </summary>
        /// <param name="solution">The solution containing vehicles.</param>
        /// <param name="remainingPassengers">The list of unassigned passengers.</param>
        /// <remarks>
        /// Passengers are assigned to the closest vehicle with available capacity, based on the distance
        /// between the vehicle's starting location and the passenger's location.
        /// </remarks>
        private void AssignToVehiclesWithCapacity(Solution solution, List<Passenger> remainingPassengers)
        {
            foreach (var passenger in remainingPassengers.ToList())
            {
                var availableVehicles = solution.Vehicles
                    .Where(v => v.AssignedPassengers.Count < v.Capacity)
                    .ToList();

                if (availableVehicles.Count > 0)
                {
                    var bestVehicle = availableVehicles
                        .OrderBy(v => GeoCalculator.CalculateDistance(
                            v.StartLatitude, v.StartLongitude, passenger.Latitude, passenger.Longitude))
                        .First();

                    bestVehicle.AssignedPassengers.Add(passenger);
                    remainingPassengers.Remove(passenger);
                }
            }
        }

        /// <summary>
        /// Assigns any remaining passengers to the least loaded vehicles.
        /// </summary>
        /// <param name="solution">The solution containing vehicles.</param>
        /// <param name="remainingPassengers">The list of unassigned passengers.</param>
        /// <remarks>
        /// This method is used as a fallback to ensure all passengers are assigned, even if it means overloading
        /// vehicles. It prioritizes vehicles with the fewest assigned passengers.
        /// </remarks>
        private void AssignToLeastLoadedVehicles(Solution solution, List<Passenger> remainingPassengers)
        {
            foreach (var passenger in remainingPassengers)
            {
                var vehicle = solution.Vehicles
                    .OrderBy(v => v.AssignedPassengers.Count)
                    .First();

                vehicle.AssignedPassengers.Add(passenger);
            }
        }
    }
}