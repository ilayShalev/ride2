using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace claudpro.Models
{
    public class Vehicle
    {
        public int Id { get; set; }
        public int Capacity { get; set; }
        public double StartLatitude { get; set; }
        public double StartLongitude { get; set; }
        public List<Passenger> AssignedPassengers { get; set; } = new List<Passenger>();
        public double TotalDistance { get; set; }
        public double TotalTime { get; set; }

        public override string ToString()
        {
            return $"Vehicle {Id} (Capacity: {Capacity})";
        }
    }
}
