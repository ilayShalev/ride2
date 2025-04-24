using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using RideMatchProject.Models;
using RideMatchProject.Services;
using RideMatchProject.Utilities;

namespace RideMatchProject.DriverClasses
{
    /// <summary>
    /// Manages map-related operations for the driver interface without the black center point
    /// </summary>
    public class DriverMapManager
    {
        private readonly MapService _mapService;
        private readonly DatabaseService _dbService;
        private GMapControl _mapControl;
        private ThreadSafeMapManager _threadSafeMapManager;
        private MapVisualization _mapVisualization;

        public DriverMapManager(MapService mapService, DatabaseService dbService)
        {
            _mapService = mapService ?? throw new ArgumentNullException(nameof(mapService));
            _dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
        }

        public void InitializeMap(GMapControl mapControl, double latitude, double longitude)
        {
            _mapControl = mapControl ?? throw new ArgumentNullException(nameof(mapControl));

            // Disable the default center marker (black dot) directly
            _mapControl.ShowCenter = false;

            _threadSafeMapManager = new ThreadSafeMapManager(_mapControl);
            _mapVisualization = new MapVisualization(_mapService, _mapControl);

            // Initialize map with custom settings
            _mapService.InitializeGoogleMaps(_mapControl, latitude, longitude);

            // Ensure map settings are properly configured for no center point
            _threadSafeMapManager.DisableCenterMarker();

            // Make sure required features are enabled
            _threadSafeMapManager.ExecuteOnUIThread(() => {
                _mapControl.MarkersEnabled = true;
                _mapControl.PolygonsEnabled = true;
                _mapControl.RoutesEnabled = true;
            });
        }

        public async Task DisplayRouteOnMapAsync(Vehicle vehicle, List<Passenger> passengers)
        {
            if (_mapControl == null || vehicle == null)
            {
                return;
            }

            try
            {
                // Make sure center marker is disabled
                _threadSafeMapManager.DisableCenterMarker();

                // Get destination tuple from database
                var destinationTuple = await _dbService.GetDestinationAsync();

                // Extract latitude and longitude from the tuple
                double destLatitude = destinationTuple.Latitude;
                double destLongitude = destinationTuple.Longitude;

                // Use the enhanced visualization without black center point
                await _mapVisualization.DisplayRouteWithPointsAsync(vehicle, passengers, destLatitude, destLongitude);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error displaying route: {ex.Message}",
                    "Map Display Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void DisplayRouteOnMap(Vehicle vehicle, List<Passenger> passengers)
        {
            // Synchronous wrapper for backward compatibility
            Task.Run(async () =>
            {
                try
                {
                    await DisplayRouteOnMapAsync(vehicle, passengers);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in DisplayRouteOnMap: {ex.Message}");
                }
            });
        }
    }
}