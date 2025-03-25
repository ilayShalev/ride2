using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using claudpro.Models;
using claudpro.Utilities;

namespace claudpro.Services
{
    public class RideSharingGenetic
    {
        private readonly List<Passenger> passengers;
        private readonly List<Vehicle> vehicles;
        private readonly int populationSize;
        private readonly double destinationLat;
        private readonly double destinationLng;
        private readonly int targetTime;
        private readonly Random random = new Random();
        private List<Solution> population;

        public RideSharingGenetic(List<Passenger> passengers, List<Vehicle> vehicles, int populationSize,
            double destinationLat, double destinationLng, int targetTime)
        {
            this.passengers = passengers;
            this.vehicles = vehicles;
            this.populationSize = populationSize;
            this.destinationLat = destinationLat;
            this.destinationLng = destinationLng;
            this.targetTime = targetTime;
        }

        /// <summary>
        /// Solves the ride sharing problem using a genetic algorithm
        /// </summary>
        public Solution Solve(int generations, List<Solution> initialPopulation = null)
        {
            // Initialize population
            population = initialPopulation?.Count > 0
                ? initialPopulation
                : GenerateInitialPopulation();

            // Run genetic algorithm for specified number of generations
            for (int i = 0; i < generations; i++)
            {
                var newPopulation = new List<Solution>();

                // Elitism - keep the best solution
                newPopulation.Add(GetBestSolution());

                // Create new solutions through selection, crossover and mutation
                while (newPopulation.Count < populationSize)
                {
                    var parent1 = TournamentSelection();
                    var parent2 = TournamentSelection();
                    var child = Crossover(parent1, parent2);
                    Mutate(child);
                    newPopulation.Add(child);
                }

                population = newPopulation;
            }

            return GetBestSolution();
        }

        /// <summary>
        /// Gets the latest population of solutions
        /// </summary>
        public List<Solution> GetLatestPopulation()
        {
            return population;
        }

        /// <summary>
        /// Generates initial population of solutions
        /// </summary>
        private List<Solution> GenerateInitialPopulation()
        {
            var result = new List<Solution>();

            for (int i = 0; i < populationSize; i++)
            {
                var solution = new Solution { Vehicles = DeepCopyVehicles() };
                var unassigned = passengers.OrderBy(x => random.Next()).ToList(); // Shuffle passengers

                // Assign passengers to vehicles
                foreach (var vehicle in solution.Vehicles)
                {
                    int toAssign = Math.Min(vehicle.Capacity, unassigned.Count);
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
                        var vehicle = solution.Vehicles
                            .OrderBy(v => v.AssignedPassengers.Count)
                            .FirstOrDefault();

                        if (vehicle != null)
                        {
                            vehicle.AssignedPassengers.Add(passenger);
                            unassigned.Remove(passenger);
                        }
                    }
                }

                solution.Score = Evaluate(solution);
                result.Add(solution);
            }

            return result;
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
                AssignedPassengers = new List<Passenger>(),
                TotalDistance = 0,
                TotalTime = 0
            }).ToList();
        }

        /// <summary>
        /// Evaluates a solution and assigns a score
        /// </summary>
        private double Evaluate(Solution solution)
        {
            double totalDistance = 0;
            int assignedCount = 0;
            int usedVehicles = 0;

            foreach (var vehicle in solution.Vehicles)
            {
                if (vehicle.AssignedPassengers.Count == 0) continue;

                usedVehicles++;
                double vehicleDistance = 0;
                double currentLat = vehicle.StartLatitude;
                double currentLng = vehicle.StartLongitude;

                // Calculate distance for picking up each passenger
                foreach (var passenger in vehicle.AssignedPassengers)
                {
                    double legDistance = GeoCalculator.CalculateDistance(
                        currentLat, currentLng,
                        passenger.Latitude, passenger.Longitude);
                    vehicleDistance += legDistance;

                    currentLat = passenger.Latitude;
                    currentLng = passenger.Longitude;
                }

                // Add distance to final destination
                double destDistance = GeoCalculator.CalculateDistance(
                    currentLat, currentLng,
                    destinationLat, destinationLng);
                vehicleDistance += destDistance;

                vehicle.TotalDistance = vehicleDistance;
                vehicle.TotalTime = (vehicleDistance / 30.0) * 60; // Assuming 30 km/h, convert to minutes

                totalDistance += vehicleDistance;
                assignedCount += vehicle.AssignedPassengers.Count;
            }

            // Calculate score - lower distance is better, bonus for using fewer vehicles
            // and severe penalty for unassigned passengers
            double score = 10000.0 / (1 + totalDistance);

            // Penalty for unused capacity
            int unusedCapacity = vehicles.Sum(v => v.Capacity) - assignedCount;
            score -= unusedCapacity * 10;

            // Bonus for using fewer vehicles
            score += (vehicles.Count - usedVehicles) * 50;

            // Critical penalty for unassigned passengers
            if (assignedCount < passengers.Count)
            {
                score -= 1000 * (passengers.Count - assignedCount);
            }

            return score;
        }

        /// <summary>
        /// Selects a solution using tournament selection
        /// </summary>
        private Solution TournamentSelection()
        {
            int tournamentSize = Math.Min(5, population.Count);
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
            var assigned = new HashSet<int>();

            // First inherit from parent1 for half the vehicles
            for (int i = 0; i < child.Vehicles.Count / 2; i++)
            {
                var sourceVehicle = parent1.Vehicles[i];
                foreach (var passenger in sourceVehicle.AssignedPassengers)
                {
                    if (!assigned.Contains(passenger.Id) &&
                        child.Vehicles[i].AssignedPassengers.Count < child.Vehicles[i].Capacity)
                    {
                        child.Vehicles[i].AssignedPassengers.Add(passenger);
                        assigned.Add(passenger.Id);
                    }
                }
            }

            // Then inherit from parent2 for the other half
            for (int i = child.Vehicles.Count / 2; i < child.Vehicles.Count; i++)
            {
                var sourceVehicle = parent2.Vehicles[i];
                foreach (var passenger in sourceVehicle.AssignedPassengers)
                {
                    if (!assigned.Contains(passenger.Id) &&
                        child.Vehicles[i].AssignedPassengers.Count < child.Vehicles[i].Capacity)
                    {
                        child.Vehicles[i].AssignedPassengers.Add(passenger);
                        assigned.Add(passenger.Id);
                    }
                }
            }

            // Assign any remaining passengers
            var unassignedPassengers = passengers
                .Where(p => !assigned.Contains(p.Id))
                .ToList();

            foreach (var passenger in unassignedPassengers)
            {
                var availableVehicles = child.Vehicles
                    .Where(v => v.AssignedPassengers.Count < v.Capacity)
                    .OrderBy(v => v.AssignedPassengers.Count)
                    .ToList();

                if (availableVehicles.Any())
                {
                    availableVehicles.First().AssignedPassengers.Add(passenger);
                    assigned.Add(passenger.Id);
                }
                else
                {
                    // Overload a vehicle if necessary to ensure all passengers are assigned
                    var vehicle = child.Vehicles.OrderBy(v => v.AssignedPassengers.Count).First();
                    vehicle.AssignedPassengers.Add(passenger);
                    assigned.Add(passenger.Id);
                }
            }

            child.Score = Evaluate(child);
            return child;
        }

        /// <summary>
        /// Mutates a solution with a certain probability
        /// </summary>
        private void Mutate(Solution solution)
        {
            if (random.NextDouble() < 0.3) // 30% chance of mutation
            {
                int mutationType = random.Next(3);
                switch (mutationType)
                {
                    case 0: // Swap passengers between vehicles
                        SwapPassengers(solution);
                        break;
                    case 1: // Reorder passengers within a vehicle
                        ReorderPassengers(solution);
                        break;
                    case 2: // Move a passenger to another vehicle
                        MovePassenger(solution);
                        break;
                }

                solution.Score = Evaluate(solution);
            }
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

            // Select two random vehicles
            int idx1 = random.Next(vehiclesWithPassengers.Count);
            int idx2 = random.Next(vehiclesWithPassengers.Count);
            while (idx2 == idx1) idx2 = random.Next(vehiclesWithPassengers.Count);

            var vehicle1 = vehiclesWithPassengers[idx1];
            var vehicle2 = vehiclesWithPassengers[idx2];

            // Swap a random passenger from each vehicle
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
        /// Reorders passengers within a vehicle
        /// </summary>
        private void ReorderPassengers(Solution solution)
        {
            // Select a random vehicle with multiple passengers
            var vehiclesWithMultiplePassengers = solution.Vehicles
                .Where(v => v.AssignedPassengers.Count > 1)
                .ToList();

            if (vehiclesWithMultiplePassengers.Count == 0) return;

            var vehicle = vehiclesWithMultiplePassengers[random.Next(vehiclesWithMultiplePassengers.Count)];

            // Shuffle the passengers in this vehicle
            vehicle.AssignedPassengers = vehicle.AssignedPassengers
                .OrderBy(x => random.Next())
                .ToList();
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
            var targetVehicle = solution.Vehicles
                .Where(v => v.Id != sourceVehicle.Id)
                .OrderBy(x => random.Next())
                .FirstOrDefault();

            if (targetVehicle != null)
            {
                sourceVehicle.AssignedPassengers.RemoveAt(passengerIdx);
                targetVehicle.AssignedPassengers.Add(passenger);
            }
        }

        /// <summary>
        /// Gets the best solution from the current population
        /// </summary>
        private Solution GetBestSolution()
        {
            return population.OrderByDescending(s => s.Score).First();
        }
    }
}
