using GMap.NET.WindowsForms;
using RideMatchProject.Models;
using RideMatchProject.Services;
using RideMatchProject.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.AdminClasses
{
    /// <summary>
    /// Controller for the Routes tab
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

        public RoutesTabController(
            DatabaseService dbService,
            MapService mapService,
            AdminDataManager dataManager)
            : base(dbService, mapService, dataManager)
        {
        }

        public override void InitializeTab(TabPage tabPage)
        {
            CreateDateSelectionControls(tabPage);
            CreateRoutesPanel(tabPage);
        }

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

            // Google Routes button
            _getGoogleRoutesButton = AdminUIFactory.CreateButton(
                "Get Google Routes",
                new Point(520, 10),
                new Size(150, 30),
                GetGoogleRoutesButtonClick
            );

            // Add controls to tab
            tabPage.Controls.Add(dateLabel);
            tabPage.Controls.Add(_dateSelector);
            tabPage.Controls.Add(_loadButton);
            tabPage.Controls.Add(_getGoogleRoutesButton);
        }

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

        private async void LoadButtonClick(object sender, EventArgs e)
        {
            await LoadRoutesForDateAsync(_dateSelector.Value);
        }

        private async void GetGoogleRoutesButtonClick(object sender, EventArgs e)
        {
            await GetGoogleRoutesAsync();
        }

        private void RoutesListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_routesListView.SelectedItems.Count > 0)
            {
                int vehicleId = int.Parse(_routesListView.SelectedItems[0].SubItems[0].Text);
                DisplayRouteDetails(vehicleId);
            }
        }

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

                // Initialize routing service if needed
                if (_routingService == null)
                {
                    var destination = await DbService.GetDestinationAsync();
                    _routingService = new RoutingService(
                        MapService,
                        destination.Latitude,
                        destination.Longitude
                    );
                }

                // Display routes on map
                _routingService.DisplaySolutionOnMap(_mapControl, _currentSolution);
                DisplayPassengersOnMap(_currentSolution);

                // Display routes in list
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

        private async Task GetGoogleRoutesAsync()
        {
            try
            {
                if (_currentSolution == null)
                {
                    MessageDisplayer.ShowWarning(
                        "Please load a route first!",
                        "No Route Loaded"
                    );
                    return;
                }

                _getGoogleRoutesButton.Enabled = false;
                _getGoogleRoutesButton.Text = "Getting Routes...";

                try
                {
                    // Create a new routing service with the destination
                    var destination = await DbService.GetDestinationAsync();
                    var tempRoutingService = new RoutingService(
                        MapService,
                        destination.Latitude,
                        destination.Longitude
                    );

                    // Get the Google routes
                    await tempRoutingService.GetGoogleRoutesAsync(_mapControl, _currentSolution);

                    // Update routing service reference
                    _routingService = tempRoutingService;

                    // Update display
                    UpdateRouteDetailsDisplay();

                    MessageDisplayer.ShowInfo(
                        "Routes updated with Google API data!",
                        "Routes Updated"
                    );
                }
                catch (Exception ex)
                {
                    MessageDisplayer.ShowError(
                        $"Error getting Google routes: {ex.Message}",
                        "API Error"
                    );
                }
            }
            finally
            {
                _getGoogleRoutesButton.Enabled = true;
                _getGoogleRoutesButton.Text = "Get Google Routes";
            }
        }

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

        public override async Task RefreshTabAsync()
        {
            if (_currentSolution != null)
            {
                await LoadRoutesForDateAsync(_dateSelector.Value);
            }
        }
    }
}
