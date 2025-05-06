using RideMatchProject.Models;
using RideMatchProject.Services;
using RideMatchProject.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.DriverClasses
{
    /// <summary>
    /// Manages driver data and database operations for a ride-matching application, handling vehicle information,
    /// assigned passengers, and pickup times for a specific driver.
    /// </summary>
    public class DriverDataManager
    {
        private readonly DatabaseService dbService; // Service for database operations
        private readonly int userId;               // Unique identifier for the driver
        private readonly string username;          // Username of the driver

        /// <summary>
        /// Gets the driver's vehicle information.
        /// </summary>
        public Vehicle Vehicle { get; private set; }

        /// <summary>
        /// Gets the list of passengers assigned to the driver.
        /// </summary>
        public List<Passenger> AssignedPassengers { get; private set; } = new List<Passenger>();

        /// <summary>
        /// Gets the scheduled pickup time for the driver's route, if available.
        /// </summary>
        public DateTime? PickupTime { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DriverDataManager"/> class.
        /// </summary>
        /// <param name="dbService">The database service used for data operations.</param>
        /// <param name="userId">The unique identifier of the driver.</param>
        /// <param name="username">The username of the driver.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="dbService"/> or <paramref name="username"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="userId"/> is less than or equal to zero.</exception>
        public DriverDataManager(DatabaseService dbService, int userId, string username)
        {
            if (dbService == null)
                throw new ArgumentNullException(nameof(dbService));
            if (userId <= 0)
                throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be a positive integer.");
            if (string.IsNullOrEmpty(username))
                throw new ArgumentNullException(nameof(username));

            this.dbService = dbService;
            this.userId = userId;
            this.username = username;
        }

        /// <summary>
        /// Asynchronously loads driver-related data, including vehicle information, assigned passengers, and pickup time.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown if an error occurs while loading data from the database.</exception>
        public async Task LoadDriverDataAsync()
        {
            try
            {
                // Load vehicle information for the driver
                Vehicle = await dbService.GetVehicleByUserIdAsync(userId);

                if (Vehicle == null)
                {
                    // If no vehicle exists, initialize a default vehicle
                    InitializeDefaultVehicle();
                    return;
                }

                // Get the destination and target time for route planning
                var destination = await dbService.GetDestinationAsync();

                // Determine the query date for route data based on the destination's target time
                string queryDate = RouteScheduleHelper.GetRouteQueryDate(destination.TargetTime);

                // Load route data, which includes vehicle, passengers, and pickup time
                var routeData = await dbService.GetDriverRouteAsync(userId, queryDate);

                // Update vehicle if route data provides a valid vehicle
                if (routeData.Vehicle != null)
                {
                    Vehicle = routeData.Vehicle;
                }

                // Update assigned passengers, defaulting to an empty list if null
                AssignedPassengers = routeData.Passengers ?? new List<Passenger>();
                PickupTime = routeData.PickupTime; // Set the pickup time
            }
            catch (Exception ex)
            {
                // Log the error and rethrow to allow callers to handle it
                LogError("Error loading driver data", ex);
                throw;
            }
        }

        /// <summary>
        /// Initializes a default vehicle for the driver when no vehicle exists in the database.
        /// </summary>
        private void InitializeDefaultVehicle()
        {
            Vehicle = new Vehicle
            {
                UserId = userId,             // Associate with the driver
                Capacity = 4,                // Default capacity of 4 passengers
                IsAvailableTomorrow = true,  // Default to available
                DriverName = username        // Set driver's username
            };
        }

        /// <summary>
        /// Asynchronously updates the vehicle's availability status for tomorrow.
        /// </summary>
        /// <param name="isAvailable">True if the vehicle is available tomorrow, false otherwise.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation, with a boolean indicating success.</returns>
        public async Task<bool> UpdateVehicleAvailabilityAsync(bool isAvailable)
        {
            if (Vehicle == null)
                return false; // Cannot update if no vehicle exists

            // Update availability in the database
            bool success = await dbService.UpdateVehicleAvailabilityAsync(Vehicle.Id, isAvailable);

            if (success)
            {
                // Update local vehicle object if database update succeeds
                Vehicle.IsAvailableTomorrow = isAvailable;
            }

            return success;
        }

        /// <summary>
        /// Asynchronously updates the vehicle's passenger capacity.
        /// </summary>
        /// <param name="capacity">The new passenger capacity for the vehicle.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation, with a boolean indicating success.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="capacity"/> is negative.</exception>
        public async Task<bool> UpdateVehicleCapacityAsync(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity cannot be negative.");
            if (Vehicle == null)
                return false; // Cannot update if no vehicle exists

            // Update capacity in the database
            bool success = await dbService.UpdateVehicleCapacityAsync(userId, capacity);

            if (success)
            {
                // Update local vehicle object if database update succeeds
                Vehicle.Capacity = capacity;
            }

            return success;
        }

        /// <summary>
        /// Asynchronously updates the vehicle's location information, creating a new vehicle if none exists.
        /// </summary>
        /// <param name="latitude">The latitude of the vehicle's starting location.</param>
        /// <param name="longitude">The longitude of the vehicle's starting location.</param>
        /// <param name="address">The human-readable address of the vehicle's starting location.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation, with a boolean indicating success.</returns>
        public async Task<bool> UpdateVehicleLocationAsync(double latitude, double longitude, string address)
        {
            try
            {
                if (Vehicle == null || Vehicle.Id == 0)
                {
                    // Create a new vehicle if none exists or ID is invalid
                    return await CreateNewVehicle(latitude, longitude, address);
                }

                // Update existing vehicle's location
                return await UpdateExistingVehicle(latitude, longitude, address);
            }
            catch (Exception ex)
            {
                // Log any errors and return false to indicate failure
                LogError("Error updating vehicle location", ex);
                return false;
            }
        }

        /// <summary>
        /// Asynchronously creates a new vehicle with the specified location and default properties.
        /// </summary>
        /// <param name="latitude">The latitude of the vehicle's starting location.</param>
        /// <param name="longitude">The longitude of the vehicle's starting location.</param>
        /// <param name="address">The human-readable address of the vehicle's starting location.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation, with a boolean indicating success.</returns>
        private async Task<bool> CreateNewVehicle(double latitude, double longitude, string address)
        {
            // Save new vehicle to the database with default or existing capacity
            int vehicleId = await dbService.SaveDriverVehicleAsync(
                userId, Vehicle?.Capacity ?? 4, latitude, longitude, address);

            if (vehicleId > 0)
            {
                // Load the newly created vehicle to update the local state
                Vehicle = await dbService.GetVehicleByUserIdAsync(userId);
                return true;
            }

            return false; // Return false if vehicle creation fails
        }

        /// <summary>
        /// Asynchronously updates the location of an existing vehicle.
        /// </summary>
        /// <param name="latitude">The new latitude of the vehicle's starting location.</param>
        /// <param name="longitude">The new longitude of the vehicle's starting location.</param>
        /// <param name="address">The new human-readable address of the vehicle's starting location.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation, with a boolean indicating success.</returns>
        private async Task<bool> UpdateExistingVehicle(double latitude, double longitude, string address)
        {
            // Update location in the database
            bool success = await dbService.UpdateVehicleLocationAsync(
                userId, latitude, longitude, address);

            if (success)
            {
                // Update local vehicle object if database update succeeds
                Vehicle.StartLatitude = latitude;
                Vehicle.StartLongitude = longitude;
                Vehicle.StartAddress = address;
            }

            return success;
        }

        /// <summary>
        /// Logs an error message and exception details to the console.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        /// <param name="ex">The exception containing error details.</param>
        private void LogError(string message, Exception ex)
        {
            Console.WriteLine($"{message}: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}