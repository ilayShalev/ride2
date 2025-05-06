using System;
using System.Collections.Generic;
using System.Linq;
using RideMatchProject.Models;
using RideMatchProject.Utilities;
using RideMatchProject.Services.AlgoritemClasses;

namespace RideMatchProject.Services
{
    /// <summary>
    /// Coordinates a genetic algorithm to optimize ride-sharing assignments, matching passengers to vehicles
    /// to minimize travel time, distance, or other costs while respecting vehicle capacities and destination constraints.
    /// </summary>
    /// <remarks>
    /// This class implements a genetic algorithm that evolves a population of potential ride-sharing solutions over multiple generations.
    /// It uses selection, crossover, and mutation operators to explore the solution space and converges toward an optimal or near-optimal
    /// assignment of passengers to vehicles. The algorithm considers factors such as vehicle capacity, passenger destinations, and travel metrics.
    /// </remarks>
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
        private bool _hasCapacityIssue;

        /// <summary>
        /// Initializes a new instance of the <see cref="RideSharingGenetic"/> class with the specified problem data and configuration.
        /// </summary>
        /// <param name="passengers">The list of passengers to be assigned to vehicles. Must not be null; an empty list is treated as valid.</param>
        /// <param name="vehicles">The list of vehicles available for ride-sharing. Must not be null; an empty list is treated as valid.</param>
        /// <param name="populationSize">The number of solutions in the genetic algorithm's population. Must be positive.</param>
        /// <param name="destinationLat">The latitude of the common destination for all passengers.</param>
        /// <param name="destinationLng">The longitude of the common destination for all passengers.</param>
        /// <param name="targetTime">The target time for reaching the destination, used for scheduling constraints.</param>
        /// <remarks>
        /// The constructor initializes the genetic algorithm's configuration, problem data, and supporting components (e.g., route calculator,
        /// population manager, and genetic operators). It also sets initial values for tracking the best solution and convergence criteria.
        /// If the provided <paramref name="passengers"/> or <paramref name="vehicles"/> lists are null, they are replaced with empty lists to prevent null reference issues.
        /// </remarks>
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
            _hasCapacityIssue = false;
        }

        /// <summary>
        /// Solves the ride-sharing problem using a genetic algorithm, returning the best solution found.
        /// </summary>
        /// <param name="generations">The maximum number of generations to evolve the population.</param>
        /// <param name="initialPopulation">An optional list of initial solutions to seed the population. If null or empty, a new population is generated.</param>
        /// <returns>The best solution found, containing vehicle assignments and associated metrics (e.g., score, routes).</returns>
        /// <remarks>
        /// This method orchestrates the genetic algorithm by validating inputs, initializing the population, running the evolution process
        /// for the specified number of generations, and finalizing the results. If the problem is infeasible (e.g., no passengers or vehicles),
        /// an empty solution is returned. The algorithm may terminate early if convergence criteria are met (e.g., no improvement for a set number of generations).
        /// </remarks>
        public Solution Solve(int generations, List<Solution> initialPopulation = null)
        {
            ValidateInputs();
            InitializePopulation(initialPopulation);
            RunGeneticAlgorithm(generations);
            FinalizeResults();

            return _bestSolution;
        }

        /// <summary>
        /// Validates the input data to ensure the problem is solvable and logs warnings if capacity constraints are violated.
        /// </summary>
        /// <remarks>
        /// Checks if there are any passengers or vehicles. If either is empty, an empty solution is set as the best solution, and the method returns.
        /// Additionally, it verifies whether the total vehicle capacity is sufficient to accommodate all passengers. If not, a warning is logged,
        /// and the <see cref="_hasCapacityIssue"/> flag is set to true.
        /// </remarks>
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

        /// <summary>
        /// Initializes the population of solutions, either using a provided initial population or generating a new one.
        /// </summary>
        /// <param name="initialPopulation">An optional list of initial solutions to use. If null or empty, a new population is generated.</param>
        /// <remarks>
        /// If an initial population is provided and contains solutions, it is used directly. Otherwise, the <see cref="PopulationManager"/>
        /// generates a new population of the specified size. The best solution and its score are identified, and the convergence counter is reset.
        /// </remarks>
        private void InitializePopulation(List<Solution> initialPopulation)
        {
            _population = initialPopulation?.Count > 0
                ? initialPopulation
                : _populationManager.GenerateInitialPopulation(_config.PopulationSize);

            _bestSolution = GetBestSolution();
            _bestScore = _bestSolution.Score;
            _generationsWithoutImprovement = 0;
        }

        /// <summary>
        /// Runs the genetic algorithm for the specified number of generations or until convergence.
        /// </summary>
        /// <param name="generations">The maximum number of generations to evolve the population.</param>
        /// <remarks>
        /// If the problem is infeasible (e.g., no passengers or vehicles), the method returns immediately. Otherwise, it evolves the population
        /// generation by generation, updating the best solution and tracking convergence. The algorithm stops early if the number of generations
        /// without improvement exceeds the configured maximum (<see cref="GeneticAlgorithmConfig.MaxGenerationsWithoutImprovement"/>).
        /// </remarks>
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

        /// <summary>
        /// Updates the best solution and convergence tracking based on the current generation's best solution.
        /// </summary>
        /// <param name="currentBest">The best solution from the current generation.</param>
        /// <param name="hasBestCapacityIssue">Indicates whether the current best solution violates vehicle capacity constraints.</param>
        /// <remarks>
        /// The best solution is updated only if the current solution has a higher score and either has no capacity issues or the problem
        /// inherently has capacity issues (i.e., <see cref="_hasCapacityIssue"/> is true). If the best solution is updated, the convergence
        /// counter is reset; otherwise, it is incremented.
        /// </remarks>
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

        /// <summary>
        /// Finalizes the results by calculating exact metrics for the best solution and logging capacity issues, if any.
        /// </summary>
        /// <remarks>
        /// Uses the <see cref="RouteCalculator"/> to compute precise metrics (e.g., travel times, distances) for the best solution.
        /// If the best solution violates vehicle capacity constraints, a warning is logged, and the <see cref="_hasCapacityIssue"/> flag is set.
        /// </remarks>
        private void FinalizeResults()
        {
            _routeCalculator.CalculateExactMetrics(_bestSolution);

            if (HasCapacityIssue(_bestSolution))
            {
                Console.WriteLine("No solution found with adequate capacity. The best solution still exceeds vehicle capacities.");
                _hasCapacityIssue = true;
            }
        }

        /// <summary>
        /// Evolves the population by creating a new generation through elitism, selection, crossover, and mutation.
        /// </summary>
        /// <remarks>
        /// A portion of the best solutions (elites) is carried over unchanged, based on the elitism rate. The remaining solutions are generated
        /// by selecting parents, performing crossover to create offspring, and applying mutation with a probability defined by the configuration.
        /// The new population replaces the old one.
        /// </remarks>
        private void EvolveSingleGeneration()
        {
            var newPopulation = new List<Solution>();
            int eliteCount = (int)(_config.PopulationSize * _config.ElitismRate);

            AddElitesToPopulation(newPopulation, eliteCount);
            AddOffspringToPopulation(newPopulation, eliteCount);

            _population = newPopulation;
        }

        /// <summary>
        /// Adds the best solutions (elites) to the new population to preserve high-quality solutions.
        /// </summary>
        /// <param name="newPopulation">The new population being constructed.</param>
        /// <param name="eliteCount">The number of elite solutions to carry over.</param>
        /// <remarks>
        /// Selects the top solutions based on their scores, clones them to prevent unintended modifications, and adds them to the new population.
        /// </remarks>
        private void AddElitesToPopulation(List<Solution> newPopulation, int eliteCount)
        {
            newPopulation.AddRange(_population
                .OrderByDescending(s => s.Score)
                .Take(eliteCount)
                .Select(s => s.Clone()));
        }

        /// <summary>
        /// Generates offspring solutions through selection, crossover, and mutation to fill the new population.
        /// </summary>
        /// <param name="newPopulation">The new population being constructed.</param>
        /// <param name="eliteCount">The number of elite solutions already added, used to calculate the remaining slots.</param>
        /// <remarks>
        /// Uses tournament selection to choose two distinct parents, performs crossover to create a child solution, and applies mutation
        /// with a probability defined by the configuration. The process is repeated until the population is filled.
        /// </remarks>
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

        /// <summary>
        /// Checks whether the problem has a capacity issue (i.e., insufficient total vehicle capacity).
        /// </summary>
        /// <returns>True if the total vehicle capacity is less than the number of passengers; otherwise, false.</returns>
        public bool HasCapacityIssue()
        {
            return _hasCapacityIssue;
        }

        /// <summary>
        /// Determines whether a specific solution violates vehicle capacity constraints.
        /// </summary>
        /// <param name="solution">The solution to check.</param>
        /// <returns>True if any vehicle in the solution has more assigned passengers than its capacity; otherwise, false.</returns>
        private bool HasCapacityIssue(Solution solution)
        {
            return solution.Vehicles.Any(v => v.AssignedPassengers.Count > v.Capacity);
        }

        /// <summary>
        /// Retrieves the best solution from the current population based on its score.
        /// </summary>
        /// <returns>The solution with the highest score in the population.</returns>
        private Solution GetBestSolution()
        {
            return _population.OrderByDescending(s => s.Score).First();
        }

        /// <summary>
        /// Returns a copy of the current population.
        /// </summary>
        /// <returns>A list containing clones of all solutions in the current population. Returns an empty list if the population is null.</returns>
        public List<Solution> GetLatestPopulation()
        {
            return _population?.ToList() ?? new List<Solution>();
        }
    }
}