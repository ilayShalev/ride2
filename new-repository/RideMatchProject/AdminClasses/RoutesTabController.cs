using GMap.NET;
using GMap.NET.WindowsForms;
using RideMatchProject.Models;
using RideMatchProject.Services;
using RideMatchProject.UI;
using RideMatchProject.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using RideMatchProject.Services.DatabaseServiceClasses;

namespace RideMatchProject.AdminClasses
{
    /// <summary>
    /// Controller for the Routes tab in the admin interface, responsible for managing route visualization,
    /// loading routes for a selected date, and displaying route details on a map and list view.
    /// </summary>
    public class RoutesTabController : TabControllerBase
    {
        private DateTimePicker _dateSelector;
        private Button _loadButton;
        private Button _getGoogleRoutesButton;
        private GMapControl _mapControl;
        private ListView _routesListView;
        private RichTextBox _routeDetailsTextBox;
        private Solution _currentSolution;
        private RoutingService _routingService;

        /// <summary>
        /// Initializes a new instance of the <see cref="RoutesTabController"/> class.
        /// </summary>
        /// <param name="dbService">The database service used to retrieve route and solution data.</param>
        /// <param name="mapService">The map service used to initialize and manage the GMap control.</param>
        /// <param name="dataManager">The data manager responsible for handling admin-related data operations.</param>
        public RoutesTabController(
            DatabaseService dbService,
            MapService mapService,
            AdminDataManager dataManager)
            : base(dbService, mapService, dataManager)
        {
        }

        /// <summary>
        /// Initializes the Routes tab by creating and adding UI controls such as date selection and routes panel.
        /// </summary>
        /// <param name="tabPage">The <see cref="TabPage"/> to which the controls will be added.</param>
        public override void InitializeTab(TabPage tabPage)
        {
            CreateDateSelectionControls(tabPage);
            CreateRoutesPanel(tabPage);
        }

        /// <summary>
        /// Creates and configures date selection controls (label, date picker, and load button) for the Routes tab.
        /// </summary>
        /// <param name="tabPage">The <see cref="TabPage"/> where the controls will be placed.</param>
        private void CreateDateSelectionControls(TabPage tabPage)
        {
            // Date selector label
            var dateLabel = AdminUIFactory.CreateLabel(
                "Select Date:",
                new Point(10, 10),
                new Size(120, 25),
                ContentAlignment.MiddleRight
            );

            // Date selector
            _dateSelector = new DateTimePicker
            {
                Location = new Point(140, 10),
                Size = new Size(200, 25),
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today
            };

            // Load button
            _loadButton = AdminUIFactory.CreateButton(
                "Load Routes",
                new Point(350, 10),
                new Size(120, 30),
                LoadButtonClick
            );

            // Add controls to tab
            tabPage.Controls.Add(dateLabel);
            tabPage.Controls.Add(_dateSelector);
            tabPage.Controls.Add(_loadButton);
        }

        /// <summary>
        /// Creates and configures the routes panel, including the map control, routes list view, and route details text box.
        /// </summary>
        /// <param name="tabPage">The <see cref="TabPage"/> where the panel will be placed.</param>
        private void CreateRoutesPanel(TabPage tabPage)
        {
            // Main panel
            var routesPanel = AdminUIFactory.CreatePanel(
                new Point(10, 50),
                new Size(1140, 660),
                BorderStyle.FixedSingle
            );

            // Map control
            _mapControl = new GMapControl
            {
                Location = new Point(10, 10),
                Size = new Size(700, 640),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 12,
                DragButton = MouseButtons.Left
            };
            MapService.InitializeGoogleMaps(_mapControl);

            // Routes list
            _routesListView = new ListView
            {
                Location = new Point(720, 10),
                Size = new Size(410, 320),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            _routesListView.Columns.Add("Vehicle", 80);
            _routesListView.Columns.Add("Driver", 120);
            _routesListView.Columns.Add("Passengers", 80);
            _routesListView.Columns.Add("Distance", 80);
            _routesListView.SelectedIndexChanged += RoutesListView_SelectedIndexChanged;

            // Route details
            _routeDetailsTextBox = new RichTextBox
            {
                Location = new Point(720, 340),
                Size = new Size(410, 310),
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None
            };

            // Add controls to panel
            routesPanel.Controls.Add(_mapControl);
            routesPanel.Controls.Add(_routesListView);
            routesPanel.Controls.Add(_routeDetailsTextBox);

            // Add panel to tab
            tabPage.Controls.Add(routesPanel);
        }

        /// <summary>
        /// Handles the click event of the Load Routes button, loading routes for the selected date.
        /// </summary>
        /// <param name="sender">The source of the event (the button).</param>
        /// <param name="e">The event arguments.</param>
        private async void LoadButtonClick(object sender, EventArgs e)
        {
            _loadButton.Enabled = false;
            _loadButton.Text = "Loading...";

            try
            {
                await LoadRoutesForDateAsync(_dateSelector.Value);

                // Don't automatically get Google routes - just display what's in the database
                if (_currentSolution != null && _currentSolution.Vehicles.Count > 0)
                {
                    try
                    {
                        // Initialize routing service if needed, but don't call Google API
                        if (_routingService == null)
                        {
                            var destination = await DbService.GetDestinationAsync();
                            _routingService = new RoutingService(
                                MapService,
                                destination.Latitude,
                                destination.Longitude
                            );
                        }

                        // Display routes on map using data from database
                        _routingService.DisplaySolutionOnMap(_mapControl, _currentSolution);
                        DisplayPassengersOnMap(_currentSolution);

                        // Display routes in list
                        DisplayRoutesInListView(_currentSolution);
                    }
                    catch (Exception ex)
                    {
                        MessageDisplayer.ShowWarning(
                            $"Routes loaded, but display failed: {ex.Message}",
                            "Display Error"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error loading routes: {ex.Message}",
                    "Load Error"
                );
            }
            finally
            {
                _loadButton.Enabled = true;
                _loadButton.Text = "Load Routes";
            }
        }

        /// <summary>
        /// Handles the selection change event in the routes list view, displaying details for the selected route.
        /// </summary>
        /// <param name="sender">The source of the event (the list view).</param>
        /// <param name="e">The event arguments.</param>
        private void RoutesListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_routesListView.SelectedItems.Count > 0)
            {
                int vehicleId = int.Parse(_routesListView.SelectedItems[0].SubItems[0].Text);

                // Check if we have Google route details for this vehicle
                if (_routingService != null && _routingService.VehicleRouteDetails.ContainsKey(vehicleId))
                {
                    // Display detailed route from Google data
                    var vehicle = _currentSolution.Vehicles.FirstOrDefault(v => v.Id == vehicleId);
                    if (vehicle != null)
                    {
                        var routeDetail = _routingService.VehicleRouteDetails[vehicleId];
                        DisplayDetailedRouteForVehicle(vehicle, routeDetail);
                    }
                }
                else
                {
                    // Fall back to simplified display if Google details aren't available
                    DisplayRouteDetails(vehicleId);
                }
            }
        }

        /// <summary>
        /// Displays detailed route information for a specific vehicle in the route details text box, including Google routing data.
        /// </summary>
        /// <param name="vehicle">The <see cref="Vehicle"/> for which to display details.</param>
        /// <param name="routeDetail">The <see cref="RouteDetails"/> containing Google routing information.</param>
        private void DisplayDetailedRouteForVehicle(Vehicle vehicle, RouteDetails routeDetail)
        {
            _routeDetailsTextBox.Clear();

            // Header with vehicle info
            _routeDetailsTextBox.SelectionFont = new Font(_routeDetailsTextBox.Font, FontStyle.Bold);
            _routeDetailsTextBox.AppendText($"Route Details for Vehicle {vehicle.Id}\n\n");
            _routeDetailsTextBox.SelectionFont = _routeDetailsTextBox.Font;

            _routeDetailsTextBox.AppendText($"Driver: {vehicle.DriverName ?? $"Driver {vehicle.Id}"}\n");
            _routeDetailsTextBox.AppendText($"Vehicle Capacity: {vehicle.Capacity}\n");
            _routeDetailsTextBox.AppendText($"Total Distance: {routeDetail.TotalDistance:F2} km\n");
            _routeDetailsTextBox.AppendText($"Total Time: {TimeFormatter.FormatMinutesWithUnits(routeDetail.TotalTime)}\n");

            if (!string.IsNullOrEmpty(vehicle.DepartureTime))
            {
                _routeDetailsTextBox.SelectionFont = new Font(_routeDetailsTextBox.Font, FontStyle.Bold);
                _routeDetailsTextBox.AppendText($"Departure Time: {vehicle.DepartureTime}\n");
                _routeDetailsTextBox.SelectionFont = _routeDetailsTextBox.Font;
            }

            _routeDetailsTextBox.AppendText("\n");
            _routeDetailsTextBox.SelectionFont = new Font(_routeDetailsTextBox.Font, FontStyle.Bold);
            _routeDetailsTextBox.AppendText("Pickup Details:\n");
            _routeDetailsTextBox.SelectionFont = _routeDetailsTextBox.Font;

            // Display each stop with detailed routing information
            foreach (var stop in routeDetail.StopDetails)
            {
                if (stop.PassengerId >= 0)
                {
                    var passenger = vehicle.AssignedPassengers.FirstOrDefault(p => p.Id == stop.PassengerId);
                    if (passenger != null)
                    {
                        _routeDetailsTextBox.AppendText($"{stop.StopNumber}. {passenger.Name}\n");

                        string location = !string.IsNullOrEmpty(passenger.Address)
                            ? passenger.Address
                            : $"({passenger.Latitude:F4}, {passenger.Longitude:F4})";

                        _routeDetailsTextBox.AppendText($"   Pickup at: {location}\n");

                        if (!string.IsNullOrEmpty(passenger.EstimatedPickupTime))
                        {
                            _routeDetailsTextBox.SelectionFont = new Font(_routeDetailsTextBox.Font, FontStyle.Bold);
                            _routeDetailsTextBox.AppendText($"   Estimated pickup time: {passenger.EstimatedPickupTime}\n");
                            _routeDetailsTextBox.SelectionFont = _routeDetailsTextBox.Font;
                        }

                        // Add Google-specific routing details
                        _routeDetailsTextBox.AppendText($"   Distance from previous: {stop.DistanceFromPrevious:F2} km\n");
                        _routeDetailsTextBox.AppendText($"   Time from previous: {TimeFormatter.FormatMinutesWithUnits(stop.TimeFromPrevious)}\n");
                        _routeDetailsTextBox.AppendText($"   Cumulative distance: {stop.CumulativeDistance:F2} km\n");
                        _routeDetailsTextBox.AppendText("\n");
                    }
                }
                else if (stop.StopNumber > 0) // Destination
                {
                    _routeDetailsTextBox.SelectionFont = new Font(_routeDetailsTextBox.Font, FontStyle.Bold);
                    _routeDetailsTextBox.AppendText($"{stop.StopNumber}. Destination\n");
                    _routeDetailsTextBox.SelectionFont = _routeDetailsTextBox.Font;

                    _routeDetailsTextBox.AppendText($"   Distance from previous: {stop.DistanceFromPrevious:F2} km\n");
                    _routeDetailsTextBox.AppendText($"   Time from previous: {TimeFormatter.FormatMinutesWithUnits(stop.TimeFromPrevious)}\n");
                    _routeDetailsTextBox.AppendText($"   Arrival time: {stop.CumulativeTime:F0} minutes after departure\n");
                }
            }
        }

        /// <summary>
        /// Loads and displays routes for the specified date, including map visualization and list view population.
        /// </summary>
        /// <param name="date">The date for which to load routes.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown if an error occurs while loading routes or fetching Google routes.</exception>
        private async Task LoadRoutesForDateAsync(DateTime date)
        {
            try
            {
                string dateString = date.ToString("yyyy-MM-dd");
                _currentSolution = await DbService.GetSolutionForDateAsync(dateString);

                if (_currentSolution == null || _currentSolution.Vehicles.Count == 0)
                {
                    MessageDisplayer.ShowInfo(
                        $"No routes found for {date.ToShortDateString()}.",
                        "No Routes"
                    );
                    return;
                }

                // Load route paths for each vehicle
                int routeId = await GetRouteIdForDateAsync(dateString);
                if (routeId > 0)
                {
                    await LoadRoutePaths(_currentSolution, routeId);
                }

                // Initialize routing service if needed
                if (_routingService == null)
                {
                    var destination = await DbService.GetDestinationAsync();
                    _routingService = new RoutingService(
                        MapService,
                        destination.Latitude,
                        destination.Longitude,
                        destination.TargetTime
                    );
                }

                // Display routes on map using data from database
                _routingService.DisplaySolutionOnMap(_mapControl, _currentSolution);

                // Automatically fetch Google Routes
                try
                {
                    await _routingService.GetGoogleRoutesAsync(_mapControl, _currentSolution);
                    // After getting Google routes, display updated solution
                    _routingService.DisplaySolutionOnMap(_mapControl, _currentSolution);
                }
                catch (Exception ex)
                {
                    // Log the error but continue with basic routes
                    Console.WriteLine($"Error fetching Google routes: {ex.Message}");
                }

                DisplayPassengersOnMap(_currentSolution);
                DisplayRoutesInListView(_currentSolution);
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error loading routes: {ex.Message}",
                    "Load Error"
                );
            }
        }

        /// <summary>
        /// Retrieves the route ID for the specified date from the database.
        /// </summary>
        /// <param name="date">The date in "yyyy-MM-dd" format.</param>
        /// <returns>The route ID, or 0 if no route is found.</returns>
        /// <exception cref="Exception">Thrown if an error occurs during database access.</exception>
        private async Task<int> GetRouteIdForDateAsync(string date)
        {
            var parameters = new Dictionary<string, object> { { "@SolutionDate", date } };

            string query = @"
            SELECT RouteID 
            FROM Routes 
            WHERE SolutionDate = @SolutionDate 
            ORDER BY GeneratedTime DESC 
            LIMIT 1";

            return await DbService.GetScalarValueAsync<int>(query, parameters);
        }

        /// <summary>
        /// Loads route path points for each vehicle in the solution from the database.
        /// </summary>
        /// <param name="solution">The <see cref="Solution"/> containing vehicles to load paths for.</param>
        /// <param name="routeId">The ID of the route to load paths for.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task LoadRoutePaths(Solution solution, int routeId)
        {
            foreach (var vehicle in solution.Vehicles)
            {
                try
                {
                    int routeDetailId = await DbService.GetRouteDetailIdForVehicleAsync(vehicle.Id, routeId);
                    if (routeDetailId > 0)
                    {
                        var points = await DbService.GetRoutePathPointsAsync(routeDetailId);
                        Console.WriteLine($"Retrieved {points?.Count ?? 0} path points for vehicle {vehicle.Id}, routeDetailId={routeDetailId}");
                        if (points != null && points.Count > 0)
                        {
                            vehicle.RoutePath = points;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading route path for vehicle {vehicle.Id}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Displays passenger markers on the map for the given solution.
        /// </summary>
        /// <param name="solution">The <see cref="Solution"/> containing vehicles and their assigned passengers.</param>
        private void DisplayPassengersOnMap(Solution solution)
        {
            var passengersOverlay = new GMapOverlay("passengers");

            foreach (var vehicle in solution.Vehicles)
            {
                if (vehicle.AssignedPassengers == null)
                {
                    continue;
                }

                foreach (var passenger in vehicle.AssignedPassengers)
                {
                    var marker = MapOverlays.CreatePassengerMarker(passenger);
                    passengersOverlay.Markers.Add(marker);
                }
            }

            _mapControl.Overlays.Add(passengersOverlay);
        }

        /// <summary>
        /// Populates the routes list view with vehicle route information from the solution.
        /// </summary>
        /// <param name="solution">The <see cref="Solution"/> containing vehicles to display.</param>
        private void DisplayRoutesInListView(Solution solution)
        {
            _routesListView.Items.Clear();

            foreach (var vehicle in solution.Vehicles)
            {
                if (vehicle.AssignedPassengers == null || vehicle.AssignedPassengers.Count == 0)
                {
                    continue;
                }

                var item = new ListViewItem(vehicle.Id.ToString());
                item.SubItems.Add(vehicle.DriverName ?? $"Driver {vehicle.Id}");
                item.SubItems.Add(vehicle.AssignedPassengers.Count.ToString());
                item.SubItems.Add($"{vehicle.TotalDistance:F2} km");
                _routesListView.Items.Add(item);
            }
        }

        /// <summary>
        /// Displays simplified route details for a specific vehicle in the route details text box.
        /// </summary>
        /// <param name="vehicleId">The ID of the vehicle to display details for.</param>
        private void DisplayRouteDetails(int vehicleId)
        {
            if (_currentSolution == null)
            {
                return;
            }

            var vehicle = _currentSolution.Vehicles.FirstOrDefault(v => v.Id == vehicleId);
            if (vehicle == null)
            {
                return;
            }

            RouteDetailsFormatter.DisplayRouteDetails(_routeDetailsTextBox, vehicle);
        }

        /// <summary>
        /// Updates the route details text box with detailed route information, or a message if no details are available.
        /// </summary>
        private void UpdateRouteDetailsDisplay()
        {
            if (_routingService == null || _routingService.VehicleRouteDetails.Count == 0)
            {
                _routeDetailsTextBox.Clear();
                _routeDetailsTextBox.AppendText("No route details available.\n\n");
                _routeDetailsTextBox.AppendText("Load a route and use the 'Get Google Routes' button to see detailed timing information.");
                return;
            }

            RouteDetailsFormatter.DisplayDetailedRoutes(
                _routeDetailsTextBox,
                _routingService.VehicleRouteDetails,
                _currentSolution,
                DbService
            );
        }

        /// <summary>
        /// Refreshes the Routes tab by reloading routes for the currently selected date.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public override async Task RefreshTabAsync()
        {
            if (_currentSolution != null)
            {
                await LoadRoutesForDateAsync(_dateSelector.Value);
            }
        }
    }
}