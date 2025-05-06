using RideMatchProject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.AlgoritemClasses
{
    /// <summary>
    /// Handles the selection of solutions in a genetic algorithm using the tournament selection method.
    /// This class is responsible for selecting a single solution from a population based on a tournament-style
    /// competition, where a subset of solutions is randomly chosen, and the one with the highest score is selected.
    /// </summary>
    public class SelectionOperator
    {
        private readonly GeneticAlgorithmConfig _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectionOperator"/> class with the specified genetic algorithm configuration.
        /// </summary>
        /// <param name="config">The configuration settings for the genetic algorithm, including the tournament size and random number generator.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
        public SelectionOperator(GeneticAlgorithmConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Performs tournament selection to choose a solution from a population.
        /// A random subset of solutions (competitors) is selected based on the tournament size, and the solution
        /// with the highest score is chosen as the winner. A clone of the winning solution is returned to preserve
        /// the original solution's state.
        /// </summary>
        /// <param name="population">The list of solutions from which to select a winner.</param>
        /// <returns>A cloned instance of the winning <see cref="Solution"/> with the highest score among the competitors.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="population"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="population"/> is empty.</exception>
        /// <remarks>
        /// The tournament size is determined by <see cref="GeneticAlgorithmConfig.TournamentSize"/>, but it is capped
        /// at the population size to avoid invalid selections. The method uses the random number generator from
        /// <see cref="GeneticAlgorithmConfig.Random"/> to select competitors randomly. The solution with the highest
        /// <see cref="Solution.Score"/> is selected as the winner.
        /// </remarks>
        public Solution TournamentSelection(List<Solution> population)
        {
            if (population == null)
                throw new ArgumentNullException(nameof(population));
            if (population.Count == 0)
                throw new ArgumentException("Population cannot be empty.", nameof(population));

            // Determine the tournament size, capped at the population size
            int tournamentSize = Math.Min(_config.TournamentSize, population.Count);
            var competitors = new List<Solution>(tournamentSize);

            // Randomly select competitors
            for (int i = 0; i < tournamentSize; i++)
            {
                int idx = _config.Random.Next(population.Count);
                competitors.Add(population[idx]);
            }

            // Select the competitor with the highest score
            var winner = competitors.OrderByDescending(s => s.Score).First();

            // Return a clone of the winner to preserve the original solution
            return winner.Clone();
        }
    }
}