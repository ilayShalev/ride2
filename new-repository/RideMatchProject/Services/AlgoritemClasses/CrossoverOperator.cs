using RideMatchProject.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RideMatchProject.Services.AlgoritemClasses
{
    /// <summary>
    /// Handles the crossover operation between two parent solutions in a genetic algorithm for ride-sharing optimization.
    /// </summary>
    /// <remarks>
    /// The crossover operation combines the passenger assignments from two parent solutions to create a child solution,
    /// aiming to inherit desirable traits from both parents while maintaining feasibility. The class ensures that vehicle
    /// capacity constraints are respected as much as possible and assigns any unassigned passengers to appropriate vehicles.
    /// </remarks>
    public class CrossoverOperator
    {
        private readonly ProblemData _problemData;
        private readonly RouteCalculator _routeCalculator;
        private readonly SolutionEvaluator _evaluator;
        private readonly Random _random = new Random();

        /// <summary>
        /// Initializes a new instance of the <see cref="CrossoverOperator"/> class with the specified problem data and supporting components.
        /// </summary>
        /// <param name="problemData">The problem data containing passengers, vehicles, and destination information.</param>
        /// <param name="routeCalculator">The route calculator used to compute distances and other metrics for vehicle assignments.</param>
        /// <param name="evaluator">The solution evaluator used to calculate the score of the child solution.</param>
        /// <remarks>
        /// The constructor sets up the necessary dependencies for performing crossover operations. The <see cref="Random"/> instance
        /// is initialized to introduce randomness in selecting vehicles to inherit from each parent.
        /// </remarks>
        public CrossoverOperator(ProblemData problemData, RouteCalculator routeCalculator,
            SolutionEvaluator evaluator)
        {
            _problemData = problemData ?? throw new ArgumentNullException(nameof(problemData));
            _routeCalculator = routeCalculator ?? throw new ArgumentNullException(nameof(routeCalculator));
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }

        /// <summary>
        /// Performs a crossover operation between two parent solutions to produce a child solution.
        /// </summary>
        /// <param name="parent1">The first parent solution, containing vehicle assignments and passenger allocations.</param>
        /// <param name="parent2">The second parent solution, containing vehicle assignments and passenger allocations.</param>
        /// <returns>A new <see cref="Solution"/> that combines passenger assignments from both parents.</returns>
        /// <remarks>
        /// The crossover process works as follows:
        /// 1. Creates a new child solution with a deep copy of the vehicles.
        /// 2. Randomly selects a number of vehicles to inherit passenger assignments from <paramref name="parent1"/>.
        /// 3. Inherits passenger assignments from <paramref name="parent1"/> for the selected vehicles, respecting capacity constraints.
        /// 4. Inherits passenger assignments from <paramref name="parent2"/> for the remaining vehicles, ensuring no duplicate assignments.
        /// 5. Assigns any unassigned passengers to vehicles based on minimal additional distance and capacity considerations.
        /// 6. Evaluates the child solution to assign a score.
        /// The method ensures that passengers are not assigned multiple times and handles cases where vehicle capacities are exceeded.
        /// </remarks>
        public Solution Crossover(Solution parent1, Solution parent2)
        {
            if (parent1 == null) throw new ArgumentNullException(nameof(parent1));
            if (parent2 == null) throw new ArgumentNullException(nameof(parent2));

            var child = new Solution { Vehicles = _routeCalculator.DeepCopyVehicles() };
            var assignedPassengerIds = new HashSet<int>();

            int inheritFromParent1Count = _random.Next(1, child.Vehicles.Count);

            InheritFromParent(parent1, child, assignedPassengerIds, 0, inheritFromParent1Count);
            InheritFromParent(parent2, child, assignedPassengerIds, inheritFromParent1Count, child.Vehicles.Count);

            AssignUnassignedPassengers(child, assignedPassengerIds);

            child.Score = _evaluator.Evaluate(child);
            return child;
        }

        /// <summary>
        /// Copies passenger assignments from a parent solution to a child solution for a specified range of vehicles.
        /// </summary>
        /// <param name="parent">The parent solution from which to inherit passenger assignments.</param>
        /// <param name="child">The child solution to receive the passenger assignments.</param>
        /// <param name="assignedPassengerIds">A set of passenger IDs that have already been assigned to prevent duplicates.</param>
        /// <param name="startIdx">The starting index of the vehicle range to process.</param>
        /// <param name="endIdx">The ending index (exclusive) of the vehicle range to process.</param>
        /// <remarks>
        /// This method iterates through the specified range of vehicles in the parent solution and copies their passenger assignments
        /// to the corresponding vehicles in the child solution. It ensures that:
        /// - Only unassigned passengers (not in <paramref name="assignedPassengerIds"/>) are copied.
        /// - The target vehicle's capacity is not exceeded.
        /// If the parent solution has fewer vehicles than the specified range, the method exits early to avoid index out-of-range errors.
        /// </remarks>
        private void InheritFromParent(Solution parent, Solution child, HashSet<int> assignedPassengerIds,
            int startIdx, int endIdx)
        {
            for (int i = startIdx; i < endIdx; i++)
            {
                if (i >= parent.Vehicles.Count)
                {
                    return;
                }

                var sourceVehicle = parent.Vehicles[i];
                var targetVehicle = child.Vehicles[i];

                foreach (var passenger in sourceVehicle.AssignedPassengers)
                {
                    if (!assignedPassengerIds.Contains(passenger.Id) &&
                        targetVehicle.AssignedPassengers.Count < targetVehicle.Capacity)
                    {
                        targetVehicle.AssignedPassengers.Add(passenger);
                        assignedPassengerIds.Add(passenger.Id);
                    }
                }
            }
        }

        /// <summary>
        /// Assigns any remaining unassigned passengers to vehicles in the child solution.
        /// </summary>
        /// <param name="child">The child solution to which unassigned passengers will be assigned.</param>
        /// <param name="assignedPassengerIds">A set of passenger IDs that have already been assigned.</param>
        /// <remarks>
        /// This method identifies passengers who were not assigned during the crossover process and assigns them to vehicles as follows:
        /// 1. For each unassigned passenger, it identifies vehicles with available capacity (i.e., fewer passengers than their capacity).
        /// 2. If such vehicles exist, the passenger is assigned to the vehicle that minimizes the additional distance traveled
        ///    (calculated by <see cref="RouteCalculator.CalculateAdditionalDistance"/>).
        /// 3. If no vehicles have available capacity, the passenger is assigned to the vehicle that minimizes the additional distance,
        ///    with a tiebreaker based on the number of assigned passengers (to distribute overloads evenly).
        /// This approach ensures all passengers are assigned, even if it results in capacity violations, which can be addressed later
        /// by the genetic algorithm's selection and mutation processes.
        /// </remarks>
        private void AssignUnassignedPassengers(Solution child, HashSet<int> assignedPassengerIds)
        {
            var unassignedPassengers = _problemData.Passengers
                .Where(p => !assignedPassengerIds.Contains(p.Id))
                .ToList();

            foreach (var passenger in unassignedPassengers)
            {
                var availableVehicles = child.Vehicles
                    .Where(v => v.AssignedPassengers.Count < v.Capacity)
                    .OrderBy(v => _routeCalculator.CalculateAdditionalDistance(v, passenger))
                    .ToList();

                if (availableVehicles.Any())
                {
                    availableVehicles.First().AssignedPassengers.Add(passenger);
                }
                else
                {
                    var bestVehicle = child.Vehicles
                        .OrderBy(v => _routeCalculator.CalculateAdditionalDistance(v, passenger))
                        .ThenBy(v => v.AssignedPassengers.Count)
                        .First();

                    bestVehicle.AssignedPassengers.Add(passenger);
                }
            }
        }
    }
}