using GMap.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.PassengerClasses
{
    /// <summary>
    /// Controller for passenger form logic
    /// </summary>
    public class PassengerController
    {
        private readonly PassengerDataAccessLayer _dataLayer;
        private readonly MapVisualizer _mapVisualizer;
        private readonly PassengerUIManager _uiManager;
        private bool _isSettingLocation;

        public PassengerController(
            PassengerDataAccessLayer dataLayer,
            MapVisualizer mapVisualizer,
            PassengerUIManager uiManager)
        {
            _dataLayer = dataLayer;
            _mapVisualizer = mapVisualizer;
            _uiManager = uiManager;
            _isSettingLocation = false;

            SubscribeToEvents();
        }

        private void SubscribeToEvents()
        {
            _uiManager.AvailabilityChanged += async (sender, isAvailable) =>
                await UpdateAvailabilityAsync(isAvailable);

            _uiManager.RefreshRequested += async (sender, args) =>
                await RefreshDataAsync();

            _uiManager.SetLocationRequested += (sender, args) =>
                EnableLocationSelection();

            _uiManager.AddressSearchRequested += async (sender, address) =>
                await SearchAddressAsync(address);

            _mapVisualizer.MapClicked += (sender, point) =>
                HandleMapClick(point);
        }

        public async Task InitializeAsync()
        {
            _uiManager.ShowLoadingMessage("Initializing...");
            await _mapVisualizer.InitializeMapAsync();
            await RefreshDataAsync();
        }

        private async Task RefreshDataAsync()
        {
            _uiManager.ShowLoadingMessage("Loading passenger data...");
            await _dataLayer.LoadPassengerDataAsync();

            var passenger = _dataLayer.CurrentPassenger;
            var vehicle = _dataLayer.AssignedVehicle;

            if (passenger != null)
            {
                _uiManager.UpdateAvailabilityControl(passenger.IsAvailableTomorrow);
                _uiManager.DisplayPassengerDetails(passenger, vehicle, _dataLayer.PickupTime);
                _mapVisualizer.DisplayPassenger(passenger, vehicle);
            }
            else
            {
                _uiManager.ShowNoProfileMessage();
            }
        }

        private void EnableLocationSelection()
        {
            _isSettingLocation = true;
            _uiManager.ShowLocationSelectionInstructions(true);
            _mapVisualizer.EnableLocationSelection(true);
        }

        private void HandleMapClick(PointLatLng point)
        {
            if (!_isSettingLocation)
            {
                return;
            }

            _isSettingLocation = false;
            _uiManager.ShowLocationSelectionInstructions(false);
            _mapVisualizer.EnableLocationSelection(false);

            UpdatePassengerLocationAsync(point.Lat, point.Lng);
        }

        private async Task SearchAddressAsync(string address)
        {
            _uiManager.SetSearchControlsEnabled(false);

            var coordinates = await _mapVisualizer.GeocodeAddressAsync(address);
            if (coordinates.HasValue)
            {
                await UpdatePassengerLocationAsync(coordinates.Value.Latitude, coordinates.Value.Longitude);
            }
            else
            {
                _uiManager.ShowErrorMessage("Address not found. Please try again.");
            }

            _uiManager.SetSearchControlsEnabled(true);
        }

        private async Task UpdatePassengerLocationAsync(double latitude, double longitude)
        {
            _uiManager.ShowBusyState(true);

            string address = await _mapVisualizer.ReverseGeocodeAsync(latitude, longitude);
            await _dataLayer.UpdatePassengerLocationAsync(latitude, longitude, address);

            _uiManager.ShowLocationUpdatedMessage(address);
            await RefreshDataAsync();

            _uiManager.ShowBusyState(false);
        }

        private async Task UpdateAvailabilityAsync(bool isAvailable)
        {
            var passenger = _dataLayer.CurrentPassenger;
            if (passenger == null)
            {
                return;
            }

            bool success = await _dataLayer.UpdatePassengerAvailabilityAsync(isAvailable);

            if (success)
            {
                if (isAvailable && _dataLayer.AssignedVehicle == null)
                {
                    _uiManager.ShowRideRequestMessage();
                }
            }
            else
            {
                _uiManager.ShowErrorMessage("Failed to update availability");
                _uiManager.UpdateAvailabilityControl(passenger.IsAvailableTomorrow);
            }

            await RefreshDataAsync();
        }
    }
}
