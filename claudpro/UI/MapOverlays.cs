using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using claudpro.Models;

namespace claudpro.UI
{
    /// <summary>
    /// Handles creation of map overlays and markers
    /// </summary>
    public static class MapOverlays
    {
        /// <summary>
        /// Creates a passenger marker
        /// </summary>
        public static GMarkerGoogle CreatePassengerMarker(Passenger passenger)
        {
            return new GMarkerGoogle(new PointLatLng(passenger.Latitude, passenger.Longitude), GMarkerGoogleType.blue)
            {
                ToolTipText = $"Passenger {passenger.Name} (ID: {passenger.Id})",
                ToolTipMode = MarkerTooltipMode.OnMouseOver
            };
        }

        /// <summary>
        /// Creates a vehicle marker
        /// </summary>
        public static GMarkerGoogle CreateVehicleMarker(Vehicle vehicle)
        {
            return new GMarkerGoogle(new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude), GMarkerGoogleType.green)
            {
                ToolTipText = $"Vehicle {vehicle.Id} (Capacity: {vehicle.Capacity})",
                ToolTipMode = MarkerTooltipMode.OnMouseOver
            };
        }

        /// <summary>
        /// Creates a destination marker
        /// </summary>
        public static GMarkerGoogle CreateDestinationMarker(double latitude, double longitude)
        {
            return new GMarkerGoogle(new PointLatLng(latitude, longitude), GMarkerGoogleType.red)
            {
                ToolTipText = "Destination",
                ToolTipMode = MarkerTooltipMode.OnMouseOver
            };
        }

        /// <summary>
        /// Creates a route between points with the specified color
        /// </summary>
        public static GMapRoute CreateRoute(List<PointLatLng> points, string name, Color color, int width = 3)
        {
            return new GMapRoute(points, name)
            {
                Stroke = new Pen(color, width)
            };
        }

        /// <summary>
        /// Creates a standard set of route colors
        /// </summary>
        public static Color[] GetRouteColors()
        {
            return new Color[]
            {
                Color.FromArgb(255, 128, 0),   // Orange
                Color.FromArgb(128, 0, 128),   // Purple
                Color.FromArgb(0, 128, 128),   // Teal
                Color.FromArgb(128, 0, 0),     // Maroon
                Color.FromArgb(0, 128, 0),     // Green
                Color.FromArgb(0, 0, 128),     // Navy
                Color.FromArgb(128, 128, 0),   // Olive
                Color.FromArgb(128, 0, 64)     // Burgundy
            };
        }
    }
}