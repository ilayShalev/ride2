using RideMatchProject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.AlgoritemClasses
{
    /// <summary>
    /// Stores the problem data for the ride sharing optimization
    /// </summary>
    public class ProblemData
    {
        public List<Passenger> Passengers { get; private set; }
        public List<Vehicle> Vehicles { get; private set; }
        public double DestinationLat { get; private set; }
        public double DestinationLng { get; private set; }
        public int TargetTime { get; private set; }

        public ProblemData(List<Passenger> passengers, List<Vehicle> vehicles,
            double destinationLat, double destinationLng, int targetTime)
        {
            Passengers = passengers;
            Vehicles = vehicles;
            DestinationLat = destinationLat;
            DestinationLng = destinationLng;
            TargetTime = targetTime;
        }
    }

}
