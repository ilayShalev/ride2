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
    /// Handles access to the data layer and database operations for passenger-related data in a ride-matching application.
    /// Manages passenger information, availability, location updates, and vehicle assignments.
    /// </summary>
    public class PassengerDataAccessLayer
    {
        /// <summary>
        /// The database service used for performing database operations.
        /// </summary>
        private readonly DatabaseService _databaseService;

        /// <summary>
        /// The unique identifier of the user associated with the passenger.
        /// </summary>
        private readonly int _userId;

        /// <summary>
        /// The username of the user associated with the passenger.
        /// </summary>
        private readonly string _username;

        /// <summary>
        /// Gets the current passenger's data, if loaded.
        /// </summary>
        public Passenger CurrentPassenger { get; private set; }

        /// <summary>
        /// Gets the vehicle assigned to the passenger, if any.
        /// </summary>
        public Vehicle AssignedVehicle { get; private set; }

        /// <summary>
        /// Gets the scheduled pickup time for the passenger, if assigned.
        /// </summary>
        public DateTime? PickupTime { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PassengerDataAccessLayer"/> class.
        /// </summary>
        /// <param name="databaseService">The <see cref="DatabaseService"/> used for database operations.</param>
        /// <param name="userId">The unique identifier of the user associated with the passenger.</param>
        /// <param name="username">The username of the user associated with the passenger.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="databaseService"/> or <paramref name="username"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="userId"/> is less than or equal to zero or <paramref name="username"/> is empty or whitespace.</exception>
        public PassengerDataAccessLayer(DatabaseService databaseService, int userId, string username)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService), "DatabaseService cannot be null.");
            if (userId <= 0)
                throw new ArgumentException("User ID must be a positive integer.", nameof(userId));
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null, empty, or whitespace.", nameof(username));

            _userId = userId;
            _username = username;
        }

        /// <summary>
        /// Loads passenger data asynchronously, including passenger details, assigned vehicle, and pickup time.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// Retrieves the passenger by user ID, fetches destination data, and queries vehicle assignments based on the route query date.
        /// Updates <see cref="CurrentPassenger"/>, <see cref="AssignedVehicle"/>, and <see cref="PickupTime"/> properties.
        /// If the passenger exists, retrieves additional details like estimated pickup time.
        /// </remarks>
        /// <exception cref="DataException">Thrown when an error occurs during data retrieval, wrapping the underlying exception.</exception>
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

                    var assignment = await _databaseService.GetPassengerAssignmentAsync(_userId, queryDate);

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

        /// <summary>
        /// Updates the passenger's availability status asynchronously.
        /// </summary>
        /// <param name="isAvailable">The new availability status (<c>true</c> for available, <c>false</c> for unavailable).</param>
        /// <returns>
        /// A <see cref="Task"/> containing <c>true</c> if the update was successful and the <see cref="CurrentPassenger"/> was updated;
        /// otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// Returns <c>false</c> if <see cref="CurrentPassenger"/> is null or if the database update fails.
        /// Updates the <see cref="Passenger.IsAvailableTomorrow"/> property on success.
        /// </remarks>
        public async Task<bool> UpdatePassengerAvailabilityAsync(bool isAvailable)
        {
            if (CurrentPassenger == null)
            {
                return false;
            }

            try
            {
                bool success = await _databaseService.UpdatePassengerAvailabilityAsync(CurrentPassenger.Id, isAvailable);

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

        /// <summary>
        /// Updates or creates the passenger's location and address asynchronously.
        /// </summary>
        /// <param name="latitude">The latitude of the passenger's location.</param>
        /// <param name="longitude">The longitude of the passenger's location.</param>
        /// <param name="address">The textual address of the passenger's location.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// If <see cref="CurrentPassenger"/> is null, creates a new passenger with the provided data.
        /// Otherwise, updates the existing passenger's location and address in both the local <see cref="CurrentPassenger"/> and the database.
        /// </remarks>
        /// <exception cref="DataException">Thrown when an error occurs during data creation or update, wrapping the underlying exception.</exception>
        public async Task UpdatePassengerLocationAsync(double latitude, double longitude, string address)
        {
            try
            {
                if (CurrentPassenger == null)
                {
                    int passengerId = await _databaseService.AddPassengerAsync(_userId, _username, latitude, longitude, address);

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

                    await _databaseService.UpdatePassengerAsync(CurrentPassenger.Id, CurrentPassenger.Name, latitude, longitude, address);
                }
            }
            catch (Exception ex)
            {
                throw new DataException("Failed to update passenger location", ex);
            }
        }
    }
}