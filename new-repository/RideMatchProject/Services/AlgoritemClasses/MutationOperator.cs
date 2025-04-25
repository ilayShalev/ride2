using RideMatchProject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.AlgoritemClasses
{
    /// <summary>
    /// Handles mutation operations on solutions
    /// </summary>
    public class MutationOperator
    {
        private readonly ProblemData _problemData;
        private readonly RouteCalculator _routeCalculator;
        private readonly SolutionEvaluator _evaluator;
        private readonly Random _random = new Random();

        private enum MutationType { Swap, Reorder, Move, OptimizeRoutes, OptimizeCapacity }

        public MutationOperator(ProblemData problemData, RouteCalculator routeCalculator,
            SolutionEvaluator evaluator)
        {
            _problemData = problemData;
            _routeCalculator = routeCalculator;
            _evaluator = evaluator;
        }

        public void Mutate(Solution solution)
        {
            MutationType mutationType = (MutationType)_random.Next(5);

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

            solution.Score = _evaluator.Evaluate(solution);
        }

        private void SwapPassengers(Solution solution)
        {
            var vehiclesWithPassengers = solution.Vehicles
                .Where(v => v.AssignedPassengers.Count > 0)
                .ToList();

            if (vehiclesWithPassengers.Count < 2)
            {
                return;
            }

            int idx1 = _random.Next(vehiclesWithPassengers.Count);
            int idx2 = _random.Next(vehiclesWithPassengers.Count - 1);
            if (idx2 >= idx1) idx2++;

            var vehicle1 = vehiclesWithPassengers[idx1];
            var vehicle2 = vehiclesWithPassengers[idx2];

            PerformPassengerSwap(vehicle1, vehicle2);
        }

        private void PerformPassengerSwap(Vehicle vehicle1, Vehicle vehicle2)
        {
            if (vehicle1.AssignedPassengers.Count == 0 || vehicle2.AssignedPassengers.Count == 0)
            {
                return;
            }

            int passengerIdx1 = _random.Next(vehicle1.AssignedPassengers.Count);
            int passengerIdx2 = _random.Next(vehicle2.AssignedPassengers.Count);

            var passenger1 = vehicle1.AssignedPassengers[passengerIdx1];
            var passenger2 = vehicle2.AssignedPassengers[passengerIdx2];

            vehicle1.AssignedPassengers[passengerIdx1] = passenger2;
            vehicle2.AssignedPassengers[passengerIdx2] = passenger1;
        }

        private void ReorderPassengers(Solution solution)
        {
            var vehiclesWithMultiplePassengers = solution.Vehicles
                .Where(v => v.AssignedPassengers.Count > 1)
                .ToList();

            if (vehiclesWithMultiplePassengers.Count == 0)
            {
                return;
            }

            var vehicle = vehiclesWithMultiplePassengers[_random.Next(vehiclesWithMultiplePassengers.Count)];

            if (vehicle.AssignedPassengers.Count > 2)
            {
                ReorderWithTwoOpt(vehicle);
            }
            else if (vehicle.AssignedPassengers.Count == 2)
            {
                if (_random.Next(2) == 0)
                {
                    var temp = vehicle.AssignedPassengers[0];
                    vehicle.AssignedPassengers[0] = vehicle.AssignedPassengers[1];
                    vehicle.AssignedPassengers[1] = temp;
                }
            }
        }

        private void ReorderWithTwoOpt(Vehicle vehicle)
        {
            int pos1 = _random.Next(vehicle.AssignedPassengers.Count);
            int pos2 = _random.Next(vehicle.AssignedPassengers.Count);

            if (pos1 == pos2)
            {
                return;
            }

            if (pos1 > pos2)
            {
                int temp = pos1;
                pos1 = pos2;
                pos2 = temp;
            }

            var passengers = vehicle.AssignedPassengers;
            ReverseSegment(passengers, pos1, pos2);
        }

        private void ReverseSegment(List<Passenger> passengers, int start, int end)
        {
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

        private void MovePassenger(Solution solution)
        {
            var vehiclesWithPassengers = solution.Vehicles
                .Where(v => v.AssignedPassengers.Count > 0)
                .ToList();

            if (vehiclesWithPassengers.Count == 0)
            {
                return;
            }

            var sourceVehicle = vehiclesWithPassengers[_random.Next(vehiclesWithPassengers.Count)];
            int passengerIdx = _random.Next(sourceVehicle.AssignedPassengers.Count);
            var passenger = sourceVehicle.AssignedPassengers[passengerIdx];

            var targetOptions = solution.Vehicles
                .Where(v => v.Id != sourceVehicle.Id)
                .ToList();

            if (targetOptions.Count > 0)
            {
                var targetVehicle = targetOptions[_random.Next(targetOptions.Count)];
                sourceVehicle.AssignedPassengers.RemoveAt(passengerIdx);
                targetVehicle.AssignedPassengers.Add(passenger);
            }
        }

        private void OptimizeRoutes(Solution solution)
        {
            var vehiclesWithManyPassengers = solution.Vehicles
                .Where(v => v.AssignedPassengers.Count >= 4)
                .ToList();

            if (vehiclesWithManyPassengers.Count == 0)
            {
                return;
            }

            var vehicle = vehiclesWithManyPassengers[_random.Next(vehiclesWithManyPassengers.Count)];
            PerformTwoOptSearch(vehicle);
        }

        private void PerformTwoOptSearch(Vehicle vehicle)
        {
            double bestDistance = _routeCalculator.CalculateRouteMetrics(vehicle);
            var bestOrder = new List<Passenger>(vehicle.AssignedPassengers);

            int maxSwaps = Math.Min(10, vehicle.AssignedPassengers.Count * (vehicle.AssignedPassengers.Count - 1) / 2);
            var originalPassengers = vehicle.AssignedPassengers;

            for (int swapCount = 0; swapCount < maxSwaps; swapCount++)
            {
                TrySwapAndUpdateBest(vehicle, ref bestDistance, ref bestOrder, originalPassengers);
            }

            // Apply the best route found
            vehicle.AssignedPassengers = bestOrder;
        }

        private void TrySwapAndUpdateBest(Vehicle vehicle, ref double bestDistance,
            ref List<Passenger> bestOrder, List<Passenger> originalPassengers)
        {
            int i = _random.Next(vehicle.AssignedPassengers.Count);
            int j = _random.Next(vehicle.AssignedPassengers.Count);

            if (i == j)
            {
                return;
            }

            if (i > j)
            {
                int temp = i;
                i = j;
                j = temp;
            }

            var newOrder = new List<Passenger>(originalPassengers);
            newOrder.Reverse(i, j - i + 1);

            vehicle.AssignedPassengers = newOrder;
            double newDistance = _routeCalculator.CalculateRouteMetrics(vehicle);

            if (newDistance < bestDistance)
            {
                bestDistance = newDistance;
                bestOrder = new List<Passenger>(newOrder);
            }

            vehicle.AssignedPassengers = originalPassengers;
        }

        private void OptimizeCapacity(Solution solution)
        {
            var overloadedVehicles = solution.Vehicles
                .Where(v => v.AssignedPassengers.Count > v.Capacity)
                .OrderByDescending(v => v.AssignedPassengers.Count - v.Capacity)
                .ToList();

            if (overloadedVehicles.Count == 0)
            {
                return;
            }

            var vehiclesWithCapacity = solution.Vehicles
                .Where(v => v.AssignedPassengers.Count < v.Capacity)
                .OrderBy(v => v.AssignedPassengers.Count)
                .ToList();

            if (vehiclesWithCapacity.Count == 0)
            {
                return;
            }

            foreach (var overloadedVehicle in overloadedVehicles)
            {
                MoveExcessPassengers(overloadedVehicle, vehiclesWithCapacity);
            }
        }

        private void MoveExcessPassengers(Vehicle overloadedVehicle, List<Vehicle> vehiclesWithCapacity)
        {
            int excessPassengers = overloadedVehicle.AssignedPassengers.Count - overloadedVehicle.Capacity;

            for (int i = 0; i < excessPassengers; i++)
            {
                if (overloadedVehicle.AssignedPassengers.Count <= overloadedVehicle.Capacity ||
                    vehiclesWithCapacity.Count == 0)
                {
                    return;
                }

                var passengerToMove = overloadedVehicle.AssignedPassengers.Last();

                foreach (var targetVehicle in vehiclesWithCapacity.ToList())
                {
                    if (targetVehicle.AssignedPassengers.Count < targetVehicle.Capacity)
                    {
                        overloadedVehicle.AssignedPassengers.Remove(passengerToMove);
                        targetVehicle.AssignedPassengers.Add(passengerToMove);

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
