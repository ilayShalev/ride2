using GMap.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.PassengerClasses
{
    /// <summary>
    /// Controller for managing the logic of the passenger form in a ride-matching application.
    /// Coordinates interactions between the data layer, map visualization, and UI manager to handle passenger data, map interactions, and user inputs.
    /// </summary>
    public class PassengerController
    {
        /// <summary>
        /// The data access layer for retrieving and updating passenger-related data.
        /// </summary>
        private readonly PassengerDataAccessLayer _dataLayer;

        /// <summary>
        /// The map visualizer for rendering passenger locations and handling map interactions.
        /// </summary>
        private readonly MapVisualizer _mapVisualizer;

        /// <summary>
        /// The UI manager for updating the passenger form's user interface.
        /// </summary>
        private readonly PassengerUIManager _uiManager;

        /// <summary>
        /// Indicates whether the controller is in location selection mode, where map clicks update the passenger's location.
        /// </summary>
        private bool _isSettingLocation;

        /// <summary>
        /// Initializes a new instance of the <see cref="PassengerController"/> class.
        /// </summary>
        /// <param name="dataLayer">The <see cref="PassengerDataAccessLayer"/> for accessing and updating passenger data.</param>
        /// <param name="mapVisualizer">The <see cref="MapVisualizer"/> for map rendering and location services.</param>
        /// <param name="uiManager">The <see cref="PassengerUIManager"/> for managing the passenger form's UI.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="dataLayer"/>, <paramref name="mapVisualizer"/>, or <paramref name="uiManager"/> is null.</exception>
        public PassengerController(
            PassengerDataAccessLayer dataLayer,
            MapVisualizer mapVisualizer,
            PassengerUIManager uiManager)
        {
            _dataLayer = dataLayer ?? throw new ArgumentNullException(nameof(dataLayer), "PassengerDataAccessLayer cannot be null.");
            _mapVisualizer = mapVisualizer ?? throw new ArgumentNullException(nameof(mapVisualizer), "MapVisualizer cannot be null.");
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager), "PassengerUIManager cannot be null.");
            _isSettingLocation = false;

            SubscribeToEvents();
        }

        /// <summary>
        /// Subscribes to events from the UI manager and map visualizer to handle user interactions.
        /// </summary>
        /// <remarks>
        /// Subscribes to events for availability changes, refresh requests, location selection requests, address searches, and map clicks.
        /// </remarks>
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

        /// <summary>
        /// Initializes the passenger form asynchronously, setting up the map and loading initial data.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// Displays a loading message, initializes the map via <see cref="MapVisualizer"/>, and refreshes passenger data.
        /// </remarks>
        public async Task InitializeAsync()
        {
            _uiManager.ShowLoadingMessage("Initializing...");
            await _mapVisualizer.InitializeMapAsync();
            await RefreshDataAsync();
        }

        /// <summary>
        /// Refreshes passenger data and updates the UI and map accordingly.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// Loads passenger data from the data layer, updates the UI with passenger details, availability, and pickup time,
        /// and displays the passenger on the map. Shows a "no profile" message if no passenger data is available.
        /// </remarks>
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

        /// <summary>
        /// Enables location selection mode, allowing the user to click on the map to set the passenger's location.
        /// </summary>
        /// <remarks>
        /// Sets the <see cref="_isSettingLocation"/> flag, shows location selection instructions in the UI,
        /// and enables map click handling via <see cref="MapVisualizer"/>.
        /// </remarks>
        private void EnableLocationSelection()
        {
            _isSettingLocation = true;
            _uiManager.ShowLocationSelectionInstructions(true);
            _mapVisualizer.EnableLocationSelection(true);
        }

        /// <summary>
        /// Handles map click events, updating the passenger's location if in location selection mode.
        /// </summary>
        /// <param name="point">The <see cref="PointLatLng"/> representing the clicked location on the map.</param>
        /// <remarks>
        /// If not in location selection mode, the method does nothing. Otherwise, it disables location selection,
        /// hides instructions, and updates the passenger's location with the clicked coordinates.
        /// </remarks>
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

        /// <summary>
        /// Searches for an address and updates the passenger's location with the geocoded coordinates.
        /// </summary>
        /// <param name="address">The address to geocode and set as the passenger's location.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// Disables search controls during processing, geocodes the address, and updates the passenger's location if successful.
        /// Shows an error message if the address cannot be geocoded.
        /// </remarks>
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

        /// <summary>
        /// Updates the passenger's location with the specified coordinates and reverse-geocoded address.
        /// </summary>
        /// <param name="latitude">The latitude of the new location.</param>
        /// <param name="longitude">The longitude of the new location.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// Shows a busy state, reverse-geocodes the coordinates to get an address, updates the data layer,
        /// displays a confirmation message, and refreshes the UI and map.
        /// </remarks>
        private async Task UpdatePassengerLocationAsync(double latitude, double longitude)
        {
            _uiManager.ShowBusyState(true);

            string address = await _mapVisualizer.ReverseGeocodeAsync(latitude, longitude);
            await _dataLayer.UpdatePassengerLocationAsync(latitude, longitude, address);

            _uiManager.ShowLocationUpdatedMessage(address);
            await RefreshDataAsync();

            _uiManager.ShowBusyState(false);
        }

        /// <summary>
        /// Updates the passenger's availability status and handles related UI updates.
        /// </summary>
        /// <param name="isAvailable">The new availability status (<c>true</c> for available, <c>false</c> for unavailable).</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// Updates the data layer with the new availability status. If successful and the passenger is available without an assigned vehicle,
        /// shows a ride request message. If the update fails, shows an error message and reverts the UI to the previous state.
        /// Refreshes the UI and map afterward.
        /// </remarks>
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