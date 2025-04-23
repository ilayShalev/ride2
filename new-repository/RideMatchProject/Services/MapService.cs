using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Windows.Forms;
using System.Configuration;
using System.Drawing;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using Newtonsoft.Json;
using RideMatchProject.Models;
using RideMatchProject.Utilities;

namespace RideMatchProject.Services
{
    /// <summary>
    /// Facade class that coordinates all map-related services
    /// </summary>
    public class MapService : IDisposable
    {
        private readonly ApiKeyManager _apiKeyManager;
        private readonly MapInitializer _mapInitializer;
        private readonly DirectionsService _directionsService;
        private readonly GeocodingService _geocodingService;
        private readonly RouteVisualizer _routeVisualizer;
        private bool _disposed = false;

        public MapService(string apiKey = null)
        {
            _apiKeyManager = new ApiKeyManager(apiKey);
            string resolvedApiKey = _apiKeyManager.GetApiKey();

            _mapInitializer = new MapInitializer(resolvedApiKey);
            _directionsService = new DirectionsService(resolvedApiKey);
            _geocodingService = new GeocodingService(resolvedApiKey);
            _routeVisualizer = new RouteVisualizer();
        }

        public bool InitializeGoogleMaps(GMapControl mapControl, double latitude = 32.0853, double longitude = 34.7818)
        {
            return _mapInitializer.InitializeMap(mapControl, latitude, longitude);
        }

        public bool ChangeMapProvider(GMapControl mapControl, int providerType)
        {
            return _mapInitializer.ChangeProvider(mapControl, providerType);
        }

        public async Task<List<PointLatLng>> GetGoogleDirectionsAsync(List<PointLatLng> waypoints)
        {
            return await _directionsService.GetDirectionsAsync(waypoints);
        }

        public async Task<RouteDetails> GetRouteDetailsAsync(Vehicle vehicle, double destinationLat, double destinationLng)
        {
            return await _directionsService.GetRouteDetailsAsync(vehicle, destinationLat, destinationLng);
        }

        public RouteDetails EstimateRouteDetails(Vehicle vehicle, double destinationLat, double destinationLng)
        {
            return _directionsService.EstimateRouteDetails(vehicle, destinationLat, destinationLng);
        }

        public Color GetRouteColor(int index)
        {
            return _routeVisualizer.GetRouteColor(index);
        }

        public async Task<(double Latitude, double Longitude)?> GeocodeAddressAsync(string address)
        {
            return await _geocodingService.GeocodeAddressAsync(address);
        }

        public async Task<string> ReverseGeocodeAsync(double latitude, double longitude)
        {
            return await _geocodingService.ReverseGeocodeAsync(latitude, longitude);
        }

        public async Task<List<string>> GetAddressSuggestionsAsync(string query)
        {
            return await _geocodingService.GetAddressSuggestionsAsync(query);
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
                    _directionsService.Dispose();
                    _geocodingService.Dispose();
                }
                _disposed = true;
            }
        }

        ~MapService()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// Manages API key acquisition and storage
    /// </summary>
    public class ApiKeyManager
    {
        private string _apiKey;

        public ApiKeyManager(string apiKey = null)
        {
            _apiKey = apiKey;
        }

        public string GetApiKey()
        {
            if (!string.IsNullOrEmpty(_apiKey))
            {
                return _apiKey;
            }

            _apiKey = LoadApiKeyFromConfig();

            if (string.IsNullOrEmpty(_apiKey))
            {
                _apiKey = PromptForApiKey();
            }

            return _apiKey;
        }

        private string LoadApiKeyFromConfig()
        {
            return ConfigurationManager. AppSettings["GoogleApiKey"];
        }

        private string PromptForApiKey()
        {
            string enteredKey = string.Empty;

            using (var form = new Form())
            {
                form.Width = 400;
                form.Height = 150;
                form.Text = "Google API Key Required";

                var label = new Label { Left = 20, Top = 20, Text = "Please enter your Google Maps API Key:", Width = 360 };
                var textBox = new TextBox { Left = 20, Top = 50, Width = 360 };
                var button = new Button { Text = "OK", Left = 160, Top = 80, DialogResult = DialogResult.OK };

                form.Controls.Add(label);
                form.Controls.Add(textBox);
                form.Controls.Add(button);
                form.AcceptButton = button;

                if (form.ShowDialog() == DialogResult.OK)
                {
                    enteredKey = textBox.Text;
                    SaveApiKeyToConfig(enteredKey);
                }
            }

            return enteredKey;
        }

        private void SaveApiKeyToConfig(string apiKey)
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings["GoogleApiKey"].Value = apiKey;
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
            catch
            {
                // Silently handle config save errors
            }
        }
    }

    /// <summary>
    /// Handles map initialization and configuration
    /// </summary>
    public class MapInitializer
    {
        private readonly string _apiKey;

        public MapInitializer(string apiKey)
        {
            _apiKey = apiKey;
            InitializeGMapProviders();
        }

        private void InitializeGMapProviders()
        {
            try
            {
                GMaps.Instance.Mode = AccessMode.ServerAndCache;

                GoogleMapProvider.Instance.ApiKey = _apiKey;
                GoogleSatelliteMapProvider.Instance.ApiKey = _apiKey;
                GoogleHybridMapProvider.Instance.ApiKey = _apiKey;
                GoogleTerrainMapProvider.Instance.ApiKey = _apiKey;
            }
            catch (Exception ex)
            {
                HandleMapInitializationError(ex);
            }
        }

        private void HandleMapInitializationError(Exception ex)
        {
            MessageBox.Show(
                $"Error initializing map providers: {ex.Message}",
                "Map Initialization Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }

        public bool InitializeMap(GMapControl mapControl, double latitude, double longitude)
        {
            if (mapControl == null)
            {
                return false;
            }

            try
            {
                ConfigureMapControl(mapControl, latitude, longitude);
                SetupGMapInstance();
                RegisterMapEvents(mapControl);

                mapControl.Refresh();
                return true;
            }
            catch (Exception ex)
            {
                HandleMapError(ex);
                return false;
            }
        }

        private void ConfigureMapControl(GMapControl mapControl, double latitude, double longitude)
        {
            mapControl.MapProvider = GoogleMapProvider.Instance;
            mapControl.Position = new PointLatLng(latitude, longitude);
            mapControl.MinZoom = 2;
            mapControl.MaxZoom = 18;
            mapControl.Zoom = 12;
            mapControl.DragButton = MouseButtons.Left;
            mapControl.CanDragMap = true;
            mapControl.ShowCenter = false;
        }

        private void SetupGMapInstance()
        {
            try
            {
                GMaps.Instance.Mode = AccessMode.ServerAndCache;
                GMaps.Instance.UseRouteCache = true;
                GMaps.Instance.UsePlacemarkCache = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning initializing GMaps: {ex.Message}");
            }
        }

        private void RegisterMapEvents(GMapControl mapControl)
        {
            mapControl.OnMapZoomChanged += () =>
            {
                // Save zoom level or perform other actions when zoom changes
            };
        }

        private void HandleMapError(Exception ex)
        {
            MessageBox.Show(
                $"Error initializing map: {ex.Message}",
                "Map Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }

        public bool ChangeProvider(GMapControl mapControl, int providerType)
        {
            if (mapControl == null)
            {
                return false;
            }

            try
            {
                ApplyMapProvider(mapControl, providerType);
                mapControl.Refresh();
                return true;
            }
            catch (Exception ex)
            {
                HandleProviderChangeError(ex);
                return false;
            }
        }

        private void ApplyMapProvider(GMapControl mapControl, int providerType)
        {
            switch (providerType)
            {
                case 0:
                    mapControl.MapProvider = GoogleMapProvider.Instance;
                    break;
                case 1:
                    mapControl.MapProvider = GoogleSatelliteMapProvider.Instance;
                    break;
                case 2:
                    mapControl.MapProvider = GoogleHybridMapProvider.Instance;
                    break;
                case 3:
                    mapControl.MapProvider = GoogleTerrainMapProvider.Instance;
                    break;
                default:
                    mapControl.MapProvider = GoogleMapProvider.Instance;
                    break;
            }
        }

        private void HandleProviderChangeError(Exception ex)
        {
            MessageBox.Show(
                $"Error changing map provider: {ex.Message}",
                "Map Provider Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }
    }

    /// <summary>
    /// Handles route visualization aspects
    /// </summary>
    public class RouteVisualizer
    {
        private readonly Color[] _routeColors = {
            Color.FromArgb(255, 128, 0),   // Orange
            Color.FromArgb(128, 0, 128),   // Purple
            Color.FromArgb(0, 128, 128),   // Teal
            Color.FromArgb(128, 0, 0),     // Maroon
            Color.FromArgb(0, 128, 0),     // Green
            Color.FromArgb(0, 0, 128),     // Navy
            Color.FromArgb(128, 128, 0),   // Olive
            Color.FromArgb(128, 0, 64)     // Burgundy
        };

        public Color GetRouteColor(int index)
        {
            return _routeColors[index % _routeColors.Length];
        }
    }

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

                string url = BuildDirectionsUrl(waypoints);
                string response = await _httpClient.GetStringAsync(url);

                var points = ProcessDirectionsResponse(response);

                if (points != null)
                {
                    _routeCache[cacheKey] = points;
                }

                return points;
            }
            catch (Exception ex)
            {
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

        public async Task<RouteDetails> GetRouteDetailsAsync(Vehicle vehicle, double destinationLat, double destinationLng)
        {
            if (IsInvalidVehicleData(vehicle))
            {
                return null;
            }

            try
            {
                string url = BuildRouteDetailsUrl(vehicle, destinationLat, destinationLng);
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

        private string BuildRouteDetailsUrl(Vehicle vehicle, double destinationLat, double destinationLng)
        {
            string origin = $"{vehicle.StartLatitude},{vehicle.StartLongitude}";
            string destination = $"{destinationLat},{destinationLng}";

            string waypointsStr = string.Join("|",
                vehicle.AssignedPassengers.Select(p => $"{p.Latitude},{p.Longitude}"));

            string waypointsParam = vehicle.AssignedPassengers.Any()
                ? $"&waypoints={waypointsStr}"
                : "";

            return $"https://maps.googleapis.com/maps/api/directions/json?" +
                $"origin={origin}" +
                $"&destination={destination}" +
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

    /// <summary>
    /// Handles geocoding services
    /// </summary>
    public class GeocodingService : IDisposable
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private bool _disposed = false;

        public GeocodingService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = CreateHttpClient();
        }

        private HttpClient CreateHttpClient()
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            return client;
        }

        public async Task<(double Latitude, double Longitude)?> GeocodeAddressAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return null;
            }

            try
            {
                string url = BuildGeocodeUrl(address);
                string response = await _httpClient.GetStringAsync(url);

                return ProcessGeocodeResponse(response);
            }
            catch (Exception ex)
            {
                HandleGeocodingError(ex);
                return null;
            }
        }

        private string BuildGeocodeUrl(string address)
        {
            string encodedAddress = Uri.EscapeDataString(address);
            return $"https://maps.googleapis.com/maps/api/geocode/json?address={encodedAddress}&key={_apiKey}";
        }

        private (double Latitude, double Longitude)? ProcessGeocodeResponse(string response)
        {
            dynamic data = JsonConvert.DeserializeObject(response);

            if (data.status.ToString() != "OK" || data.results.Count == 0)
            {
                string errorMessage = GetErrorMessage(data);
                throw new Exception($"Geocoding error: {errorMessage}");
            }

            double lat = Convert.ToDouble(data.results[0].geometry.location.lat);
            double lng = Convert.ToDouble(data.results[0].geometry.location.lng);

            return (lat, lng);
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

        private void HandleGeocodingError(Exception ex)
        {
            MessageBox.Show(
                $"Error geocoding address: {ex.Message}",
                "Geocoding Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }

        public async Task<string> ReverseGeocodeAsync(double latitude, double longitude)
        {
            try
            {
                string url = BuildReverseGeocodeUrl(latitude, longitude);
                string response = await _httpClient.GetStringAsync(url);

                return ProcessReverseGeocodeResponse(response);
            }
            catch (Exception ex)
            {
                HandleReverseGeocodingError(ex);
                return null;
            }
        }

        private string BuildReverseGeocodeUrl(double latitude, double longitude)
        {
            return $"https://maps.googleapis.com/maps/api/geocode/json?latlng={latitude},{longitude}&key={_apiKey}";
        }

        private string ProcessReverseGeocodeResponse(string response)
        {
            dynamic data = JsonConvert.DeserializeObject(response);

            if (data.status.ToString() != "OK" || data.results.Count == 0)
            {
                string errorMessage = GetErrorMessage(data);
                throw new Exception($"Reverse geocoding error: {errorMessage}");
            }

            return data.results[0].formatted_address.ToString();
        }

        private void HandleReverseGeocodingError(Exception ex)
        {
            MessageBox.Show(
                $"Error reverse geocoding: {ex.Message}",
                "Reverse Geocoding Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }

        public async Task<List<string>> GetAddressSuggestionsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<string>();
            }

            try
            {
                string url = BuildAutocompleteUrl(query);
                string response = await _httpClient.GetStringAsync(url);

                return ProcessAutocompleteResponse(response);
            }
            catch (Exception ex)
            {
                HandleSuggestionError(ex);
                return new List<string>();
            }
        }

        private string BuildAutocompleteUrl(string query)
        {
            string encodedQuery = Uri.EscapeDataString(query);
            return $"https://maps.googleapis.com/maps/api/place/autocomplete/json?input={encodedQuery}&key={_apiKey}";
        }

        private List<string> ProcessAutocompleteResponse(string response)
        {
            dynamic data = JsonConvert.DeserializeObject(response);
            var suggestions = new List<string>();

            if (data.status.ToString() != "OK")
            {
                string errorMessage = GetErrorMessage(data);
                throw new Exception($"Autocomplete error: {errorMessage}");
            }

            foreach (var prediction in data.predictions)
            {
                suggestions.Add(prediction.description.ToString());
            }

            return suggestions;
        }

        private void HandleSuggestionError(Exception ex)
        {
            Console.WriteLine($"Error getting address suggestions: {ex.Message}");
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

        ~GeocodingService()
        {
            Dispose(false);
        }
    }
}