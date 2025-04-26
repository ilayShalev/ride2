using GMap.NET;
using Newtonsoft.Json;
using RideMatchProject.Models;
using RideMatchProject.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.Services.MapServiceClasses
{
    /// <summary>
    /// Handles directions and routing operations
    /// </summary>
    public class DirectionsService : IDisposable
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, List<PointLatLng>> _routeCache;
        private bool _disposed = false;

        public DirectionsService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = CreateHttpClient();
            _routeCache = new Dictionary<string, List<PointLatLng>>();
        }

        private HttpClient CreateHttpClient()
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            return client;
        }

        public async Task<List<PointLatLng>> GetDirectionsAsync(List<PointLatLng> waypoints)
        {
            if (HasInsufficientWaypoints(waypoints))
            {
                return null;
            }

            try
            {
                string cacheKey = GenerateCacheKey(waypoints);

                if (_routeCache.ContainsKey(cacheKey))
                {
                    return _routeCache[cacheKey];
                }
                Console.WriteLine($"Requesting directions with {waypoints.Count} waypoints");

                string url = BuildDirectionsUrl(waypoints);
                Console.WriteLine($"Google API URL: {url}");

                string response = await _httpClient.GetStringAsync(url);
                Console.WriteLine($"Google API response received: {response.Substring(0, Math.Min(100, response.Length))}...");

                var points = ProcessDirectionsResponse(response);
                Console.WriteLine($"Processed {points?.Count ?? 0} points from response");

                if (points != null)
                {
                    _routeCache[cacheKey] = points;
                }

                return points;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in GetDirectionsAsync: {ex.Message}");

                HandleDirectionsError(ex);
                return null;
            }
        }

        private bool HasInsufficientWaypoints(List<PointLatLng> waypoints)
        {
            return waypoints == null || waypoints.Count < 2;
        }

        private string GenerateCacheKey(List<PointLatLng> waypoints)
        {
            return string.Join("|", waypoints.Select(p => $"{p.Lat},{p.Lng}"));
        }

        private string BuildDirectionsUrl(List<PointLatLng> waypoints)
        {
            var origin = waypoints[0];
            var destination = waypoints.Last();
            var intermediates = waypoints.Skip(1).Take(waypoints.Count - 2).ToList();

            string waypointsParam = intermediates.Any()
                ? $"waypoints={string.Join("|", intermediates.Select(p => $"{p.Lat},{p.Lng}"))}&"
                : "";

            return $"https://maps.googleapis.com/maps/api/directions/json?" +
                $"origin={origin.Lat},{origin.Lng}&" +
                $"destination={destination.Lat},{destination.Lng}&" +
                waypointsParam +
                $"key={_apiKey}";
        }

        private List<PointLatLng> ProcessDirectionsResponse(string response)
        {
            dynamic data = JsonConvert.DeserializeObject(response);

            if (data.status != "OK")
            {
                string errorMessage = GetErrorMessage(data);
                throw new Exception($"Google Directions API error: {errorMessage}");
            }

            var points = new List<PointLatLng>();

            foreach (var leg in data.routes[0].legs)
            {
                foreach (var step in leg.steps)
                {
                    points.AddRange(PolylineEncoder.Decode(step.polyline.points.ToString()));
                }
            }

            return points;
        }

        private string GetErrorMessage(dynamic responseData)
        {
            string errorMessage = responseData.status.ToString();

            if (responseData.error_message != null)
            {
                errorMessage += $": {responseData.error_message.ToString()}";
            }

            return errorMessage;
        }

        private void HandleDirectionsError(Exception ex)
        {
            MessageBox.Show(
                $"Error getting directions: {ex.Message}",
                "Directions Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }

        public async Task<RouteDetails> GetRouteDetailsAsync(Vehicle vehicle, double destinationLat, double destinationLng, DateTime arrivalTime)
        {
            if (IsInvalidVehicleData(vehicle))
            {
                return null;
            }

            try
            {
                string url = BuildRouteDetailsUrl(vehicle, destinationLat, destinationLng,arrivalTime);
                string response = await _httpClient.GetStringAsync(url);

                return ProcessRouteDetailsResponse(response, vehicle);
            }
            catch (Exception ex)
            {
                HandleRouteDetailsError(ex);
                return null;
            }
        }

        private bool IsInvalidVehicleData(Vehicle vehicle)
        {
            return vehicle == null || vehicle.AssignedPassengers == null || vehicle.AssignedPassengers.Count == 0;
        }

        private string BuildRouteDetailsUrl(Vehicle vehicle, double destinationLat, double destinationLng, DateTime arrivalTime)
        {
            string origin = $"{vehicle.StartLatitude},{vehicle.StartLongitude}";
            string destination = $"{destinationLat},{destinationLng}";

            string waypointsStr = string.Join("|",
                vehicle.AssignedPassengers.Select(p => $"{p.Latitude},{p.Longitude}"));

            string waypointsParam = vehicle.AssignedPassengers.Any()
                ? $"&waypoints={waypointsStr}"
                : "";


            long arrivalTimestamp = new DateTimeOffset(arrivalTime).ToUnixTimeSeconds();

            return $"https://maps.googleapis.com/maps/api/directions/json?" +
                $"origin={origin}" +
                $"&destination={destination}" +
                $"&arrival_time={arrivalTimestamp}" +
                waypointsParam +
                $"&key={_apiKey}";
        }

        private RouteDetails ProcessRouteDetailsResponse(string response, Vehicle vehicle)
        {
            dynamic data = JsonConvert.DeserializeObject(response);

            if (data.status.ToString() != "OK")
            {
                string errorMessage = GetErrorMessage(data);
                throw new Exception($"Google Directions API error: {errorMessage}");
            }

            var routeDetail = CreateEmptyRouteDetails(vehicle.Id);
            ProcessRouteLegs(data, vehicle, routeDetail);

            return routeDetail;
        }

        private RouteDetails CreateEmptyRouteDetails(int vehicleId)
        {
            return new RouteDetails
            {
                VehicleId = vehicleId,
                StopDetails = new List<StopDetail>()
            };
        }

        private void ProcessRouteLegs(dynamic data, Vehicle vehicle, RouteDetails routeDetail)
        {
            double totalDistance = 0;
            double totalTime = 0;

            for (int i = 0; i < data.routes[0].legs.Count; i++)
            {
                var leg = data.routes[0].legs[i];

                double distance = Convert.ToDouble(leg.distance.value) / 1000.0;
                double time = Convert.ToDouble(leg.duration.value) / 60.0;

                totalDistance += distance;
                totalTime += time;

                AddStopDetail(routeDetail, vehicle, i, distance, time, totalDistance, totalTime);
            }

            routeDetail.TotalDistance = totalDistance;
            routeDetail.TotalTime = totalTime;
        }

        private void AddStopDetail(
            RouteDetails routeDetail,
            Vehicle vehicle,
            int index,
            double distance,
            double time,
            double totalDistance,
            double totalTime)
        {
            string stopName = index < vehicle.AssignedPassengers.Count
                ? vehicle.AssignedPassengers[index].Name
                : "Destination";

            int passengerId = index < vehicle.AssignedPassengers.Count
                ? vehicle.AssignedPassengers[index].Id
                : -1;

            routeDetail.StopDetails.Add(new StopDetail
            {
                StopNumber = index + 1,
                PassengerId = passengerId,
                PassengerName = stopName,
                DistanceFromPrevious = distance,
                TimeFromPrevious = time,
                CumulativeDistance = totalDistance,
                CumulativeTime = totalTime
            });
        }

        private void HandleRouteDetailsError(Exception ex)
        {
            MessageBox.Show(
                $"Error getting route details: {ex.Message}",
                "Route Details Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }

        public RouteDetails EstimateRouteDetails(Vehicle vehicle, double destinationLat, double destinationLng)
        {
            if (IsInvalidVehicleData(vehicle))
            {
                return null;
            }

            try
            {
                var routeDetail = CreateEmptyRouteDetails(vehicle.Id);
                EstimateRouteStops(vehicle, destinationLat, destinationLng, routeDetail);

                return routeDetail;
            }
            catch (Exception ex)
            {
                HandleRouteEstimationError(ex);
                return null;
            }
        }

        private void EstimateRouteStops(
            Vehicle vehicle,
            double destinationLat,
            double destinationLng,
            RouteDetails routeDetail)
        {
            double currentLat = vehicle.StartLatitude;
            double currentLng = vehicle.StartLongitude;
            double totalDistance = 0;
            double totalTime = 0;

            // Add passenger stops
            for (int i = 0; i < vehicle.AssignedPassengers.Count; i++)
            {
                var passenger = vehicle.AssignedPassengers[i];
                if (passenger == null)
                {
                    continue;
                }

                double distance = GeoCalculator.CalculateDistance(
                    currentLat, currentLng, passenger.Latitude, passenger.Longitude);
                double time = (distance / 30.0) * 60;

                totalDistance += distance;
                totalTime += time;

                AddPassengerStop(routeDetail, passenger, i, distance, time, totalDistance, totalTime);

                currentLat = passenger.Latitude;
                currentLng = passenger.Longitude;
            }

            // Add final destination
            AddFinalDestinationStop(
                routeDetail, vehicle, currentLat, currentLng,
                destinationLat, destinationLng, totalDistance, totalTime);
        }

        private void AddPassengerStop(
            RouteDetails routeDetail,
            Passenger passenger,
            int index,
            double distance,
            double time,
            double totalDistance,
            double totalTime)
        {
            routeDetail.StopDetails.Add(new StopDetail
            {
                StopNumber = index + 1,
                PassengerId = passenger.Id,
                PassengerName = passenger.Name,
                DistanceFromPrevious = distance,
                TimeFromPrevious = time,
                CumulativeDistance = totalDistance,
                CumulativeTime = totalTime
            });
        }

        private void AddFinalDestinationStop(
            RouteDetails routeDetail,
            Vehicle vehicle,
            double currentLat,
            double currentLng,
            double destinationLat,
            double destinationLng,
            double totalDistance,
            double totalTime)
        {
            double distToDest = GeoCalculator.CalculateDistance(
                currentLat, currentLng, destinationLat, destinationLng);
            double timeToDest = (distToDest / 30.0) * 60;

            totalDistance += distToDest;
            totalTime += timeToDest;

            routeDetail.StopDetails.Add(new StopDetail
            {
                StopNumber = vehicle.AssignedPassengers.Count + 1,
                PassengerId = -1,
                PassengerName = "Destination",
                DistanceFromPrevious = distToDest,
                TimeFromPrevious = timeToDest,
                CumulativeDistance = totalDistance,
                CumulativeTime = totalTime
            });

            routeDetail.TotalDistance = totalDistance;
            routeDetail.TotalTime = totalTime;
        }

        private void HandleRouteEstimationError(Exception ex)
        {
            MessageBox.Show(
                $"Error estimating route details: {ex.Message}",
                "Route Estimation Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                }
                _disposed = true;
            }
        }

        ~DirectionsService()
        {
            Dispose(false);
        }
    }
}
