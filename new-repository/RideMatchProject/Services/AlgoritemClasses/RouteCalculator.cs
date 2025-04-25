using RideMatchProject.Models;
using RideMatchProject.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.AlgoritemClasses
{
    /// <summary>
    /// Calculates route metrics for vehicles and passengers
    /// </summary>
    public class RouteCalculator
    {
        private readonly ProblemData _problemData;

        public RouteCalculator(ProblemData problemData)
        {
            _problemData = problemData;
        }

        public double CalculateRouteMetrics(Vehicle vehicle)
        {
            if (vehicle.AssignedPassengers.Count == 0)
            {
                return 0;
            }

            double totalDistance = 0;
            double currentLat = vehicle.StartLatitude;
            double currentLng = vehicle.StartLongitude;

            totalDistance = AddDistanceForPassengerPickups(vehicle, currentLat, currentLng, totalDistance);
            totalDistance = AddDistanceToDestination(vehicle, totalDistance);

            return totalDistance;
        }

        private double AddDistanceForPassengerPickups(Vehicle vehicle, double currentLat,
            double currentLng, double totalDistance)
        {
            double result = totalDistance;
            double lat = currentLat;
            double lng = currentLng;

            foreach (var passenger in vehicle.AssignedPassengers)
            {
                double legDistance = GeoCalculator.CalculateDistance(
                    lat, lng, passenger.Latitude, passenger.Longitude);
                result += legDistance;

                lat = passenger.Latitude;
                lng = passenger.Longitude;
            }

            return result;
        }

        private double AddDistanceToDestination(Vehicle vehicle, double totalDistance)
        {
            if (vehicle.AssignedPassengers.Count == 0)
            {
                return totalDistance;
            }

            var lastPassenger = vehicle.AssignedPassengers.Last();
            double destDistance = GeoCalculator.CalculateDistance(
                lastPassenger.Latitude, lastPassenger.Longitude,
                _problemData.DestinationLat, _problemData.DestinationLng);

            return totalDistance + destDistance;
        }

        public double CalculateAdditionalDistance(Vehicle vehicle, Passenger passenger)
        {
            if (vehicle.AssignedPassengers.Count == 0)
            {
                return CalculateDistanceForEmptyVehicle(vehicle, passenger);
            }
            else
            {
                return CalculateDistanceWithPassenger(vehicle, passenger);
            }
        }

        private double CalculateDistanceForEmptyVehicle(Vehicle vehicle, Passenger passenger)
        {
            return GeoCalculator.CalculateDistance(
                vehicle.StartLatitude, vehicle.StartLongitude,
                passenger.Latitude, passenger.Longitude) +
               GeoCalculator.CalculateDistance(
                passenger.Latitude, passenger.Longitude,
                _problemData.DestinationLat, _problemData.DestinationLng);
        }

        private double CalculateDistanceWithPassenger(Vehicle vehicle, Passenger passenger)
        {
            var lastPassenger = vehicle.AssignedPassengers.Last();

            double currentDistance = GeoCalculator.CalculateDistance(
                lastPassenger.Latitude, lastPassenger.Longitude,
                _problemData.DestinationLat, _problemData.DestinationLng);

            double newDistance = GeoCalculator.CalculateDistance(
                lastPassenger.Latitude, lastPassenger.Longitude,
                passenger.Latitude, passenger.Longitude) +
                GeoCalculator.CalculateDistance(
                passenger.Latitude, passenger.Longitude,
                _problemData.DestinationLat, _problemData.DestinationLng);

            return newDistance - currentDistance;
        }

        public void CalculateExactMetrics(Solution solution)
        {
            foreach (var vehicle in solution.Vehicles)
            {
                if (vehicle.AssignedPassengers.Count > 0)
                {
                    vehicle.TotalDistance = CalculateRouteMetrics(vehicle);
                }
            }
        }

        public List<Vehicle> DeepCopyVehicles()
        {
            return _problemData.Vehicles.Select(v => new Vehicle
            {
                Id = v.Id,
                Capacity = v.Capacity,
                StartLatitude = v.StartLatitude,
                StartLongitude = v.StartLongitude,
                StartAddress = v.StartAddress,
                DriverName = v.DriverName,
                AssignedPassengers = new List<Passenger>(),
                TotalDistance = 0,
            }).ToList();
        }
    }
}
