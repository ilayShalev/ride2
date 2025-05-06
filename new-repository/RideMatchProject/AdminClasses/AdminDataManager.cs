using RideMatchProject.Models;
using RideMatchProject.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.AdminClasses
{
    /// <summary>
    /// Central manager responsible for loading and holding all admin-relevant data.
    /// Handles users, vehicles, passengers, and destination information.
    /// </summary>
    public class AdminDataManager
    {
        private readonly DatabaseService _dbService;

        // Loaded application data
        public List<(int Id, string Username, string UserType, string Name, string Email, string Phone)> Users { get; private set; }
        public List<Vehicle> Vehicles { get; private set; }
        public List<Passenger> Passengers { get; private set; }
        public (int Id, string Name, double Latitude, double Longitude, string Address, string TargetTime) Destination { get; private set; }

        /// <summary>
        /// Constructs the data manager with a reference to the database service.
        /// </summary>
        public AdminDataManager(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        /// <summary>
        /// Loads all data types in sequence: users, vehicles, passengers, and destination.
        /// </summary>
        public async Task LoadAllDataAsync()
        {
            await LoadUsersAsync();
            await LoadVehiclesAsync();
            await LoadPassengersAsync();
            await LoadDestinationAsync();
        }

        /// <summary>
        /// Loads all users. Falls back to example data on failure.
        /// </summary>
        public async Task LoadUsersAsync()
        {
            try
            {
                Users = await _dbService.GetAllUsersAsync();
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError($"Error loading users: {ex.Message}", "Database Error");

                // Provide fallback data for admin panel usability
                Users = new List<(int, string, string, string, string, string)>
                {
                    (1, "admin", "Admin", "Administrator", "", ""),
                    (2, "driver1", "Driver", "John Driver", "", ""),
                    (3, "passenger1", "Passenger", "Alice Passenger", "", "")
                };
            }
        }

        /// <summary>
        /// Loads all vehicles from the database. Falls back to an empty list on error.
        /// </summary>
        public async Task LoadVehiclesAsync()
        {
            try
            {
                Vehicles = await _dbService.GetAllVehiclesAsync();
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError($"Error loading vehicles: {ex.Message}", "Database Error");

                Vehicles = new List<Vehicle>();
            }
        }

        /// <summary>
        /// Loads all passengers from the database. Falls back to an empty list on error.
        /// </summary>
        public async Task LoadPassengersAsync()
        {
            try
            {
                Passengers = await _dbService.GetAllPassengersAsync();
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError($"Error loading passengers: {ex.Message}", "Database Error");

                Passengers = new List<Passenger>();
            }
        }

        /// <summary>
        /// Loads destination configuration from the database. Uses default fallback on error.
        /// </summary>
        public async Task LoadDestinationAsync()
        {
            try
            {
                Destination = await _dbService.GetDestinationAsync();
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError($"Error loading destination: {ex.Message}", "Database Error");

                Destination = (0, "Default Destination", 0, 0, "Unknown", "08:00:00");
            }
        }
    }
}
