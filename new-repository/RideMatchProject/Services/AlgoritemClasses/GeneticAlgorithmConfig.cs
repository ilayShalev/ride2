using System;

namespace RideMatchProject.Services.AlgoritemClasses
{
    /// <summary>
    /// Stores configuration parameters for the genetic algorithm used in ride-sharing optimization.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the settings that control the behavior of the genetic algorithm, such as population size,
    /// mutation rate, elitism rate, tournament size, and convergence criteria. These parameters influence the algorithm's
    /// exploration of the solution space, balance between exploitation and exploration, and termination conditions.
    /// </remarks>
    public class GeneticAlgorithmConfig
    {
        /// <summary>
        /// Gets the size of the population, i.e., the number of solutions maintained in each generation.
        /// </summary>
        /// <remarks>
        /// A larger population size increases diversity but may slow down the algorithm. The value is ensured to be at least 50
        /// to maintain sufficient diversity for effective exploration. This property is read-only after initialization.
        /// </remarks>
        public int PopulationSize { get; private set; }

        /// <summary>
        /// Gets the probability that a child solution undergoes mutation after crossover.
        /// </summary>
        /// <remarks>
        /// The mutation rate is set to 0.3 (30%), meaning 30% of offspring solutions are mutated to introduce random changes,
        /// promoting exploration of the solution space. This property is read-only and fixed at initialization.
        /// </remarks>
        public double MutationRate { get; private set; } = 0.3;

        /// <summary>
        /// Gets the proportion of the population that is preserved as elites in each generation.
        /// </summary>
        /// <remarks>
        /// The elitism rate is set to 0.2 (20%), meaning the top 20% of solutions (based on score) are carried over unchanged
        /// to the next generation to preserve high-quality solutions. This property is read-only and fixed at initialization.
        /// </remarks>
        public double ElitismRate { get; private set; } = 0.2;

        /// <summary>
        /// Gets the number of solutions randomly selected for tournament selection.
        /// </summary>
        /// <remarks>
        /// The tournament size is set to 5, meaning 5 solutions are randomly chosen from the population, and the best among them
        /// is selected as a parent for crossover. A larger tournament size increases selection pressure, favoring better solutions.
        /// This property is read-only and fixed at initialization.
        /// </remarks>
        public int TournamentSize { get; private set; } = 5;

        /// <summary>
        /// Gets the maximum number of generations allowed without improvement in the best solution before terminating the algorithm.
        /// </summary>
        /// <remarks>
        /// This value is set to 20, meaning the algorithm will stop if the best solution's score does not improve for 20 consecutive
        /// generations, indicating convergence. This property is read-only and fixed at initialization.
        /// </remarks>
        public int MaxGenerationsWithoutImprovement { get; private set; } = 20;

        /// <summary>
        /// Gets the random number generator used for stochastic operations in the genetic algorithm.
        /// </summary>
        /// <remarks>
        /// The <see cref="Random"/> instance is used for operations such as selecting vehicles for crossover, deciding whether to mutate,
        /// and choosing candidates for tournament selection. It is initialized once and reused to ensure consistent randomness.
        /// This property is read-only.
        /// </remarks>
        public Random Random { get; private set; } = new Random();

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneticAlgorithmConfig"/> class with the specified population size.
        /// </summary>
        /// <param name="populationSize">The desired size of the population. Must be a positive integer.</param>
        /// <remarks>
        /// The constructor sets the <see cref="PopulationSize"/> to the provided value, ensuring it is at least 50 to maintain
        /// sufficient diversity. Other properties (e.g., <see cref="MutationRate"/>, <see cref="ElitismRate"/>, <see cref="TournamentSize"/>,
        /// and <see cref="MaxGenerationsWithoutImprovement"/>) are initialized with default values that balance exploration and exploitation.
        /// The <see cref="Random"/> instance is created to support stochastic operations.
        /// </remarks>
        public GeneticAlgorithmConfig(int populationSize)
        {
            if (populationSize <= 0)
            {
                throw new ArgumentException("Population size must be positive.", nameof(populationSize));
            }
            PopulationSize = Math.Max(populationSize, 50);
        }
    }
}