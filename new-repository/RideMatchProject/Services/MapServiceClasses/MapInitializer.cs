using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.Services.MapServiceClasses
{
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
}
