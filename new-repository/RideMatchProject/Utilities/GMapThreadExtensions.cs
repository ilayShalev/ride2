using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using RideMatchProject.Models;
using RideMatchProject.UI;

namespace RideMatchProject.Utilities
{
    /// <summary>
    /// Thread-safe extensions for GMap operations
    /// </summary>
    public static class GMapThreadExtensions
    {
        /// <summary>
        /// Safely sets the map position on the UI thread
        /// </summary>
        public static void SetMapPositionSafe(this GMapControl mapControl, double latitude, double longitude, int zoom = 12)
        {
            ThreadUtils.ExecuteOnUIThread(mapControl, () => {
                mapControl.Position = new PointLatLng(latitude, longitude);
                mapControl.Zoom = zoom;
                mapControl.ShowCenter = false; // Commonly needed in this application
            });
        }

        /// <summary>
        /// Safely clears all overlays on the UI thread
        /// </summary>
        public static void ClearOverlaysSafe(this GMapControl mapControl)
        {
            ThreadUtils.ExecuteOnUIThread(mapControl, () => {
                mapControl.Overlays.Clear();
                mapControl.ShowCenter = false;
            });
        }

        /// <summary>
        /// Safely adds an overlay on the UI thread
        /// </summary>
        public static void AddOverlaySafe(this GMapControl mapControl, GMapOverlay overlay)
        {
            ThreadUtils.ExecuteOnUIThread(mapControl, () => {
                mapControl.Overlays.Add(overlay);
            });
        }

        /// <summary>
        /// Safely removes an overlay on the UI thread
        /// </summary>
        public static void RemoveOverlaySafe(this GMapControl mapControl, GMapOverlay overlay)
        {
            ThreadUtils.ExecuteOnUIThread(mapControl, () => {
                mapControl.Overlays.Remove(overlay);
            });
        }

        /// <summary>
        /// Safely refreshes the map on the UI thread
        /// </summary>
        public static void RefreshMapSafe(this GMapControl mapControl)
        {
            ThreadUtils.ExecuteOnUIThread(mapControl, () => {
                // Setting the zoom to its current value forces a refresh
                mapControl.Zoom = mapControl.Zoom;
                mapControl.Refresh();
            });
        }

        /// <summary>
        /// Safely adds a marker to an overlay on the UI thread
        /// </summary>
        public static void AddMarkerSafe(this GMapOverlay overlay, GMapMarker marker, GMapControl mapControl)
        {
            ThreadUtils.ExecuteOnUIThread(mapControl, () => {
                overlay.Markers.Add(marker);
            });
        }

        /// <summary>
        /// Safely creates and adds passenger marker on the UI thread
        /// </summary>
        public static void AddPassengerMarkerSafe(this GMapOverlay overlay, Passenger passenger, GMapControl mapControl)
        {
            ThreadUtils.ExecuteOnUIThread(mapControl, () => {
                var marker = MapOverlays.CreatePassengerMarker(passenger);
                overlay.Markers.Add(marker);
            });
        }

        /// <summary>
        /// Safely creates and adds vehicle marker on the UI thread
        /// </summary>
        public static void AddVehicleMarkerSafe(this GMapOverlay overlay, Vehicle vehicle, GMapControl mapControl)
        {
            ThreadUtils.ExecuteOnUIThread(mapControl, () => {
                var marker = MapOverlays.CreateVehicleMarker(vehicle);
                overlay.Markers.Add(marker);
            });
        }

        /// <summary>
        /// Safely creates and adds destination marker on the UI thread
        /// </summary>
        public static void AddDestinationMarkerSafe(this GMapOverlay overlay, double latitude, double longitude, GMapControl mapControl)
        {
            ThreadUtils.ExecuteOnUIThread(mapControl, () => {
                var marker = MapOverlays.CreateDestinationMarker(latitude, longitude);
                overlay.Markers.Add(marker);
            });
        }

        /// <summary>
        /// Safely adds a route to an overlay on the UI thread
        /// </summary>
        public static void AddRouteSafe(this GMapOverlay overlay, List<PointLatLng> points, string name, Color color, int width, GMapControl mapControl)
        {
            ThreadUtils.ExecuteOnUIThread(mapControl, () => {
                var route = MapOverlays.CreateRoute(points, name, color, width);
                overlay.Routes.Add(route);
            });
        }

        /// <summary>
        /// Gets or creates an overlay safely on the UI thread
        /// </summary>
        public static GMapOverlay GetOrCreateOverlaySafe(this GMapControl mapControl, string overlayId)
        {
            return ThreadUtils.ExecuteOnUIThread(mapControl, () => {
                var existing = mapControl.Overlays.FirstOrDefault(o => o.Id == overlayId);
                if (existing != null)
                {
                    return existing;
                }

                var newOverlay = new GMapOverlay(overlayId);
                mapControl.Overlays.Add(newOverlay);
                return newOverlay;
            });
        }
    }
}