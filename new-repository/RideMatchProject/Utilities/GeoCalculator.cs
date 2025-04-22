using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GMap.NET;
namespace RideMatchProject.Utilities
{
    public static class GeoCalculator
    {
        private const double EarthRadiusKm = 6371.0;

        /// <summary>
        /// Calculates the distance between two geographical points using the Haversine formula
        /// </summary>
        public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EarthRadiusKm * c;
        }

        /// <summary>
        /// Calculates the distance between two points
        /// </summary>
        public static double CalculateDistance(PointLatLng point1, PointLatLng point2)
        {
            return CalculateDistance(point1.Lat, point1.Lng, point2.Lat, point2.Lng);
        }

        /// <summary>
        /// Calculates the total distance of a route made up of multiple points
        /// </summary>
        public static double CalculateRouteDistance(List<PointLatLng> points)
        {
            double totalDistance = 0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                totalDistance += CalculateDistance(points[i], points[i + 1]);
            }
            return totalDistance;
        }

        /// <summary>
        /// Converts degrees to radians
        /// </summary>
        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        /// <summary>
        /// Simple check if coordinates are within valid ranges
        /// </summary>
        public static bool IsValidLocation(double lat, double lng)
        {
            // Basic validity check for coordinates
            if (lat < -90 || lat > 90 || lng < -180 || lng > 180)
                return false;

            // More sophisticated checks could be added here to determine if point is on land, etc.
            return true;
        }
    }
}
