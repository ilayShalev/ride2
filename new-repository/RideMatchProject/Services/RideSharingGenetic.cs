using System;
using System.Collections.Generic;
using System.Linq;
using RideMatchProject.Models;
using RideMatchProject.Utilities;
using RideMatchProject.Services.AlgoritemClasses;

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

  
  
}