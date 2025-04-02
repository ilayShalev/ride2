using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace claudpro.Models
{
    public class Solution
    {
        public List<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
        public double Score { get; set; }
        // Update the Clone method in Models/Solution.cs to handle the new address properties

        public Solution Clone()
        {
            // Create deep copy of the solution
            var clone = new Solution
            {
                Score = this.Score,
                Vehicles = new List<Vehicle>()
            };

            foreach (var vehicle in this.Vehicles)
            {
                clone.Vehicles.Add(new Vehicle
                {
                    Id = vehicle.Id,
                    Capacity = vehicle.Capacity,
                    StartLatitude = vehicle.StartLatitude,
                    StartLongitude = vehicle.StartLongitude,
                    StartAddress = vehicle.StartAddress, // Include address in clone
                    AssignedPassengers = new List<Passenger>(vehicle.AssignedPassengers),
                    TotalDistance = vehicle.TotalDistance,
                    TotalTime = vehicle.TotalTime
                });
            }

            return clone;
        }
    }
}
