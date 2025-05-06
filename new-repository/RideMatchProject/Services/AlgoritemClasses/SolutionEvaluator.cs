using RideMatchProject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.AlgoritemClasses
{
    /// <summary>
    /// Evaluates solutions in a ride-matching system by calculating fitness scores based on various metrics.
    /// The class assesses solutions by considering total distance traveled, passenger assignments, vehicle utilization,
    /// and penalties for capacity violations and unassigned passengers. It relies on a <see cref="RouteCalculator"/>
    /// to compute route distances and uses problem data to validate assignments.
    /// </summary>
    public class SolutionEvaluator
    {
        private readonly ProblemData _problemData;
        private readonly RouteCalculator _routeCalculator;

        /// <summary>
        /// Initializes a new instance of the <see cref="SolutionEvaluator"/> class with the specified problem data
        /// and route calculator.
        /// </summary>
        /// <param name="problemData">The problem data containing passengers, vehicles, and other relevant information.</param>
        /// <param name="routeCalculator">The route calculator used to compute distances for vehicle routes.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="problemData"/> or <paramref name="routeCalculator"/> is null.</exception>
        public SolutionEvaluator(ProblemData problemData, RouteCalculator routeCalculator)
        {
            _problemData = problemData ?? throw new ArgumentNullException(nameof(problemData));
            _routeCalculator = routeCalculator ?? throw new ArgumentNullException(nameof(routeCalculator));
        }

        /// <summary>
        /// Evaluates a solution by calculating its fitness score based on route distances, passenger assignments,
        /// vehicle utilization, and penalties for capacity violations and unassigned passengers.
        /// </summary>
        /// <param name="solution">The solution to evaluate, containing vehicles with assigned passengers.</param>
        /// <returns>
        /// A double representing the fitness score of the solution. Higher scores indicate better solutions.
        /// The score is computed as a combination of distance efficiency, passenger assignment rewards,
        /// and penalties for vehicle overuse, overloading, and unassigned passengers.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="solution"/> is null.</exception>
        /// <remarks>
        /// The evaluation process involves calculating metrics such as total distance, number of assigned passengers,
        /// number of used vehicles, and capacity violations. These metrics are then used to compute a score
        /// with weighted components, including rewards for efficiency and penalties for infeasibility.
        /// </remarks>
        public double Evaluate(Solution solution)
        {
            if (solution == null)
                throw new ArgumentNullException(nameof(solution));

            double totalDistance = 0;
            int assignedCount = 0;
            int usedVehicles = 0;
            int overloadedVehicles = 0;
            int totalCapacityViolation = 0;

            // Calculate metrics for the solution
            CalculateMetrics(solution, ref totalDistance, ref assignedCount,
                ref usedVehicles, ref overloadedVehicles, ref totalCapacityViolation);

            // Compute and return the fitness score
            return CalculateScore(totalDistance, assignedCount, usedVehicles,
                overloadedVehicles, totalCapacityViolation);
        }

        /// <summary>
        /// Calculates key metrics for a solution, including total distance, number of assigned passengers,
        /// number of used vehicles, and capacity violations.
        /// </summary>
        /// <param name="solution">The solution containing vehicles with assigned passengers.</param>
        /// <param name="totalDistance">The total distance traveled by all vehicles (updated by reference).</param>
        /// <param name="assignedCount">The total number of passengers assigned to vehicles (updated by reference).</param>
        /// <param name="usedVehicles">The number of vehicles with at least one assigned passenger (updated by reference).</param>
        /// <param name="overloadedVehicles">The number of vehicles exceeding their capacity (updated by reference).</param>
        /// <param name="totalCapacityViolation">The total number of excess passengers across overloaded vehicles (updated by reference).</param>
        /// <remarks>
        /// This method iterates through each vehicle in the solution, calculates its route distance using
        /// <see cref="RouteCalculator.CalculateRouteMetrics"/>, and updates the provided metrics. Vehicles with
        /// no assigned passengers are skipped, and capacity violations are tracked for vehicles exceeding their capacity.
        /// </remarks>
        private void CalculateMetrics(Solution solution, ref double totalDistance,
            ref int assignedCount, ref int usedVehicles, ref int overloadedVehicles,
            ref int totalCapacityViolation)
        {
            foreach (var vehicle in solution.Vehicles)
            {
                if (vehicle == null || vehicle.AssignedPassengers == null || vehicle.AssignedPassengers.Count == 0)
                {
                    continue;
                }

                // Increment the count of used vehicles
                usedVehicles++;

                // Check for capacity violations
                if (vehicle.AssignedPassengers.Count > vehicle.Capacity)
                {
                    overloadedVehicles++;
                    totalCapacityViolation += (vehicle.AssignedPassengers.Count - vehicle.Capacity);
                }

                // Calculate and accumulate the vehicle's route distance
                vehicle.TotalDistance = _routeCalculator.CalculateRouteMetrics(vehicle);
                totalDistance += vehicle.TotalDistance;

                // Accumulate the number of assigned passengers
                assignedCount += vehicle.AssignedPassengers.Count;
            }
        }

        /// <summary>
        /// Calculates the fitness score for a solution based on provided metrics.
        /// The score combines rewards for efficiency (e.g., low distance, high passenger assignments)
        /// and penalties for infeasibility (e.g., capacity violations, unassigned passengers).
        /// </summary>
        /// <param name="totalDistance">The total distance traveled by all vehicles.</param>
        /// <param name="assignedCount">The total number of passengers assigned to vehicles.</param>
        /// <param name="usedVehicles">The number of vehicles with at least one assigned passenger.</param>
        /// <param name="overloadedVehicles">The number of vehicles exceeding their capacity.</param>
        /// <param name="totalCapacityViolation">The total number of excess passengers across overloaded vehicles.</param>
        /// <returns>
        /// A double representing the fitness score. Higher scores indicate better solutions.
        /// </returns>
        /// <remarks>
        /// The score is computed as a weighted sum of the following components:
        /// <list type="bullet">
        /// <item><b>Distance Score</b>: Inversely proportional to total distance (1500 / distance, or 0 if distance is 0).</item>
        /// <item><b>Assignment Score</b>: 100 points per assigned passenger.</item>
        /// <item><b>Vehicle Utilization Penalty</b>: -10 points per used vehicle.</item>
        /// <item><b>Overload Penalty</b>: -200 points per overloaded vehicle.</item>
        /// <item><b>Capacity Violation Penalty</b>: -300 points per excess passenger.</item>
        /// <item><b>Unassigned Penalty</b>: -1000 points per unassigned passenger.</item>
        /// </list>
        /// The weights are chosen to prioritize feasible solutions (assigning all passengers without overloading)
        /// while minimizing distance and vehicle usage.
        /// </remarks>
        private double CalculateScore(double totalDistance, int assignedCount,
            int usedVehicles, int overloadedVehicles, int totalCapacityViolation)
        {
            // Reward for low distance (avoid division by zero)
            double distanceScore = totalDistance > 0 ? 1500.0 / totalDistance : 0;

            // Reward for assigning passengers
            double assignmentScore = assignedCount * 100.0;

            // Penalty for using more vehicles
            double vehicleUtilizationScore = usedVehicles * -10.0;

            // Penalty for overloaded vehicles
            double overloadPenalty = overloadedVehicles * -200.0;

            // Penalty for excess passengers
            double capacityViolationPenalty = totalCapacityViolation * -300.0;

            // Penalty for unassigned passengers
            double unassignedPenalty = (_problemData.Passengers.Count - assignedCount) * -1000.0;

            // Combine all components to compute the final score
            return distanceScore + assignmentScore + vehicleUtilizationScore +
                   overloadPenalty + capacityViolationPenalty + unassignedPenalty;
        }
    }
}