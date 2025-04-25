using Newtonsoft.Json;
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
