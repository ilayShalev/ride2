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
using RideMatchProject.Services.MapServiceClasses;

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

   
 

 

 

}