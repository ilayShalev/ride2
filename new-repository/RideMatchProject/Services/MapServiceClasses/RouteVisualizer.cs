using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.MapServiceClasses
{
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
}
