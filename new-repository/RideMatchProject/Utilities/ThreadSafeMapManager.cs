using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using RideMatchProject.Models;
using RideMatchProject.UI;

namespace RideMatchProject.Utilities
{
    /// <summary>
    /// Thread-safe manager for map operations that ensures all UI updates happen on the UI thread
    /// </summary>
    public class ThreadSafeMapManager
    {
        private readonly GMapControl _mapControl;

        public ThreadSafeMapManager(GMapControl mapControl)
        {
            _mapControl = mapControl ?? throw new ArgumentNullException(nameof(mapControl));
        }

        /// <summary>
        /// Safely executes an action on the UI thread
        /// </summary>
        public void ExecuteOnUIThread(Action action)
        {
            if (_mapControl.InvokeRequired)
            {
                try
                {
                    _mapControl.Invoke(action);
                }
                catch (ObjectDisposedException)
                {
                    // Control may have been disposed if form is closing
                }
                catch (InvalidOperationException ex)
                {
                    // Handle case where handle isn't created yet
                    Console.WriteLine($"UI operation failed: {ex.Message}");
                }
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Safely disables the center marker (black point)
        /// </summary>
        public void DisableCenterMarker()
        {
            ExecuteOnUIThread(() => {
                _mapControl.ShowCenter = false;
            });
        }

        /// <summary>
        /// Safely adds a route to the map
        /// </summary>
        public void AddRoute(GMapOverlay overlay, List<PointLatLng> points, string name, Color color, int width = 3)
        {
            ExecuteOnUIThread(() => {
                try
                {
                    var route = MapOverlays.CreateRoute(points, name, color, width);
                    overlay.Routes.Add(route);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error adding route: {ex.Message}",
                        "Route Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            });
        }

        /// <summary>
        /// Safely clears all overlays from the map
        /// </summary>
        public void ClearOverlays()
        {
            ExecuteOnUIThread(() => {
                try
                {
                    _mapControl.Overlays.Clear();

                    // Make sure center marker stays disabled
                    _mapControl.ShowCenter = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error clearing overlays: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Safely adds an overlay to the map
        /// </summary>
        public void AddOverlay(GMapOverlay overlay)
        {
            ExecuteOnUIThread(() => {
                try
                {
                    _mapControl.Overlays.Add(overlay);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error adding overlay: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Safely sets the map position
        /// </summary>
        public void SetPosition(double latitude, double longitude, int zoom = 12)
        {
            ExecuteOnUIThread(() => {
                try
                {
                    _mapControl.Position = new PointLatLng(latitude, longitude);
                    _mapControl.Zoom = zoom;

                    // Make sure center marker stays disabled
                    _mapControl.ShowCenter = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error setting map position: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Safely refreshes the map
        /// </summary>
        public void RefreshMap()
        {
            ExecuteOnUIThread(() => {
                try
                {
                    // Force refresh by setting zoom to current value
                    _mapControl.Zoom = _mapControl.Zoom;
                    _mapControl.Refresh();

                    // Make sure center marker stays disabled
                    _mapControl.ShowCenter = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error refreshing map: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Gets or creates an overlay with the specified ID
        /// </summary>
        public GMapOverlay GetOrCreateOverlay(string overlayId)
        {
            // This method needs to be called from the UI thread
            if (_mapControl.InvokeRequired)
            {
                return (GMapOverlay)_mapControl.Invoke(new Func<string, GMapOverlay>(GetOrCreateOverlay), overlayId);
            }

            var overlay = _mapControl.Overlays.FirstOrDefault(o => o.Id == overlayId);

            if (overlay == null)
            {
                overlay = new GMapOverlay(overlayId);
                _mapControl.Overlays.Add(overlay);
            }

            return overlay;
        }
    }
}