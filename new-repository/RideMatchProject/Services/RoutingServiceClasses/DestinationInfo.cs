using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.RoutingServiceClasses
{
    /// <summary>
    /// Stores destination information
    /// </summary>
    public class DestinationInfo
    {
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }

        public DateTime TargetArrivelTime { get; private set; }

        public DestinationInfo(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }
    }
}
