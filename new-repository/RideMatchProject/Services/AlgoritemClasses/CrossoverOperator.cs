using RideMatchProject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.AlgoritemClasses
{
    /// <summary>
    /// Handles crossover between parent solutions
    /// </summary>
    public class CrossoverOperator
    {
        private readonly ProblemData _problemData;
        private readonly RouteCalculator _routeCalculator;
        private readonly SolutionEvaluator _evaluator;
        private readonly Random _random = new Random();

        public CrossoverOperator(ProblemData problemData, RouteCalculator routeCalculator,
            SolutionEvaluator evaluator)
        {
            _problemData = problemData;
            _routeCalculator = routeCalculator;
            _evaluator = evaluator;
        }

        public Solution Crossover(Solution parent1, Solution parent2)
        {
            var child = new Solution { Vehicles = _routeCalculator.DeepCopyVehicles() };
            var assignedPassengerIds = new HashSet<int>();

            int inheritFromParent1Count = _random.Next(1, child.Vehicles.Count);

            InheritFromParent(parent1, child, assignedPassengerIds, 0, inheritFromParent1Count);
            InheritFromParent(parent2, child, assignedPassengerIds, inheritFromParent1Count, child.Vehicles.Count);

            AssignUnassignedPassengers(child, assignedPassengerIds);

            child.Score = _evaluator.Evaluate(child);
            return child;
        }

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
