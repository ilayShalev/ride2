using RideMatchProject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.AlgoritemClasses
{
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
}
