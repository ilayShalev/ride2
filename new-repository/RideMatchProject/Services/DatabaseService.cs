using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using RideMatchProject.Models;
using RideMatchProject.Services.DatabaseServiceClasses;

namespace RideMatchProject.Services
{
 
    /// <summary>
    /// Main database service facade that provides access to all domain services
    /// </summary>
    public class DatabaseService : IDisposable
    {
        private readonly DatabaseManager _dbManager;
        private readonly UserService _userService;
        private readonly VehicleService _vehicleService;
        private readonly PassengerService _passengerService;
        private readonly DestinationService _destinationService;
        private readonly RouteService _routeService;
        private readonly SettingsService _settingsService;
        private bool _disposed = false;

        public DatabaseService(string dbFilePath = "ridematch.db")
        {
            _dbManager = new DatabaseManager(dbFilePath);
            _userService = new UserService(_dbManager);
            _vehicleService = new VehicleService(_dbManager);
            _passengerService = new PassengerService(_dbManager);
            _destinationService = new DestinationService(_dbManager);
            _routeService = new RouteService(_dbManager);
            _settingsService = new SettingsService(_dbManager);
        }

        public SQLiteConnection GetConnection()
        {
            return _dbManager.GetConnection();
        }

        #region User Methods

        public Task<(bool Success, string UserType, int UserId)> AuthenticateUserAsync(
            string username, string password)
        {
            return _userService.AuthenticateUserAsync(username, password);
        }

        public Task<int> AddUserAsync(string username, string password,
            string userType, string name, string email = "", string phone = "")
        {
            return _userService.AddUserAsync(username, password, userType, name, email, phone);
        }

        public Task<bool> UpdateUserAsync(int userId, string name, string email, string phone)
        {
            return _userService.UpdateUserAsync(userId, name, email, phone);
        }

        public Task<bool> UpdateUserProfileAsync(int userId, string userType,
            string name, string email, string phone)
        {
            return _userService.UpdateUserProfileAsync(userId, userType, name, email, phone);
        }

        public Task<bool> ChangePasswordAsync(int userId, string newPassword)
        {
            return _userService.ChangePasswordAsync(userId, newPassword);
        }

        public Task<(string Username, string UserType, string Name, string Email, string Phone)>
            GetUserInfoAsync(int userId)
        {
            return _userService.GetUserInfoAsync(userId);
        }

        public Task<List<(int Id, string Username, string UserType, string Name, string Email, string Phone)>>
            GetAllUsersAsync()
        {
            return _userService.GetAllUsersAsync();
        }

        public Task<bool> DeleteUserAsync(int userId)
        {
            return _userService.DeleteUserAsync(userId);
        }

        public Task<List<(int Id, string Username, string Name)>> GetUsersByTypeAsync(string userType)
        {
            return _userService.GetUsersByTypeAsync(userType);
        }

        #endregion

        #region Vehicle Methods

        public Task<int> AddVehicleAsync(int userId, int capacity,
            double startLatitude, double startLongitude, string startAddress = "")
        {
            return _vehicleService.AddVehicleAsync(userId, capacity, startLatitude, startLongitude, startAddress);
        }

        public Task<bool> UpdateVehicleAsync(int vehicleId, int capacity,
            double startLatitude, double startLongitude, string startAddress = "")
        {
            return _vehicleService.UpdateVehicleAsync(vehicleId, capacity, startLatitude, startLongitude, startAddress);
        }

        public Task<bool> UpdateVehicleAvailabilityAsync(int vehicleId, bool isAvailable)
        {
            return _vehicleService.UpdateVehicleAvailabilityAsync(vehicleId, isAvailable);
        }

        public Task<List<Vehicle>> GetAllVehiclesAsync()
        {
            return _vehicleService.GetAllVehiclesAsync();
        }

        public Task<List<Vehicle>> GetAvailableVehiclesAsync()
        {
            return _vehicleService.GetAvailableVehiclesAsync();
        }

        public Task<Vehicle> GetVehicleByUserIdAsync(int userId)
        {
            return _vehicleService.GetVehicleByUserIdAsync(userId);
        }

        public Task<Vehicle> GetVehicleByIdAsync(int vehicleId)
        {
            return _vehicleService.GetVehicleByIdAsync(vehicleId);
        }

        public Task<int> SaveDriverVehicleAsync(int userId, int capacity,
            double startLatitude, double startLongitude, string startAddress = "")
        {
            return _vehicleService.SaveDriverVehicleAsync(userId, capacity, startLatitude, startLongitude, startAddress);
        }

        public Task<bool> UpdateVehicleCapacityAsync(int userId, int capacity)
        {
            return _vehicleService.SaveDriverVehicleAsync(userId, capacity, 0, 0, "").ContinueWith(t => t.Result > 0);
        }

        public Task<bool> UpdateVehicleLocationAsync(int userId, double latitude, double longitude, string address = "")
        {
            return _vehicleService.SaveDriverVehicleAsync(userId, 4, latitude, longitude, address)
                .ContinueWith(t => t.Result > 0);
        }

        public Task<bool> DeleteVehicleAsync(int vehicleId)
        {
            return _vehicleService.DeleteVehicleAsync(vehicleId);
        }

        #endregion

        #region Passenger Methods

        public Task<int> AddPassengerAsync(int userId, string name,
            double latitude, double longitude, string address = "")
        {
            return _passengerService.AddPassengerAsync(userId, name, latitude, longitude, address);
        }

        public Task<bool> UpdatePassengerAsync(int passengerId, string name,
            double latitude, double longitude, string address = "")
        {
            return _passengerService.UpdatePassengerAsync(passengerId, name, latitude, longitude, address);
        }

        public Task<bool> UpdatePassengerAvailabilityAsync(int passengerId, bool isAvailable)
        {
            return _passengerService.UpdatePassengerAvailabilityAsync(passengerId, isAvailable);
        }

        public Task<List<Passenger>> GetAvailablePassengersAsync()
        {
            return _passengerService.GetAvailablePassengersAsync();
        }

        public Task<List<Passenger>> GetAllPassengersAsync()
        {
            return _passengerService.GetAllPassengersAsync();
        }

        public Task<Passenger> GetPassengerByUserIdAsync(int userId)
        {
            return _passengerService.GetPassengerByUserIdAsync(userId);
        }

        public Task<Passenger> GetPassengerByIdAsync(int passengerId)
        {
            return _passengerService.GetPassengerByIdAsync(passengerId);
        }

        public Task<bool> DeletePassengerAsync(int passengerId)
        {
            return _passengerService.DeletePassengerAsync(passengerId);
        }

        #endregion

        #region Destination Methods

        public Task<(int Id, string Name, double Latitude, double Longitude, string Address, string TargetTime)>
            GetDestinationAsync()
        {
            return _destinationService.GetDestinationAsync();
        }

        public Task<bool> UpdateDestinationAsync(string name, double latitude,
            double longitude, string targetTime, string address = "")
        {
            return _destinationService.UpdateDestinationAsync(name, latitude, longitude, targetTime, address);
        }

        #endregion

        #region Route Methods

        public Task<int> SaveSolutionAsync(Solution solution, string date)
        {
            return _routeService.SaveSolutionAsync(solution, date);
        }

        public Task<Solution> GetSolutionForDateAsync(string date)
        {
            return _routeService.GetSolutionForDateAsync(date);
        }

        public Task<(Vehicle Vehicle, List<Passenger> Passengers, DateTime? PickupTime)>
            GetDriverRouteAsync(int userId, string date)
        {
            return _routeService.GetDriverRouteAsync(userId, date);
        }

        public Task<(Vehicle AssignedVehicle, DateTime? PickupTime)> GetPassengerAssignmentAsync(int userId, string date)
        {
            return _routeService.GetPassengerAssignmentAsync(userId, date);
        }

        public Task<bool> UpdatePickupTimesAsync(int routeDetailId, Dictionary<int, string> passengerPickupTimes)
        {
            return _routeService.UpdatePickupTimesAsync(routeDetailId, passengerPickupTimes);
        }

        public Task ResetAvailabilityAsync()
        {
            return _routeService.ResetAvailabilityAsync();
        }

        public Task<List<(int RouteId, DateTime GeneratedTime, int VehicleCount, int PassengerCount)>>
            GetRouteHistoryAsync()
        {
            return _routeService.GetRouteHistoryAsync();
        }

        #endregion

        #region Settings Methods

        public Task SaveSchedulingSettingsAsync(bool isEnabled, DateTime scheduledTime)
        {
            return _settingsService.SaveSchedulingSettingsAsync(isEnabled, scheduledTime);
        }

        public Task<(bool IsEnabled, DateTime ScheduledTime)> GetSchedulingSettingsAsync()
        {
            return _settingsService.GetSchedulingSettingsAsync();
        }

        public Task LogSchedulingRunAsync(DateTime runTime, string status,
            int routesGenerated, int passengersAssigned, string errorMessage = null)
        {
            return _settingsService.LogSchedulingRunAsync(runTime, status,
                routesGenerated, passengersAssigned, errorMessage);
        }

        public Task<List<(DateTime RunTime, string Status, int RoutesGenerated, int PassengersAssigned)>>
            GetSchedulingLogAsync()
        {
            return _settingsService.GetSchedulingLogAsync();
        }

        public Task<bool> SaveSettingAsync(string settingName, string settingValue)
        {
            return _settingsService.SaveSettingAsync(settingName, settingValue);
        }

        public Task<string> GetSettingAsync(string settingName, string defaultValue = "")
        {
            return _settingsService.GetSettingAsync(settingName, defaultValue);
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _dbManager?.Dispose();
                }
                _disposed = true;
            }
        }

        #endregion
    }
}