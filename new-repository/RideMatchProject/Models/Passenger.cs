using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Models
{
    public class Passenger
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Address { get; set; }

        // New properties for database integration
        public int UserId { get; set; }
        public bool IsAvailableTomorrow { get; set; } = true;
        public string EstimatedPickupTime { get; set; }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Address))
                return $"{Name} (ID: {Id}, {Address})";

            return $"{Name} (ID: {Id})";
        }
    }
}