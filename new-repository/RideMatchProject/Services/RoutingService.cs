using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using RideMatchProject.Models;
using RideMatchProject.UI;
using RideMatchProject.Utilities;
using System.Windows.Forms;
using RideMatchProject.Services.RoutingServiceClasses;

namespace RideMatchProject.Services
{
    /// <summary>
    /// Main facade service for routing operations
    /// </summary>
    public class RoutingService
    {
        private readonly MapService _mapService;
        private readonly DestinationInfo _destination;
        private readonly MapDisplayManager _displayManager;
        private readonly RoutingPathCalculator _routeCalculator;
        private readonly SolutionValidator _validator;

        public Dictionary<int, RouteDetails> VehicleRouteDetails { get; private set; }

        public RoutingService(MapService mapService, double destinationLat, double destinationLng)
        {
            _mapService = mapService;
            _destination = new DestinationInfo(destinationLat, destinationLng);
            _displayManager = new MapDisplayManager(_mapService);
            _routeCalculator = new RoutingPathCalculator(_mapService, _destination);
            _validator = new SolutionValidator();
            VehicleRouteDetails = new Dictionary<int, RouteDetails>();
        }

        public void DisplayDataOnMap(GMapControl mapControl, List<Passenger> passengers, List<Vehicle> vehicles)
        {
            _displayManager.DisplayDataOnMap(mapControl, passengers, vehicles, _destination);
        }

        public void DisplaySolutionOnMap(GMapControl mapControl, Solution solution)
        {
            _displayManager.DisplaySolutionOnMap(mapControl, solution, _destination);
        }

        public async Task GetGoogleRoutesAsync(GMapControl mapControl, Solution solution)
        {
            VehicleRouteDetails = await _routeCalculator.GetGoogleRoutesAsync(mapControl, solution);
        }

        public void CalculateEstimatedRouteDetails(Solution solution)
        {
            VehicleRouteDetails = _routeCalculator.CalculateEstimatedRouteDetails(solution);
        }

        public string ValidateSolution(Solution solution, List<Passenger> allPassengers)
        {
            return _validator.ValidateSolution(solution, allPassengers);
        }
    }
}