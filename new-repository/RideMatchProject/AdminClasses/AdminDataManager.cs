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
    /// Central manager for application data
    /// </summary>
    public class AdminDataManager
    {
        private readonly DatabaseService _dbService;

        public List<(int Id, string Username, string UserType, string Name, string Email, string Phone)> Users { get; private set; }
        public List<Vehicle> Vehicles { get; private set; }
        public List<Passenger> Passengers { get; private set; }
        public (int Id, string Name, double Latitude, double Longitude, string Address, string TargetTime) Destination { get; private set; }

        public AdminDataManager(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        public async Task LoadAllDataAsync()
        {
            await LoadUsersAsync();
            await LoadVehiclesAsync();
            await LoadPassengersAsync();
            await LoadDestinationAsync();
        }

        public async Task LoadUsersAsync()
        {
            try
            {
                Users = await _dbService.GetAllUsersAsync();
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error loading users: {ex.Message}",
                    "Database Error"
                );

                // Fallback to sample data
                Users = new List<(int Id, string Username, string UserType, string Name, string Email, string Phone)>
                {
                    (1, "admin", "Admin", "Administrator", "", ""),
                    (2, "driver1", "Driver", "John Driver", "", ""),
                    (3, "passenger1", "Passenger", "Alice Passenger", "", "")
                };
            }
        }

        public async Task LoadVehiclesAsync()
        {
            try
            {
                Vehicles = await _dbService.GetAllVehiclesAsync();
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error loading vehicles: {ex.Message}",
                    "Database Error"
                );

                // Fallback to empty list
                Vehicles = new List<Vehicle>();
            }
        }

        public async Task LoadPassengersAsync()
        {
            try
            {
                Passengers = await _dbService.GetAllPassengersAsync();
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error loading passengers: {ex.Message}",
                    "Database Error"
                );

                // Fallback to empty list
                Passengers = new List<Passenger>();
            }
        }

        public async Task LoadDestinationAsync()
        {
            try
            {
                Destination = await _dbService.GetDestinationAsync();
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error loading destination: {ex.Message}",
                    "Database Error"
                );

                // Fallback to default
                Destination = (0, "Default Destination", 0, 0, "Unknown", "08:00:00");
            }
        }
    }
}
