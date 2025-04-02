using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Update the Passenger class in Models/Passenger.cs

namespace claudpro.Models
{
    public class Passenger
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Address { get; set; }  // Add this property

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Address))
                return $"{Name} (ID: {Id}, {Address})";

            return $"{Name} (ID: {Id})";
        }
    }
}
