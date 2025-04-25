using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.AlgoritemClasses
{
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
}
