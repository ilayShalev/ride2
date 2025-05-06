using RideMatchProject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.AlgoritemClasses
{
    /// <summary>
    /// Handles mutation operations on ride-sharing solutions to improve optimization.
    /// Mutations modify solutions randomly to explore new configurations in a genetic algorithm.
    /// </summary>
    public class MutationOperator
    {
        private readonly ProblemData _problemData; // Input data defining the ride-sharing problem
        private readonly RouteCalculator _routeCalculator; // Calculates route metrics for vehicles
        private readonly SolutionEvaluator _evaluator; // Evaluates solution quality
        private readonly Random _random = new Random(); // Random number generator for mutation decisions

        /// <summary>
        /// Types of mutations that can be applied to a solution.
        /// </summary>
        private enum MutationType { Swap, Reorder, Move, OptimizeRoutes, OptimizeCapacity }

        /// <summary>
        /// Initializes a new instance of the MutationOperator class.
        /// </summary>
        /// <param name="problemData">The problem data containing passengers, vehicles, and constraints.</param>
        /// <param name="routeCalculator">The calculator for route distances and metrics.</param>
        /// <param name="evaluator">The evaluator for assessing solution quality.</param>
        public MutationOperator(ProblemData problemData, RouteCalculator routeCalculator, SolutionEvaluator evaluator)
        {
            _problemData = problemData ?? throw new ArgumentNullException(nameof(problemData));
            _routeCalculator = routeCalculator ?? throw new ArgumentNullException(nameof(routeCalculator));
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }

        /// <summary>
        /// Applies a random mutation to the given solution and updates its score.
        /// </summary>
        /// <param name="solution">The solution to mutate, containing vehicle-passenger assignments.</param>
        public void Mutate(Solution solution)
        {
            if (solution == null) throw new ArgumentNullException(nameof(solution));

            // Randomly select a mutation type
            MutationType mutationType = (MutationType)_random.Next(5);

            // Apply the selected mutation
            switch (mutationType)
            {
                case MutationType.Swap:
                    SwapPassengers(solution);
                    break;
                case MutationType.Reorder:
                    ReorderPassengers(solution);
                    break;
                case MutationType.Move:
                    MovePassenger(solution);
                    break;
                case MutationType.OptimizeRoutes:
                    OptimizeRoutes(solution);
                    break;
                case MutationType.OptimizeCapacity:
                    OptimizeCapacity(solution);
                    break;
            }

            // Re-evaluate the solution's score after mutation
            solution.Score = _evaluator.Evaluate(solution);
        }

        /// <summary>
        /// Swaps passengers between two randomly selected vehicles to explore new assignments.
        /// </summary>
        /// <param name="solution">The solution containing vehicles and their assigned passengers.</param>
        private void SwapPassengers(Solution solution)
        {
            // Select vehicles with at least one passenger
            var vehiclesWithPassengers = solution.Vehicles
                .Where(v => v.AssignedPassengers.Count > 0)
                .ToList();

            // Need at least two vehicles to swap passengers
            if (vehiclesWithPassengers.Count < 2)
            {
                return;
            }

            // Randomly select two different vehicles
            int idx1 = _random.Next(vehiclesWithPassengers.Count);
            int idx2 = _random.Next(vehiclesWithPassengers.Count - 1);
            if (idx2 >= idx1) idx2++;

            var vehicle1 = vehiclesWithPassengers[idx1];
            var vehicle2 = vehiclesWithPassengers[idx2];

            // Perform the swap
            PerformPassengerSwap(vehicle1, vehicle2);
        }

        /// <summary>
        /// Performs the actual swap of passengers between two vehicles.
        /// </summary>
        /// <param name="vehicle1">The first vehicle involved in the swap.</param>
        /// <param name="vehicle2">The second vehicle involved in the swap.</param>
        private void PerformPassengerSwap(Vehicle vehicle1, Vehicle vehicle2)
        {
            // Ensure both vehicles have passengers to swap
            if (vehicle1.AssignedPassengers.Count == 0 || vehicle2.AssignedPassengers.Count == 0)
            {
                return;
            }

            // Randomly select passengers from each vehicle
            int passengerIdx1 = _random.Next(vehicle1.AssignedPassengers.Count);
            int passengerIdx2 = _random.Next(vehicle2.AssignedPassengers.Count);

            var passenger1 = vehicle1.AssignedPassengers[passengerIdx1];
            var passenger2 = vehicle2.AssignedPassengers[passengerIdx2];

            // Swap the passengers
            vehicle1.AssignedPassengers[passengerIdx1] = passenger2;
            vehicle2.AssignedPassengers[passengerIdx2] = passenger1;
        }

        /// <summary>
        /// Reorders passengers within a randomly selected vehicle to optimize route efficiency.
        /// </summary>
        /// <param name="solution">The solution containing vehicles and their assigned passengers.</param>
        private void ReorderPassengers(Solution solution)
        {
            // Select vehicles with at least two passengers
            var vehiclesWithMultiplePassengers = solution.Vehicles
                .Where(v => v.AssignedPassengers.Count > 1)
                .ToList();

            if (vehiclesWithMultiplePassengers.Count == 0)
            {
                return;
            }

            // Randomly select a vehicle
            var vehicle = vehiclesWithMultiplePassengers[_random.Next(vehiclesWithMultiplePassengers.Count)];

            // Apply 2-opt for more than two passengers, or simple swap for exactly two
            if (vehicle.AssignedPassengers.Count > 2)
            {
                ReorderWithTwoOpt(vehicle);
            }
            else if (vehicle.AssignedPassengers.Count == 2)
            {
                // 50% chance to swap the two passengers
                if (_random.Next(2) == 0)
                {
                    var temp = vehicle.AssignedPassengers[0];
                    vehicle.AssignedPassengers[0] = vehicle.AssignedPassengers[1];
                    vehicle.AssignedPassengers[1] = temp;
                }
            }
        }

        /// <summary>
        /// Applies a 2-opt reorder by reversing a segment of the passenger list.
        /// </summary>
        /// <param name="vehicle">The vehicle whose passengers are to be reordered.</param>
        private void ReorderWithTwoOpt(Vehicle vehicle)
        {
            // Select two different positions
            int pos1 = _random.Next(vehicle.AssignedPassengers.Count);
            int pos2 = _random.Next(vehicle.AssignedPassengers.Count);

            if (pos1 == pos2)
            {
                return;
            }

            // Ensure pos1 is the smaller index
            if (pos1 > pos2)
            {
                int temp = pos1;
                pos1 = pos2;
                pos2 = temp;
            }

            // Reverse the segment between pos1 and pos2
            ReverseSegment(vehicle.AssignedPassengers, pos1, pos2);
        }

        /// <summary>
        /// Reverses a segment of the passenger list between two indices.
        /// </summary>
        /// <param name="passengers">The list of passengers to modify.</param>
        /// <param name="start">The starting index of the segment.</param>
        /// <param name="end">The ending index of the segment.</param>
        private void ReverseSegment(List<Passenger> passengers, int start, int end)
        {
            // Swap elements from start to end
            for (int i = 0; i < (end - start) / 2 + 1; i++)
            {
                if (start + i <= end - i)
                {
                    var temp = passengers[start + i];
                    passengers[start + i] = passengers[end - i];
                    passengers[end - i] = temp;
                }
            }
        }

        /// <summary>
        /// Moves a passenger from one vehicle to another to explore new assignments.
        /// </summary>
        /// <param name="solution">The solution containing vehicles and their assigned passengers.</param>
        private void MovePassenger(Solution solution)
        {
            // Select vehicles with at least one passenger
            var vehiclesWithPassengers = solution.Vehicles
                .Where(v => v.AssignedPassengers.Count > 0)
                .ToList();

            if (vehiclesWithPassengers.Count == 0)
            {
                return;
            }

            // Randomly select a source vehicle and passenger
            var sourceVehicle = vehiclesWithPassengers[_random.Next(vehiclesWithPassengers.Count)];
            int passengerIdx = _random.Next(sourceVehicle.AssignedPassengers.Count);
            var passenger = sourceVehicle.AssignedPassengers[passengerIdx];

            // Select potential target vehicles (excluding source)
            var targetOptions = solution.Vehicles
                .Where(v => v.Id != sourceVehicle.Id)
                .ToList();

            if (targetOptions.Count > 0)
            {
                // Move the passenger to a random target vehicle
                var targetVehicle = targetOptions[_random.Next(targetOptions.Count)];
                sourceVehicle.AssignedPassengers.RemoveAt(passengerIdx);
                targetVehicle.AssignedPassengers.Add(passenger);
            }
        }

        /// <summary>
        /// Optimizes the route of a vehicle with many passengers using a 2-opt search.
        /// </summary>
        /// <param name="solution">The solution containing vehicles and their assigned passengers.</param>
        private void OptimizeRoutes(Solution solution)
        {
            // Select vehicles with at least four passengers
            var vehiclesWithManyPassengers = solution.Vehicles
                .Where(v => v.AssignedPassengers.Count >= 4)
                .ToList();

            if (vehiclesWithManyPassengers.Count == 0)
            {
                return;
            }

            // Randomly select a vehicle
            var vehicle = vehiclesWithManyPassengers[_random.Next(vehiclesWithManyPassengers.Count)];
            PerformTwoOptSearch(vehicle);
        }

        /// <summary>
        /// Performs a 2-opt search to find a better passenger order for a vehicle.
        /// </summary>
        /// <param name="vehicle">The vehicle whose route is to be optimized.</param>
        private void PerformTwoOptSearch(Vehicle vehicle)
        {
            // Calculate initial route distance
            double bestDistance = _routeCalculator.CalculateRouteMetrics(vehicle);
            var bestOrder = new List<Passenger>(vehicle.AssignedPassengers);

            // Limit the number of swaps to avoid excessive computation
            int maxSwaps = Math.Min(10, vehicle.AssignedPassengers.Count * (vehicle.AssignedPassengers.Count - 1) / 2);
            var originalPassengers = vehicle.AssignedPassengers;

            // Try multiple swaps
            for (int swapCount = 0; swapCount < maxSwaps; swapCount++)
            {
                TrySwapAndUpdateBest(vehicle, ref bestDistance, ref bestOrder, originalPassengers);
            }

            // Apply the best route found
            vehicle.AssignedPassengers = bestOrder;
        }

        /// <summary>
        /// Tries a swap and updates the best route if an improvement is found.
        /// </summary>
        /// <param name="vehicle">The vehicle being optimized.</param>
        /// <param name="bestDistance">The best route distance found so far.</param>
        /// <param name="bestOrder">The best passenger order found so far.</param>
        /// <param name="originalPassengers">The original passenger order for restoration.</param>
        private void TrySwapAndUpdateBest(Vehicle vehicle, ref double bestDistance,
            ref List<Passenger> bestOrder, List<Passenger> originalPassengers)
        {
            // Select two positions for reversal
            int i = _random.Next(vehicle.AssignedPassengers.Count);
            int j = _random.Next(vehicle.AssignedPassengers.Count);

            if (i == j)
            {
                return;
            }

            // Ensure i is the smaller index
            if (i > j)
            {
                int temp = i;
                i = j;
                j = temp;
            }

            // Create a new order by reversing the segment
            var newOrder = new List<Passenger>(originalPassengers);
            newOrder.Reverse(i, j - i + 1);

            // Test the new order
            vehicle.AssignedPassengers = newOrder;
            double newDistance = _routeCalculator.CalculateRouteMetrics(vehicle);

            // Update best if improved
            if (newDistance < bestDistance)
            {
                bestDistance = newDistance;
                bestOrder = new List<Passenger>(newOrder);
            }

            // Restore original order
            vehicle.AssignedPassengers = originalPassengers;
        }

        /// <summary>
        /// Reassigns passengers from overloaded vehicles to vehicles with available capacity.
        /// </summary>
        /// <param name="solution">The solution containing vehicles and their assigned passengers.</param>
        private void OptimizeCapacity(Solution solution)
        {
            // Select vehicles exceeding capacity
            var overloadedVehicles = solution.Vehicles
                .Where(v => v.AssignedPassengers.Count > v.Capacity)
                .OrderByDescending(v => v.AssignedPassengers.Count - v.Capacity)
                .ToList();

            if (overloadedVehicles.Count == 0)
            {
                return;
            }

            // Select vehicles with available capacity
            var vehiclesWithCapacity = solution.Vehicles
                .Where(v => v.AssignedPassengers.Count < v.Capacity)
                .OrderBy(v => v.AssignedPassengers.Count)
                .ToList();

            if (vehiclesWithCapacity.Count == 0)
            {
                return;
            }

            // Move passengers from overloaded vehicles
            foreach (var overloadedVehicle in overloadedVehicles)
            {
                MoveExcessPassengers(overloadedVehicle, vehiclesWithCapacity);
            }
        }

        /// <summary>
        /// Moves excess passengers from an overloaded vehicle to vehicles with capacity.
        /// </summary>
        /// <param name="overloadedVehicle">The vehicle with too many passengers.</param>
        /// <param name="vehiclesWithCapacity">List of vehicles with available capacity.</param>
        private void MoveExcessPassengers(Vehicle overloadedVehicle, List<Vehicle> vehiclesWithCapacity)
        {
            int excessPassengers = overloadedVehicle.AssignedPassengers.Count - overloadedVehicle.Capacity;

            // Move excess passengers one by one
            for (int i = 0; i < excessPassengers; i++)
            {
                if (overloadedVehicle.AssignedPassengers.Count <= overloadedVehicle.Capacity ||
                    vehiclesWithCapacity.Count == 0)
                {
                    return;
                }

                // Select the last passenger to move
                var passengerToMove = overloadedVehicle.AssignedPassengers.Last();

                // Find a vehicle with capacity
                foreach (var targetVehicle in vehiclesWithCapacity.ToList())
                {
                    if (targetVehicle.AssignedPassengers.Count < targetVehicle.Capacity)
                    {
                        overloadedVehicle.AssignedPassengers.Remove(passengerToMove);
                        targetVehicle.AssignedPassengers.Add(passengerToMove);

                        // Update capacity list if target is now full
                        if (targetVehicle.AssignedPassengers.Count >= targetVehicle.Capacity)
                        {
                            vehiclesWithCapacity.Remove(targetVehicle);
                        }

                        return;
                    }
                }
            }
        }
    }
}