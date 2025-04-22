// Update the Vehicle class to include DepartureTime property
using System;
using System.Collections.Generic;

namespace RideMatchProject.Models
{
    public class Vehicle
    {
        // Basic vehicle information
        public int Id { get; set; }

        // Link to user (driver)
        public int UserId { get; set; }

        // Capacity/number of seats
        public int Capacity { get; set; }

        // Starting location
        public double StartLatitude { get; set; }
        public double StartLongitude { get; set; }
        public string StartAddress { get; set; }

        // Assigned passengers for routing
        public List<Passenger> AssignedPassengers { get; set; } = new List<Passenger>();

        // Route metrics
        public double TotalDistance { get; set; }
        public double TotalTime { get; set; }

        // Departure time calculated by scheduler
        public string DepartureTime { get; set; }

        // Availability for scheduling
        public bool IsAvailableTomorrow { get; set; } = true;

        // Driver details (derived from the user)
        public string DriverName { get; set; }

        // Additional properties for UI display
        public string Model { get; set; } = "Standard Vehicle";
        public string Color { get; set; } = "White";
        public string LicensePlate { get; set; } = "Not Set";

        public override string ToString()
        {
            string displayName = !string.IsNullOrEmpty(DriverName) ? DriverName : $"Vehicle {Id}";

            if (!string.IsNullOrEmpty(StartAddress))
                return $"{displayName} ({Model}, {Color}, {LicensePlate}, Capacity: {Capacity}, {StartAddress})";

            return $"{displayName} ({Model}, {Color}, {LicensePlate}, Capacity: {Capacity})";
        }
    }
}