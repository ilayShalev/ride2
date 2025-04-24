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
    /// Manages driver data and database operations
    /// </summary>
    public class DriverDataManager
    {
        private readonly DatabaseService dbService;
        private readonly int userId;
        private readonly string username;

        public Vehicle Vehicle { get; private set; }
        public List<Passenger> AssignedPassengers { get; private set; } = new List<Passenger>();
        public DateTime? PickupTime { get; private set; }

        public DriverDataManager(DatabaseService dbService, int userId, string username)
        {
            this.dbService = dbService;
            this.userId = userId;
            this.username = username;
        }

        public async Task LoadDriverDataAsync()
        {
            try
            {
                Vehicle = await dbService.GetVehicleByUserIdAsync(userId);

                if (Vehicle == null)
                {
                    InitializeDefaultVehicle();
                    return;
                }

                // Get the destination tuple
                var destination = await dbService.GetDestinationAsync();

                // Use the common helper to determine which date to query
                // Access tuple elements correctly
                string queryDate = RouteScheduleHelper.GetRouteQueryDate(destination.TargetTime);

                // The GetDriverRouteAsync returns a tuple (Vehicle, List<Passenger>, DateTime?)
                var routeData = await dbService.GetDriverRouteAsync(userId, queryDate);

                // Properly unpack the tuple
                if (routeData.Vehicle != null)
                {
                    Vehicle = routeData.Vehicle;
                }

                AssignedPassengers = routeData.Passengers ?? new List<Passenger>();
                PickupTime = routeData.PickupTime;
            }
            catch (Exception ex)
            {
                LogError("Error loading driver data", ex);
                throw;
            }
        }

        private void InitializeDefaultVehicle()
        {
            Vehicle = new Vehicle
            {
                UserId = userId,
                Capacity = 4,
                IsAvailableTomorrow = true,
                DriverName = username
            };
        }

        public async Task<bool> UpdateVehicleAvailabilityAsync(bool isAvailable)
        {
            if (Vehicle == null) return false;

            bool success = await dbService.UpdateVehicleAvailabilityAsync(Vehicle.Id, isAvailable);

            if (success)
            {
                Vehicle.IsAvailableTomorrow = isAvailable;
            }

            return success;
        }

        public async Task<bool> UpdateVehicleCapacityAsync(int capacity)
        {
            if (Vehicle == null) return false;

            bool success = await dbService.UpdateVehicleCapacityAsync(userId, capacity);

            if (success)
            {
                Vehicle.Capacity = capacity;
            }

            return success;
        }

        public async Task<bool> UpdateVehicleLocationAsync(double latitude, double longitude, string address)
        {
            try
            {
                if (Vehicle == null || Vehicle.Id == 0)
                {
                    return await CreateNewVehicle(latitude, longitude, address);
                }

                return await UpdateExistingVehicle(latitude, longitude, address);
            }
            catch (Exception ex)
            {
                LogError("Error updating vehicle location", ex);
                return false;
            }
        }

        private async Task<bool> CreateNewVehicle(double latitude, double longitude, string address)
        {
            int vehicleId = await dbService.SaveDriverVehicleAsync(
                userId, Vehicle?.Capacity ?? 4, latitude, longitude, address);

            if (vehicleId > 0)
            {
                Vehicle = await dbService.GetVehicleByUserIdAsync(userId);
                return true;
            }

            return false;
        }

        private async Task<bool> UpdateExistingVehicle(double latitude, double longitude, string address)
        {
            bool success = await dbService.UpdateVehicleLocationAsync(
                userId, latitude, longitude, address);

            if (success)
            {
                Vehicle.StartLatitude = latitude;
                Vehicle.StartLongitude = longitude;
                Vehicle.StartAddress = address;
            }

            return success;
        }

        private void LogError(string message, Exception ex)
        {
            Console.WriteLine($"{message}: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}