using System;
using System.Collections.Generic;
using System.Linq;
using RideMatchProject.Models;
using RideMatchProject.Utilities;

namespace RideMatchProject.Services
{
    /// <summary>
    /// Main class that coordinates the genetic algorithm for ride sharing optimization
    /// </summary>
    public class RideSharingGenetic
    {
        private readonly GeneticAlgorithmConfig _config;
        private readonly ProblemData _problemData;
        private readonly RouteCalculator _routeCalculator;
        private readonly PopulationManager _populationManager;
        private readonly SolutionEvaluator _evaluator;
        private readonly SelectionOperator _selector;
        private readonly CrossoverOperator _crossover;
        private readonly MutationOperator _mutator;

        private List<Solution> _population;
        private Solution _bestSolution;
        private int _generationsWithoutImprovement;
        private double _bestScore;
        private bool _hasCapacityIssue = false;

        public RideSharingGenetic(List<Passenger> passengers, List<Vehicle> vehicles, int populationSize,
            double destinationLat, double destinationLng, int targetTime)
        {
            _config = new GeneticAlgorithmConfig(populationSize);
            _problemData = new ProblemData(
                passengers ?? new List<Passenger>(),
                vehicles ?? new List<Vehicle>(),
                destinationLat,
                destinationLng,
                targetTime);

            _routeCalculator = new RouteCalculator(_problemData);
            _evaluator = new SolutionEvaluator(_problemData, _routeCalculator);
            _populationManager = new PopulationManager(_problemData, _routeCalculator, _evaluator);
            _selector = new SelectionOperator(_config);
            _crossover = new CrossoverOperator(_problemData, _routeCalculator, _evaluator);
            _mutator = new MutationOperator(_problemData, _routeCalculator, _evaluator);

            _bestScore = double.MinValue;
            _generationsWithoutImprovement = 0;
        }

        /// <summary>
        /// Solves the ride sharing problem using a genetic algorithm
        /// </summary>
        public Solution Solve(int generations, List<Solution> initialPopulation = null)
        {
            ValidateInputs();
            InitializePopulation(initialPopulation);
            RunGeneticAlgorithm(generations);
            FinalizeResults();

            return _bestSolution;
        }

        private void ValidateInputs()
        {
            if (_problemData.Passengers.Count == 0 || _problemData.Vehicles.Count == 0)
            {
                _bestSolution = new Solution { Vehicles = new List<Vehicle>() };
                return;
            }

            int totalCapacity = _problemData.Vehicles.Sum(v => v.Capacity);
            if (totalCapacity < _problemData.Passengers.Count)
            {
                Console.WriteLine($"Warning: Total vehicle capacity ({totalCapacity}) is less than passenger count ({_problemData.Passengers.Count})");
                _hasCapacityIssue = true;
            }
        }

        private void InitializePopulation(List<Solution> initialPopulation)
        {
            _population = initialPopulation?.Count > 0
                ? initialPopulation
                : _populationManager.GenerateInitialPopulation(_config.PopulationSize);

            _bestSolution = GetBestSolution();
            _bestScore = _bestSolution.Score;
            _generationsWithoutImprovement = 0;
        }

        private void RunGeneticAlgorithm(int generations)
        {
            if (_problemData.Passengers.Count == 0 || _problemData.Vehicles.Count == 0)
            {
                return;
            }

            for (int i = 0; i < generations; i++)
            {
                EvolveSingleGeneration();

                Solution currentBest = GetBestSolution();
                bool hasBestCapacityIssue = HasCapacityIssue(currentBest);

                UpdateBestSolution(currentBest, hasBestCapacityIssue);

                if (_generationsWithoutImprovement >= _config.MaxGenerationsWithoutImprovement)
                {
                    Console.WriteLine($"Converged after {i + 1} generations");
                    return;
                }
            }
        }

        private void UpdateBestSolution(Solution currentBest, bool hasBestCapacityIssue)
        {
            if (currentBest.Score > _bestScore && (!hasBestCapacityIssue || _hasCapacityIssue))
            {
                _bestSolution = currentBest.Clone();
                _bestScore = currentBest.Score;
                _generationsWithoutImprovement = 0;
            }
            else
            {
                _generationsWithoutImprovement++;
            }
        }

        private void FinalizeResults()
        {
            _routeCalculator.CalculateExactMetrics(_bestSolution);

            if (HasCapacityIssue(_bestSolution))
            {
                Console.WriteLine("No solution found with adequate capacity. The best solution still exceeds vehicle capacities.");
                _hasCapacityIssue = true;
            }
        }

        private void EvolveSingleGeneration()
        {
            var newPopulation = new List<Solution>();
            int eliteCount = (int)(_config.PopulationSize * _config.ElitismRate);

            AddElitesToPopulation(newPopulation, eliteCount);
            AddOffspringToPopulation(newPopulation, eliteCount);

            _population = newPopulation;
        }

        private void AddElitesToPopulation(List<Solution> newPopulation, int eliteCount)
        {
            newPopulation.AddRange(_population
                .OrderByDescending(s => s.Score)
                .Take(eliteCount)
                .Select(s => s.Clone()));
        }

        private void AddOffspringToPopulation(List<Solution> newPopulation, int eliteCount)
        {
            int remainingToCreate = _config.PopulationSize - eliteCount;

            for (int i = 0; i < remainingToCreate; i++)
            {
                Solution parent1 = _selector.TournamentSelection(_population);
                Solution parent2 = _selector.TournamentSelection(_population);

                while (parent1 == parent2)
                {
                    parent2 = _selector.TournamentSelection(_population);
                }

                Solution child = _crossover.Crossover(parent1, parent2);

                if (_config.Random.NextDouble() < _config.MutationRate)
                {
                    _mutator.Mutate(child);
                }

                newPopulation.Add(child);
            }
        }

        public bool HasCapacityIssue()
        {
            return _hasCapacityIssue;
        }

        private bool HasCapacityIssue(Solution solution)
        {
            return solution.Vehicles.Any(v => v.AssignedPassengers.Count > v.Capacity);
        }

        private Solution GetBestSolution()
        {
            return _population.OrderByDescending(s => s.Score).First();
        }

        public List<Solution> GetLatestPopulation()
        {
            return _population?.ToList() ?? new List<Solution>();
        }
    }

    /// <summary>
    /// Stores the configuration parameters for the genetic algorithm
    /// </summary>
    public class GeneticAlgorithmConfig
    {
        public int PopulationSize { get; private set; }
        public double MutationRate { get; private set; } = 0.3;
        public double ElitismRate { get; private set; } = 0.2;
        public int TournamentSize { get; private set; } = 5;
        public int MaxGenerationsWithoutImprovement { get; private set; } = 20;
        public Random Random { get; private set; } = new Random();

        public GeneticAlgorithmConfig(int populationSize)
        {
            PopulationSize = Math.Max(populationSize, 50);
        }
    }

    /// <summary>
    /// Stores the problem data for the ride sharing optimization
    /// </summary>
    public class ProblemData
    {
        public List<Passenger> Passengers { get; private set; }
        public List<Vehicle> Vehicles { get; private set; }
        public double DestinationLat { get; private set; }
        public double DestinationLng { get; private set; }
        public int TargetTime { get; private set; }

        public ProblemData(List<Passenger> passengers, List<Vehicle> vehicles,
            double destinationLat, double destinationLng, int targetTime)
        {
            Passengers = passengers;
            Vehicles = vehicles;
            DestinationLat = destinationLat;
            DestinationLng = destinationLng;
            TargetTime = targetTime;
        }
    }

    /// <summary>
    /// Calculates route metrics for vehicles and passengers
    /// </summary>
    public class RouteCalculator
    {
        private readonly ProblemData _problemData;

        public RouteCalculator(ProblemData problemData)
        {
            _problemData = problemData;
        }

        public double CalculateRouteMetrics(Vehicle vehicle)
        {
            if (vehicle.AssignedPassengers.Count == 0)
            {
                return 0;
            }

            double totalDistance = 0;
            double currentLat = vehicle.StartLatitude;
            double currentLng = vehicle.StartLongitude;

            totalDistance = AddDistanceForPassengerPickups(vehicle, currentLat, currentLng, totalDistance);
            totalDistance = AddDistanceToDestination(vehicle, totalDistance);

            return totalDistance;
        }

        private double AddDistanceForPassengerPickups(Vehicle vehicle, double currentLat,
            double currentLng, double totalDistance)
        {
            double result = totalDistance;
            double lat = currentLat;
            double lng = currentLng;

            foreach (var passenger in vehicle.AssignedPassengers)
            {
                double legDistance = GeoCalculator.CalculateDistance(
                    lat, lng, passenger.Latitude, passenger.Longitude);
                result += legDistance;

                lat = passenger.Latitude;
                lng = passenger.Longitude;
            }

            return result;
        }

        private double AddDistanceToDestination(Vehicle vehicle, double totalDistance)
        {
            if (vehicle.AssignedPassengers.Count == 0)
            {
                return totalDistance;
            }

            var lastPassenger = vehicle.AssignedPassengers.Last();
            double destDistance = GeoCalculator.CalculateDistance(
                lastPassenger.Latitude, lastPassenger.Longitude,
                _problemData.DestinationLat, _problemData.DestinationLng);

            return totalDistance + destDistance;
        }

        public double CalculateAdditionalDistance(Vehicle vehicle, Passenger passenger)
        {
            if (vehicle.AssignedPassengers.Count == 0)
            {
                return CalculateDistanceForEmptyVehicle(vehicle, passenger);
            }
            else
            {
                return CalculateDistanceWithPassenger(vehicle, passenger);
            }
        }

        private double CalculateDistanceForEmptyVehicle(Vehicle vehicle, Passenger passenger)
        {
            return GeoCalculator.CalculateDistance(
                vehicle.StartLatitude, vehicle.StartLongitude,
                passenger.Latitude, passenger.Longitude) +
               GeoCalculator.CalculateDistance(
                passenger.Latitude, passenger.Longitude,
                _problemData.DestinationLat, _problemData.DestinationLng);
        }

        private double CalculateDistanceWithPassenger(Vehicle vehicle, Passenger passenger)
        {
            var lastPassenger = vehicle.AssignedPassengers.Last();

            double currentDistance = GeoCalculator.CalculateDistance(
                lastPassenger.Latitude, lastPassenger.Longitude,
                _problemData.DestinationLat, _problemData.DestinationLng);

            double newDistance = GeoCalculator.CalculateDistance(
                lastPassenger.Latitude, lastPassenger.Longitude,
                passenger.Latitude, passenger.Longitude) +
                GeoCalculator.CalculateDistance(
                passenger.Latitude, passenger.Longitude,
                _problemData.DestinationLat, _problemData.DestinationLng);

            return newDistance - currentDistance;
        }

        public void CalculateExactMetrics(Solution solution)
        {
            foreach (var vehicle in solution.Vehicles)
            {
                if (vehicle.AssignedPassengers.Count > 0)
                {
                    vehicle.TotalDistance = CalculateRouteMetrics(vehicle);
                }
            }
        }

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
                TotalDistance = 0,
            }).ToList();
        }
    }

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

    /// <summary>
    /// Handles solution selection using tournament selection
    /// </summary>
    public class SelectionOperator
    {
        private readonly GeneticAlgorithmConfig _config;

        public SelectionOperator(GeneticAlgorithmConfig config)
        {
            _config = config;
        }

        public Solution TournamentSelection(List<Solution> population)
        {
            int tournamentSize = Math.Min(_config.TournamentSize, population.Count);
            var competitors = new List<Solution>();

            for (int i = 0; i < tournamentSize; i++)
            {
                int idx = _config.Random.Next(population.Count);
                competitors.Add(population[idx]);
            }

            var winner = competitors.OrderByDescending(s => s.Score).First();
            return winner.Clone();
        }
    }

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