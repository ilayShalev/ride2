using System;
using System.Collections.Generic;
using System.Linq;
using claudpro.Models;
using claudpro.Utilities;

namespace claudpro.Services
{
    /// <summary>
    /// Implements a genetic algorithm to optimize ride sharing assignments
    /// </summary>
    public class RideSharingGenetic
    {
        // Input data
        private readonly List<Passenger> passengers;
        private readonly List<Vehicle> vehicles;
        private readonly int populationSize;
        private readonly double destinationLat;
        private readonly double destinationLng;
        private readonly int targetTime; // Target arrival time in minutes from midnight
        private readonly Random random = new Random();

        // Algorithm state
        private List<Solution> population;
        private Solution bestSolution;
        private int generationsWithoutImprovement;
        private double bestScore;

        // Algorithm parameters
        private const double MutationRate = 0.3;
        private const double ElitismRate = 0.2;
        private const int TournamentSize = 5;
        private const int MaxGenerationsWithoutImprovement = 20;

        public RideSharingGenetic(List<Passenger> passengers, List<Vehicle> vehicles, int populationSize,
            double destinationLat, double destinationLng, int targetTime)
        {
            this.passengers = passengers ?? new List<Passenger>();
            this.vehicles = vehicles ?? new List<Vehicle>();
            this.populationSize = Math.Max(populationSize, 50); // Ensure minimum population size
            this.destinationLat = destinationLat;
            this.destinationLng = destinationLng;
            this.targetTime = targetTime;

            this.bestScore = double.MinValue;
            this.generationsWithoutImprovement = 0;
        }

        /// <summary>
        /// Solves the ride sharing problem using a genetic algorithm
        /// </summary>
        /// <param name="generations">Maximum number of generations to run</param>
        /// <param name="initialPopulation">Optional initial population</param>
        /// <returns>The best solution found</returns>
        public Solution Solve(int generations, List<Solution> initialPopulation = null)
        {
            // Validate inputs
            if (passengers.Count == 0 || vehicles.Count == 0)
                return new Solution { Vehicles = new List<Vehicle>() };

            // Initialize population
            population = initialPopulation?.Count > 0
                ? initialPopulation
                : GenerateInitialPopulation();

            // Track best solution
            bestSolution = GetBestSolution();
            bestScore = bestSolution.Score;
            generationsWithoutImprovement = 0;

            // Run genetic algorithm for specified number of generations or until convergence
            for (int i = 0; i < generations; i++)
            {
                // Create new generation
                EvolveSingleGeneration();

                // Check for improvement
                var currentBest = GetBestSolution();
                if (currentBest.Score > bestScore)
                {
                    bestSolution = currentBest.Clone();
                    bestScore = currentBest.Score;
                    generationsWithoutImprovement = 0;
                }
                else
                {
                    generationsWithoutImprovement++;
                }

                // Check termination conditions
                if (generationsWithoutImprovement >= MaxGenerationsWithoutImprovement)
                {
                    Console.WriteLine($"Converged after {i + 1} generations");
                    break;
                }
            }

            // Calculate exact distances for best solution
            CalculateExactMetrics(bestSolution);

            return bestSolution;
        }

        /// <summary>
        /// Evolves the population by one generation
        /// </summary>
        private void EvolveSingleGeneration()
        {
            var newPopulation = new List<Solution>();
            int eliteCount = (int)(populationSize * ElitismRate);

            // Elitism - keep the best solutions
            newPopulation.AddRange(population
                .OrderByDescending(s => s.Score)
                .Take(eliteCount)
                .Select(s => s.Clone()));

            // Fill the rest of the population with new solutions through selection, crossover and mutation
            while (newPopulation.Count < populationSize)
            {
                // Selection
                var parent1 = TournamentSelection();
                var parent2 = TournamentSelection();

                // Crossover
                var child = Crossover(parent1, parent2);

                // Mutation
                if (random.NextDouble() < MutationRate)
                {
                    Mutate(child);
                }

                // Add to new population
                newPopulation.Add(child);
            }

            // Replace population
            population = newPopulation;
        }

        /// <summary>
        /// Gets the latest population of solutions
        /// </summary>
        public List<Solution> GetLatestPopulation()
        {
            return population?.ToList() ?? new List<Solution>();
        }

        /// <summary>
        /// Generates initial population of solutions
        /// </summary>
        private List<Solution> GenerateInitialPopulation()
        {
            var result = new List<Solution>();

            // Add a greedy solution
            result.Add(CreateGreedySolution());

            // Add a solution that distributes passengers evenly
            result.Add(CreateEvenDistributionSolution());

            // Add random solutions for the rest
            while (result.Count < populationSize)
            {
                var solution = new Solution { Vehicles = DeepCopyVehicles() };
                var unassigned = passengers.OrderBy(x => random.Next()).ToList(); // Shuffle passengers

                // Assign passengers to vehicles
                foreach (var vehicle in solution.Vehicles)
                {
                    int maxToAssign = Math.Min(vehicle.Capacity, unassigned.Count);
                    int toAssign = random.Next(0, maxToAssign + 1); // Allow some vehicles to be empty

                    for (int j = 0; j < toAssign; j++)
                    {
                        var passenger = unassigned[0];
                        vehicle.AssignedPassengers.Add(passenger);
                        unassigned.RemoveAt(0);
                    }
                }

                // Try to assign any remaining passengers by overloading vehicles
                if (unassigned.Count > 0)
                {
                    foreach (var passenger in unassigned.ToList())
                    {
                        // Find the best vehicle to add this passenger to
                        var vehicle = solution.Vehicles
                            .OrderBy(v => v.AssignedPassengers.Count)
                            .ThenBy(v => CalculateAdditionalDistance(v, passenger))
                            .FirstOrDefault();

                        if (vehicle != null)
                        {
                            vehicle.AssignedPassengers.Add(passenger);
                            unassigned.Remove(passenger);
                        }
                    }
                }

                // Evaluate solution
                solution.Score = Evaluate(solution);
                result.Add(solution);
            }

            return result;
        }

        /// <summary>
        /// Creates a greedy solution that assigns passengers to closest vehicles first
        /// </summary>
        private Solution CreateGreedySolution()
        {
            var solution = new Solution { Vehicles = DeepCopyVehicles() };
            var remainingPassengers = passengers.ToList();

            // Sort passengers by distance to destination (farthest first)
            remainingPassengers.Sort((p1, p2) =>
                GeoCalculator.CalculateDistance(p2.Latitude, p2.Longitude, destinationLat, destinationLng)
                    .CompareTo(GeoCalculator.CalculateDistance(p1.Latitude, p1.Longitude, destinationLat, destinationLng)));

            // Assign passengers to closest available vehicle
            foreach (var passenger in remainingPassengers)
            {
                var availableVehicles = solution.Vehicles
                    .Where(v => v.AssignedPassengers.Count < v.Capacity)
                    .ToList();

                if (availableVehicles.Count == 0)
                {
                    // All vehicles are at capacity, assign to the least overloaded
                    var vehicle = solution.Vehicles.OrderBy(v => v.AssignedPassengers.Count).First();
                    vehicle.AssignedPassengers.Add(passenger);
                }
                else
                {
                    // Find closest vehicle
                    var closestVehicle = availableVehicles
                        .OrderBy(v => GeoCalculator.CalculateDistance(
                            v.StartLatitude, v.StartLongitude, passenger.Latitude, passenger.Longitude))
                        .First();

                    closestVehicle.AssignedPassengers.Add(passenger);
                }
            }

            solution.Score = Evaluate(solution);
            return solution;
        }

        /// <summary>
        /// Creates a solution that distributes passengers evenly among vehicles
        /// </summary>
        private Solution CreateEvenDistributionSolution()
        {
            var solution = new Solution { Vehicles = DeepCopyVehicles() };
            var remainingPassengers = passengers.ToList();

            // Determine base number of passengers per vehicle
            int totalCapacity = vehicles.Sum(v => v.Capacity);
            int passengersPerVehicle = Math.Min(passengers.Count / vehicles.Count,
                                               Math.Min(totalCapacity / vehicles.Count,
                                                      vehicles.Min(v => v.Capacity)));

            // Distribute passengers
            foreach (var vehicle in solution.Vehicles)
            {
                // Find passengersPerVehicle closest passengers to this vehicle
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

            // Assign any remaining passengers
            if (remainingPassengers.Count > 0)
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

                // If there are still remaining passengers, assign them to the least loaded vehicles
                foreach (var passenger in remainingPassengers)
                {
                    var vehicle = solution.Vehicles
                        .OrderBy(v => v.AssignedPassengers.Count)
                        .First();

                    vehicle.AssignedPassengers.Add(passenger);
                }
            }

            solution.Score = Evaluate(solution);
            return solution;
        }

        /// <summary>
        /// Creates a deep copy of vehicles without passengers
        /// </summary>
        private List<Vehicle> DeepCopyVehicles()
        {
            return vehicles.Select(v => new Vehicle
            {
                Id = v.Id,
                Capacity = v.Capacity,
                StartLatitude = v.StartLatitude,
                StartLongitude = v.StartLongitude,
                StartAddress = v.StartAddress,
                DriverName = v.DriverName,
                AssignedPassengers = new List<Passenger>(),
                TotalDistance = 0,
                TotalTime = 0
            }).ToList();
        }

        /// <summary>
        /// Evaluates a solution and assigns a score (higher is better)
        /// </summary>
        private double Evaluate(Solution solution)
        {
            double totalDistance = 0;
            double maxTime = 0;
            int assignedCount = 0;
            int usedVehicles = 0;
            int overloadedVehicles = 0;

            foreach (var vehicle in solution.Vehicles)
            {
                if (vehicle.AssignedPassengers.Count == 0) continue;

                usedVehicles++;

                if (vehicle.AssignedPassengers.Count > vehicle.Capacity)
                {
                    overloadedVehicles++;
                }

                // Calculate route metrics
                var metrics = CalculateRouteMetrics(vehicle);
                vehicle.TotalDistance = metrics.TotalDistance;
                vehicle.TotalTime = metrics.TotalTime;

                // Update global metrics
                totalDistance += metrics.TotalDistance;
                maxTime = Math.Max(maxTime, metrics.TotalTime);
                assignedCount += vehicle.AssignedPassengers.Count;
            }

            // Calculate score components
            double distanceScore = totalDistance > 0 ? 1000.0 / totalDistance : 0;
            double assignmentScore = assignedCount * 100.0; // High priority for assigning all passengers
            double vehicleUtilizationScore = usedVehicles * 10.0; // Prefer using fewer vehicles
            double overloadPenalty = overloadedVehicles * -200.0; // Severe penalty for overloading
            double timeScore = maxTime > 0 ? 500.0 / maxTime : 0; // Prefer shorter routes

            // Unassigned passenger penalty
            double unassignedPenalty = (passengers.Count - assignedCount) * -1000.0;

            // Calculate final score - higher is better
            double score = distanceScore + assignmentScore - vehicleUtilizationScore +
                           overloadPenalty + timeScore + unassignedPenalty;

            return score;
        }

        /// <summary>
        /// Calculates route metrics for a vehicle
        /// </summary>
        private (double TotalDistance, double TotalTime) CalculateRouteMetrics(Vehicle vehicle)
        {
            if (vehicle.AssignedPassengers.Count == 0)
                return (0, 0);

            double totalDistance = 0;
            double currentLat = vehicle.StartLatitude;
            double currentLng = vehicle.StartLongitude;

            // Add distance for each passenger pickup
            foreach (var passenger in vehicle.AssignedPassengers)
            {
                double legDistance = GeoCalculator.CalculateDistance(
                    currentLat, currentLng,
                    passenger.Latitude, passenger.Longitude);
                totalDistance += legDistance;

                currentLat = passenger.Latitude;
                currentLng = passenger.Longitude;
            }

            // Add distance to final destination
            double destDistance = GeoCalculator.CalculateDistance(
                currentLat, currentLng,
                destinationLat, destinationLng);
            totalDistance += destDistance;

            // Estimate time assuming average speed of 30 km/h
            double totalTime = (totalDistance / 30.0) * 60; // Convert to minutes

            return (totalDistance, totalTime);
        }

        /// <summary>
        /// Calculates the additional distance if a passenger is added to a vehicle
        /// </summary>
        private double CalculateAdditionalDistance(Vehicle vehicle, Passenger passenger)
        {
            if (vehicle.AssignedPassengers.Count == 0)
            {
                // Distance from vehicle start to passenger to destination
                return GeoCalculator.CalculateDistance(
                    vehicle.StartLatitude, vehicle.StartLongitude,
                    passenger.Latitude, passenger.Longitude) +
                   GeoCalculator.CalculateDistance(
                    passenger.Latitude, passenger.Longitude,
                    destinationLat, destinationLng);
            }
            else
            {
                // Current route distance
                var lastPassenger = vehicle.AssignedPassengers.Last();
                double currentDistance = GeoCalculator.CalculateDistance(
                    lastPassenger.Latitude, lastPassenger.Longitude,
                    destinationLat, destinationLng);

                // New route distance with additional passenger
                double newDistance = GeoCalculator.CalculateDistance(
                    lastPassenger.Latitude, lastPassenger.Longitude,
                    passenger.Latitude, passenger.Longitude) +
                    GeoCalculator.CalculateDistance(
                    passenger.Latitude, passenger.Longitude,
                    destinationLat, destinationLng);

                return newDistance - currentDistance;
            }
        }

        /// <summary>
        /// Selects a solution using tournament selection
        /// </summary>
        private Solution TournamentSelection()
        {
            int tournamentSize = Math.Min(TournamentSize, population.Count);
            var competitors = new List<Solution>();

            for (int i = 0; i < tournamentSize; i++)
            {
                int idx = random.Next(population.Count);
                competitors.Add(population[idx]);
            }

            var winner = competitors.OrderByDescending(s => s.Score).First();
            return winner.Clone();
        }

        /// <summary>
        /// Performs crossover between two parent solutions
        /// </summary>
        private Solution Crossover(Solution parent1, Solution parent2)
        {
            var child = new Solution { Vehicles = DeepCopyVehicles() };
            var assignedPassengerIds = new HashSet<int>();

            // Inheritance strategy:
            // 1. Take vehicle assignments from parent1 for half the vehicles
            // 2. Take vehicle assignments from parent2 for the other half
            // 3. Handle unassigned passengers

            // First inherit from parent1 for some vehicles
            int inheritFromParent1Count = random.Next(1, child.Vehicles.Count);

            for (int i = 0; i < inheritFromParent1Count; i++)
            {
                if (i >= parent1.Vehicles.Count) break;

                var sourceVehicle = parent1.Vehicles[i];
                var targetVehicle = child.Vehicles[i];

                // Copy passenger assignments that don't exceed capacity
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

            // Then inherit from parent2 for the other vehicles
            for (int i = inheritFromParent1Count; i < child.Vehicles.Count; i++)
            {
                if (i >= parent2.Vehicles.Count) break;

                var sourceVehicle = parent2.Vehicles[i];
                var targetVehicle = child.Vehicles[i];

                // Copy passenger assignments that don't exceed capacity
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

            // Assign any unassigned passengers
            var unassignedPassengers = passengers
                .Where(p => !assignedPassengerIds.Contains(p.Id))
                .ToList();

            if (unassignedPassengers.Count > 0)
            {
                // Use a greedy approach to assign remaining passengers to the best vehicle
                foreach (var passenger in unassignedPassengers)
                {
                    // Try to find a vehicle with available capacity
                    var availableVehicles = child.Vehicles
                        .Where(v => v.AssignedPassengers.Count < v.Capacity)
                        .OrderBy(v => CalculateAdditionalDistance(v, passenger))
                        .ToList();

                    if (availableVehicles.Any())
                    {
                        // Assign to the best available vehicle
                        availableVehicles.First().AssignedPassengers.Add(passenger);
                    }
                    else
                    {
                        // If no vehicle has capacity, assign to the one that minimizes additional distance
                        var bestVehicle = child.Vehicles
                            .OrderBy(v => CalculateAdditionalDistance(v, passenger))
                            .ThenBy(v => v.AssignedPassengers.Count)
                            .First();

                        bestVehicle.AssignedPassengers.Add(passenger);
                    }
                }
            }

            // Evaluate the child solution
            child.Score = Evaluate(child);
            return child;
        }

        /// <summary>
        /// Mutates a solution by applying one of several mutation strategies
        /// </summary>
        private void Mutate(Solution solution)
        {
            // Choose a mutation strategy
            int mutationType = random.Next(4);

            switch (mutationType)
            {
                case 0:
                    // Swap passengers between vehicles
                    SwapPassengers(solution);
                    break;
                case 1:
                    // Reorder passengers within a vehicle
                    ReorderPassengers(solution);
                    break;
                case 2:
                    // Move a passenger to another vehicle
                    MovePassenger(solution);
                    break;
                case 3:
                    // Optimize routes using 2-opt local search
                    OptimizeRoutes(solution);
                    break;
            }

            // Re-evaluate solution
            solution.Score = Evaluate(solution);
        }

        /// <summary>
        /// Swaps passengers between two vehicles
        /// </summary>
        private void SwapPassengers(Solution solution)
        {
            // Only proceed if we have at least 2 vehicles with passengers
            var vehiclesWithPassengers = solution.Vehicles
                .Where(v => v.AssignedPassengers.Count > 0)
                .ToList();

            if (vehiclesWithPassengers.Count < 2) return;

            // Select two random different vehicles
            int idx1 = random.Next(vehiclesWithPassengers.Count);
            int idx2 = random.Next(vehiclesWithPassengers.Count - 1);
            if (idx2 >= idx1) idx2++; // Ensure idx2 != idx1

            var vehicle1 = vehiclesWithPassengers[idx1];
            var vehicle2 = vehiclesWithPassengers[idx2];

            // Swap a random passenger from each vehicle if both have passengers
            if (vehicle1.AssignedPassengers.Count > 0 && vehicle2.AssignedPassengers.Count > 0)
            {
                int passengerIdx1 = random.Next(vehicle1.AssignedPassengers.Count);
                int passengerIdx2 = random.Next(vehicle2.AssignedPassengers.Count);

                var passenger1 = vehicle1.AssignedPassengers[passengerIdx1];
                var passenger2 = vehicle2.AssignedPassengers[passengerIdx2];

                vehicle1.AssignedPassengers[passengerIdx1] = passenger2;
                vehicle2.AssignedPassengers[passengerIdx2] = passenger1;
            }
        }

        /// <summary>
        /// Reorders passengers within a vehicle using a simple local optimization
        /// </summary>
        private void ReorderPassengers(Solution solution)
        {
            // Select a random vehicle with multiple passengers
            var vehiclesWithMultiplePassengers = solution.Vehicles
                .Where(v => v.AssignedPassengers.Count > 1)
                .ToList();

            if (vehiclesWithMultiplePassengers.Count == 0) return;

            var vehicle = vehiclesWithMultiplePassengers[random.Next(vehiclesWithMultiplePassengers.Count)];

            // Try to optimize the order using a partial 2-opt approach
            if (vehicle.AssignedPassengers.Count > 2)
            {
                // Pick two random positions and swap the segments
                int pos1 = random.Next(vehicle.AssignedPassengers.Count);
                int pos2 = random.Next(vehicle.AssignedPassengers.Count);

                if (pos1 != pos2)
                {
                    // Ensure pos1 < pos2
                    if (pos1 > pos2)
                    {
                        int temp = pos1;
                        pos1 = pos2;
                        pos2 = temp;
                    }

                    // Reverse the segment between pos1 and pos2
                    var passengers = vehicle.AssignedPassengers;
                    for (int i = 0; i < (pos2 - pos1) / 2; i++)
                    {
                        var temp = passengers[pos1 + i];
                        passengers[pos1 + i] = passengers[pos2 - i];
                        passengers[pos2 - i] = temp;
                    }
                }
            }
            else if (vehicle.AssignedPassengers.Count == 2)
            {
                // For just 2 passengers, potentially swap them
                if (random.Next(2) == 0)
                {
                    var temp = vehicle.AssignedPassengers[0];
                    vehicle.AssignedPassengers[0] = vehicle.AssignedPassengers[1];
                    vehicle.AssignedPassengers[1] = temp;
                }
            }
        }

        /// <summary>
        /// Moves a passenger from one vehicle to another
        /// </summary>
        private void MovePassenger(Solution solution)
        {
            // Select a random vehicle with passengers
            var vehiclesWithPassengers = solution.Vehicles
                .Where(v => v.AssignedPassengers.Count > 0)
                .ToList();

            if (vehiclesWithPassengers.Count == 0) return;

            var sourceVehicle = vehiclesWithPassengers[random.Next(vehiclesWithPassengers.Count)];

            // Select a random passenger
            int passengerIdx = random.Next(sourceVehicle.AssignedPassengers.Count);
            var passenger = sourceVehicle.AssignedPassengers[passengerIdx];

            // Find a target vehicle that is not the source
            var targetOptions = solution.Vehicles
                .Where(v => v.Id != sourceVehicle.Id)
                .ToList();

            if (targetOptions.Count > 0)
            {
                var targetVehicle = targetOptions[random.Next(targetOptions.Count)];

                // Move the passenger
                sourceVehicle.AssignedPassengers.RemoveAt(passengerIdx);
                targetVehicle.AssignedPassengers.Add(passenger);
            }
        }

        /// <summary>
        /// Optimizes routes using a 2-opt local search on a random vehicle
        /// </summary>
        private void OptimizeRoutes(Solution solution)
        {
            // Select a random vehicle with at least 4 passengers (needed for 2-opt to be useful)
            var vehiclesWithManyPassengers = solution.Vehicles
                .Where(v => v.AssignedPassengers.Count >= 4)
                .ToList();

            if (vehiclesWithManyPassengers.Count == 0) return;

            var vehicle = vehiclesWithManyPassengers[random.Next(vehiclesWithManyPassengers.Count)];

            // 2-opt local search: try all possible 2-edge swaps and pick the best
            double bestDistance = CalculateRouteMetrics(vehicle).TotalDistance;
            var bestOrder = new List<Passenger>(vehicle.AssignedPassengers);

            // Try a limited number of random swaps (full 2-opt would be O(n²) which is too expensive)
            int maxSwaps = Math.Min(10, vehicle.AssignedPassengers.Count * (vehicle.AssignedPassengers.Count - 1) / 2);

            for (int swapCount = 0; swapCount < maxSwaps; swapCount++)
            {
                // Pick two random positions
                int i = random.Next(vehicle.AssignedPassengers.Count);
                int j = random.Next(vehicle.AssignedPassengers.Count);

                if (i == j) continue;

                // Ensure i < j
                if (i > j)
                {
                    int temp = i;
                    i = j;
                    j = temp;
                }

                // Make a copy and apply the 2-opt swap (reverse the path between i and j)
                var newOrder = new List<Passenger>(vehicle.AssignedPassengers);
                newOrder.Reverse(i, j - i + 1);

                // Temporarily replace the route
                var originalPassengers = vehicle.AssignedPassengers;
                vehicle.AssignedPassengers = newOrder;

                // Calculate new route distance
                double newDistance = CalculateRouteMetrics(vehicle).TotalDistance;

                // If it's better, keep this as the best so far
                if (newDistance < bestDistance)
                {
                    bestDistance = newDistance;
                    bestOrder = new List<Passenger>(newOrder);
                }

                // Restore the original route for the next iteration
                vehicle.AssignedPassengers = originalPassengers;
            }

            // Apply the best route found
            vehicle.AssignedPassengers = bestOrder;
        }

        /// <summary>
        /// Gets the best solution from the current population
        /// </summary>
        private Solution GetBestSolution()
        {
            return population.OrderByDescending(s => s.Score).First();
        }

        /// <summary>
        /// Calculates exact metrics for a solution after optimization
        /// </summary>
        private void CalculateExactMetrics(Solution solution)
        {
            foreach (var vehicle in solution.Vehicles)
            {
                if (vehicle.AssignedPassengers.Count > 0)
                {
                    var metrics = CalculateRouteMetrics(vehicle);
                    vehicle.TotalDistance = metrics.TotalDistance;
                    vehicle.TotalTime = metrics.TotalTime;
                }
            }
        }
    }
}