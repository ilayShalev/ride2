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
    /// Manages population generation and evolution
    /// </summary>
    public class PopulationManager
    {
        private readonly ProblemData _problemData;
        private readonly RouteCalculator _routeCalculator;
        private readonly SolutionEvaluator _evaluator;
        private readonly Random _random = new Random();

        public PopulationManager(ProblemData problemData, RouteCalculator routeCalculator,
            SolutionEvaluator evaluator)
        {
            _problemData = problemData;
            _routeCalculator = routeCalculator;
            _evaluator = evaluator;
        }

        public List<Solution> GenerateInitialPopulation(int populationSize)
        {
            var result = new List<Solution>();

            result.Add(CreateGreedySolution());
            result.Add(CreateEvenDistributionSolution());

            while (result.Count < populationSize)
            {
                var solution = new Solution { Vehicles = _routeCalculator.DeepCopyVehicles() };
                var unassigned = _problemData.Passengers.OrderBy(x => _random.Next()).ToList();

                AssignPassengersToVehicles(solution, unassigned);
                AssignRemainingPassengers(solution, unassigned);

                solution.Score = _evaluator.Evaluate(solution);
                result.Add(solution);
            }

            return result;
        }

        private void AssignPassengersToVehicles(Solution solution, List<Passenger> unassigned)
        {
            foreach (var vehicle in solution.Vehicles)
            {
                int maxToAssign = Math.Min(vehicle.Capacity, unassigned.Count);
                int toAssign = _random.Next(0, maxToAssign + 1);

                for (int j = 0; j < toAssign && unassigned.Count > 0; j++)
                {
                    var passenger = unassigned[0];
                    vehicle.AssignedPassengers.Add(passenger);
                    unassigned.RemoveAt(0);
                }
            }
        }

        private void AssignRemainingPassengers(Solution solution, List<Passenger> unassigned)
        {
            foreach (var passenger in unassigned.ToList())
            {
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
                    AssignToLeastOverloaded(solution, passenger);
                }
                else
                {
                    AssignToClosestVehicle(availableVehicles, passenger);
                }
            }

            solution.Score = _evaluator.Evaluate(solution);
            return solution;
        }

        private List<Passenger> SortPassengersByDistanceToDestination()
        {
            return _problemData.Passengers
                .OrderByDescending(p => GeoCalculator.CalculateDistance(
                    p.Latitude, p.Longitude,
                    _problemData.DestinationLat, _problemData.DestinationLng))
                .ToList();
        }

        private void AssignToLeastOverloaded(Solution solution, Passenger passenger)
        {
            var vehicle = solution.Vehicles.OrderBy(v => v.AssignedPassengers.Count).First();
            vehicle.AssignedPassengers.Add(passenger);
        }

        private void AssignToClosestVehicle(List<Vehicle> availableVehicles, Passenger passenger)
        {
            var closestVehicle = availableVehicles
                .OrderBy(v => GeoCalculator.CalculateDistance(
                    v.StartLatitude, v.StartLongitude, passenger.Latitude, passenger.Longitude))
                .First();

            closestVehicle.AssignedPassengers.Add(passenger);
        }

        public Solution CreateEvenDistributionSolution()
        {
            var solution = new Solution { Vehicles = _routeCalculator.DeepCopyVehicles() };
            var remainingPassengers = _problemData.Passengers.ToList();

            int passengersPerVehicle = CalculatePassengersPerVehicle();

            foreach (var vehicle in solution.Vehicles)
            {
                AssignNearestPassengersToVehicle(vehicle, remainingPassengers, passengersPerVehicle);
            }

            AssignRemainingPassengersToAvailableVehicles(solution, remainingPassengers);

            solution.Score = _evaluator.Evaluate(solution);
            return solution;
        }

        private int CalculatePassengersPerVehicle()
        {
            int totalCapacity = _problemData.Vehicles.Sum(v => v.Capacity);
            return Math.Min(_problemData.Passengers.Count / _problemData.Vehicles.Count,
                           Math.Min(totalCapacity / _problemData.Vehicles.Count,
                                  _problemData.Vehicles.Min(v => v.Capacity)));
        }

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

        private void AssignRemainingPassengersToAvailableVehicles(Solution solution, List<Passenger> remainingPassengers)
        {
            AssignToVehiclesWithCapacity(solution, remainingPassengers);
            AssignToLeastLoadedVehicles(solution, remainingPassengers);
        }

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
