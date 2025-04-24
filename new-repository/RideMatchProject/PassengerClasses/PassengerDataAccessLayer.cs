using RideMatchProject.Models;
using RideMatchProject.Services;
using RideMatchProject.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.PassengerClasses
{

    /// <summary>
    /// Handles access to data layer and database operations
    /// </summary>
    public class PassengerDataAccessLayer
    {
        private readonly DatabaseService _databaseService;
        private readonly int _userId;
        private readonly string _username;

        public Passenger CurrentPassenger { get; private set; }
        public Vehicle AssignedVehicle { get; private set; }
        public DateTime? PickupTime { get; private set; }

        public PassengerDataAccessLayer(DatabaseService databaseService, int userId, string username)
        {
            _databaseService = databaseService;
            _userId = userId;
            _username = username;
        }



        public async Task LoadPassengerDataAsync()
        {
            try
            {
                CurrentPassenger = await _databaseService.GetPassengerByUserIdAsync(_userId);

                if (CurrentPassenger != null)
                {
                    var destination = await _databaseService.GetDestinationAsync();

                    // Use the common helper to determine which date to query
                    string queryDate = RouteScheduleHelper.GetRouteQueryDate(destination.TargetTime);

                    var assignment = await _databaseService.GetPassengerAssignmentAsync(
                        _userId, queryDate);

                    AssignedVehicle = assignment.AssignedVehicle;
                    PickupTime = assignment.PickupTime;

                    if (CurrentPassenger.Id > 0)
                    {
                        var fullPassenger = await _databaseService.GetPassengerByIdAsync(CurrentPassenger.Id);

                        if (fullPassenger != null && !string.IsNullOrEmpty(fullPassenger.EstimatedPickupTime))
                        {
                            CurrentPassenger.EstimatedPickupTime = fullPassenger.EstimatedPickupTime;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new DataException("Failed to load passenger data", ex);
            }
        }
        public async Task<bool> UpdatePassengerAvailabilityAsync(bool isAvailable)
        {
            if (CurrentPassenger == null)
            {
                return false;
            }

            try
            {
                bool success = await _databaseService.UpdatePassengerAvailabilityAsync(
                    CurrentPassenger.Id, isAvailable);

                if (success)
                {
                    CurrentPassenger.IsAvailableTomorrow = isAvailable;
                }

                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task UpdatePassengerLocationAsync(double latitude, double longitude, string address)
        {
            try
            {
                if (CurrentPassenger == null)
                {
                    int passengerId = await _databaseService.AddPassengerAsync(
                        _userId, _username, latitude, longitude, address);

                    CurrentPassenger = new Passenger
                    {
                        Id = passengerId,
                        UserId = _userId,
                        Name = _username,
                        Latitude = latitude,
                        Longitude = longitude,
                        Address = address,
                        IsAvailableTomorrow = true
                    };
                }
                else
                {
                    CurrentPassenger.Latitude = latitude;
                    CurrentPassenger.Longitude = longitude;
                    CurrentPassenger.Address = address;

                    await _databaseService.UpdatePassengerAsync(
                        CurrentPassenger.Id,
                        CurrentPassenger.Name,
                        latitude,
                        longitude,
                        address);
                }
            }
            catch (Exception ex)
            {
                throw new DataException("Failed to update passenger location", ex);
            }
        }
    }
}
