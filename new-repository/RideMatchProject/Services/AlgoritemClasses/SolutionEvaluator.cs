using RideMatchProject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.AlgoritemClasses
{
    /// <summary>
    /// Evaluates solutions and assigns fitness scores
    /// </summary>
    public class SolutionEvaluator
    {
        private readonly ProblemData _problemData;
        private readonly RouteCalculator _routeCalculator;

        public SolutionEvaluator(ProblemData problemData, RouteCalculator routeCalculator)
        {
            _problemData = problemData;
            _routeCalculator = routeCalculator;
        }

        public double Evaluate(Solution solution)
        {
            double totalDistance = 0;
            int assignedCount = 0;
            int usedVehicles = 0;
            int overloadedVehicles = 0;
            int totalCapacityViolation = 0;

            CalculateMetrics(solution, ref totalDistance, ref assignedCount,
                ref usedVehicles, ref overloadedVehicles, ref totalCapacityViolation);

            return CalculateScore(totalDistance, assignedCount, usedVehicles,
                overloadedVehicles, totalCapacityViolation);
        }

        private void CalculateMetrics(Solution solution, ref double totalDistance,
            ref int assignedCount, ref int usedVehicles, ref int overloadedVehicles,
            ref int totalCapacityViolation)
        {
            foreach (var vehicle in solution.Vehicles)
            {
                if (vehicle.AssignedPassengers.Count == 0)
                {
                    continue;
                }

                usedVehicles++;

                if (vehicle.AssignedPassengers.Count > vehicle.Capacity)
                {
                    overloadedVehicles++;
                    totalCapacityViolation += (vehicle.AssignedPassengers.Count - vehicle.Capacity);
                }

                vehicle.TotalDistance = _routeCalculator.CalculateRouteMetrics(vehicle);
                totalDistance += vehicle.TotalDistance;
                assignedCount += vehicle.AssignedPassengers.Count;
            }
        }

        private double CalculateScore(double totalDistance, int assignedCount,
            int usedVehicles, int overloadedVehicles, int totalCapacityViolation)
        {
            double distanceScore = totalDistance > 0 ? 1500.0 / totalDistance : 0;
            double assignmentScore = assignedCount * 100.0;
            double vehicleUtilizationScore = usedVehicles * -10.0;
            double overloadPenalty = overloadedVehicles * -200.0;
            double capacityViolationPenalty = totalCapacityViolation * -300.0;
            double unassignedPenalty = (_problemData.Passengers.Count - assignedCount) * -1000.0;

            return distanceScore + assignmentScore + vehicleUtilizationScore +
                   overloadPenalty + capacityViolationPenalty + unassignedPenalty;
        }
    }
}
