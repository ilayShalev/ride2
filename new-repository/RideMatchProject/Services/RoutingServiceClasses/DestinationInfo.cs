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
        public DateTime TargetArrivalTime { get; private set; }

        public DestinationInfo(double latitude, double longitude, DateTime targetArrivalTime)
        {
            Latitude = latitude;
            Longitude = longitude;
            TargetArrivalTime = targetArrivalTime;
        }

        public DestinationInfo(double latitude, double longitude, string targetTimeString = "08:00:00")
        {
            Latitude = latitude;
            Longitude = longitude;

            if (TimeSpan.TryParse(targetTimeString, out TimeSpan targetTime))
            {
                // Create a DateTime from today and the target time
                TargetArrivalTime = DateTime.Today.Add(targetTime);
            }
            else
            {
                // Default to 8:00 AM if parsing fails
                TargetArrivalTime = DateTime.Today.AddHours(8);
            }
        }
    }
}
