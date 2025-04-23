using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SQLite;
using RideMatchProject.Models;
using RideMatchProject.Services;
using RideMatchProject.UI;
using GMap.NET.WindowsForms;

namespace RideMatchProject
{
    /// <summary>
    /// Main administrative interface for the RideMatch application
    /// </summary>
    public partial class AdminForm : Form
    {
        #region Fields
        private readonly DatabaseService _dbService;
        private readonly MapService _mapService;
        private RoutingService _routingService;
        private RideSharingGenetic _algorithmService;

        // Core UI components
        private TabControl _tabControl;
        private TabFactory _tabFactory;

        // Current solution being viewed
        private Solution _currentSolution;

        // Destination information
        private double _destinationLat;
        private double _destinationLng;
        private string _destinationName;
        private string _destinationAddress;
        private string _destinationTargetTime;
        #endregion

        #region Initialization
        public AdminForm(DatabaseService dbService, MapService mapService)
        {
            _dbService = dbService;
            _mapService = mapService;

            InitializeComponent();
            InitializeAdminInterface();

            // Add the Load event handler
            this.Load += AdminForm_Load;
        }

        private void AdminForm_Load(object sender, EventArgs e)
        {
            // Call the async method without awaiting
            Task.Run(LoadDestinationAndDataAsync);
        }

        private void InitializeAdminInterface()
        {
            Text = "RideMatch - Administrator Interface";
            Size = new Size(1200, 800);
            StartPosition = FormStartPosition.CenterScreen;

            _tabControl = new TabControl
            {
                Location = new Point(10, 10),
                Size = new Size(1170, 740),
                Dock = DockStyle.Fill
            };
            Controls.Add(_tabControl);

            // Initialize factory and tab controllers
            _tabFactory = new TabFactory(_dbService, _mapService);

            // Create and add all tabs
            CreateAndAddAllTabs();
        }

        private void CreateAndAddAllTabs()
        {
            // Create all tab pages using the factory
            var usersTabPage = _tabFactory.CreateUsersTab();
            var driversTabPage = _tabFactory.CreateDriversTab();
            var passengersTabPage = _tabFactory.CreatePassengersTab();
            var routesTabPage = _tabFactory.CreateRoutesTab();
            var destinationTabPage = _tabFactory.CreateDestinationTab();
            var schedulingTabPage = _tabFactory.CreateSchedulingTab();

            // Add all tabs to the control
            _tabControl.TabPages.AddRange(new TabPage[] {
                usersTabPage,
                driversTabPage,
                passengersTabPage,
                routesTabPage,
                destinationTabPage,
                schedulingTabPage
            });

            // Configure tab selection behavior
            _tabControl.SelectedIndexChanged += TabSelectionChanged;
        }

        private async Task LoadDestinationAndDataAsync()
        {
            try
            {
                var dest = await _dbService.GetDestinationAsync();
                _destinationLat = dest.Latitude;
                _destinationLng = dest.Longitude;
                _destinationName = dest.Name;
                _destinationAddress = dest.Address;
                _destinationTargetTime = dest.TargetTime;

                // Initialize routing service with destination coordinates
                _routingService = new RoutingService(
                    _mapService,
                    _destinationLat,
                    _destinationLng
                );

                // Load initial data
                await _tabFactory.LoadAllDataAsync();
            }
            catch (Exception ex)
            {
                if (this.IsHandleCreated)
                {
                    this.Invoke((Action)(() => {
                        MessageDisplayer.ShowError($"Error loading destination: {ex.Message}");
                    }));
                }
            }
        }
        #endregion

        #region Event Handlers
        private void TabSelectionChanged(object sender, EventArgs e)
        {
            var selectedTab = _tabControl.SelectedTab;

            if (selectedTab == null)
            {
                return;
            }

            // Delegate to the tab factory to refresh the selected tab
            TaskManager.ExecuteAsync(() => _tabFactory.RefreshTabAsync(selectedTab));
        }
        #endregion
    }

    /// <summary>
    /// Factory for creating and managing tab pages
    /// </summary>
    public class TabFactory
    {
        #region Fields
        private readonly DatabaseService _dbService;
        private readonly MapService _mapService;

        // Tab controllers
        private readonly UsersTabController _usersController;
        private readonly DriversTabController _driversController;
        private readonly PassengersTabController _passengersController;
        private readonly RoutesTabController _routesController;
        private readonly DestinationTabController _destinationController;
        private readonly SchedulingTabController _schedulingController;

        // Data managers
        private readonly DataManager _dataManager;
        #endregion

        #region Constructor
        public TabFactory(DatabaseService dbService, MapService mapService)
        {
            _dbService = dbService;
            _mapService = mapService;

            // Initialize data manager
            _dataManager = new DataManager(dbService);

            // Initialize all tab controllers
            _usersController = new UsersTabController(dbService, _dataManager);
            _driversController = new DriversTabController(dbService, mapService, _dataManager);
            _passengersController = new PassengersTabController(dbService, mapService, _dataManager);
            _routesController = new RoutesTabController(dbService, mapService, _dataManager);
            _destinationController = new DestinationTabController(dbService, mapService);
            _schedulingController = new SchedulingTabController(dbService, mapService, _dataManager);
        }
        #endregion

        #region Tab Creation Methods
        public TabPage CreateUsersTab()
        {
            var usersTab = new TabPage("Users");
            _usersController.InitializeTab(usersTab);
            return usersTab;
        }

        public TabPage CreateDriversTab()
        {
            var driversTab = new TabPage("Drivers");
            _driversController.InitializeTab(driversTab);
            return driversTab;
        }

        public TabPage CreatePassengersTab()
        {
            var passengersTab = new TabPage("Passengers");
            _passengersController.InitializeTab(passengersTab);
            return passengersTab;
        }

        public TabPage CreateRoutesTab()
        {
            var routesTab = new TabPage("Routes");
            _routesController.InitializeTab(routesTab);
            return routesTab;
        }

        public TabPage CreateDestinationTab()
        {
            var destinationTab = new TabPage("Destination");
            _destinationController.InitializeTab(destinationTab);
            return destinationTab;
        }

        public TabPage CreateSchedulingTab()
        {
            var schedulingTab = new TabPage("Scheduling");
            _schedulingController.InitializeTab(schedulingTab);
            return schedulingTab;
        }
        #endregion

        #region Data Loading Methods
        public async Task LoadAllDataAsync()
        {
            await _dataManager.LoadAllDataAsync();
        }

        public async Task RefreshTabAsync(TabPage selectedTab)
        {
            string tabName = selectedTab.Text;

            if (tabName == "Users")
            {
                await _usersController.RefreshTabAsync();
            }
            else if (tabName == "Drivers")
            {
                await _driversController.RefreshTabAsync();
            }
            else if (tabName == "Passengers")
            {
                await _passengersController.RefreshTabAsync();
            }
            else if (tabName == "Routes")
            {
                await _routesController.RefreshTabAsync();
            }
            else if (tabName == "Destination")
            {
                await _destinationController.RefreshTabAsync();
            }
            else if (tabName == "Scheduling")
            {
                await _schedulingController.RefreshTabAsync();
            }
        }
        #endregion
    }

    /// <summary>
    /// Base class for all tab controllers
    /// </summary>
    public abstract class TabControllerBase
    {
        protected readonly DatabaseService DbService;
        protected readonly MapService MapService;
        protected readonly DataManager DataManager;

        protected TabControllerBase(DatabaseService dbService, MapService mapService = null, DataManager dataManager = null)
        {
            DbService = dbService;
            MapService = mapService;
            DataManager = dataManager;
        }

        public abstract void InitializeTab(TabPage tabPage);
        public abstract Task RefreshTabAsync();
    }

    /// <summary>
    /// Controller for the Users tab
    /// </summary>
    public class UsersTabController : TabControllerBase
    {
        private ListView _usersListView;
        private Button _refreshButton;
        private Button _addButton;
        private Button _editButton;
        private Button _deleteButton;

        public UsersTabController(DatabaseService dbService, DataManager dataManager)
            : base(dbService, null, dataManager)
        {
        }

        public override void InitializeTab(TabPage tabPage)
        {
            CreateListView(tabPage);
            CreateActionButtons(tabPage);
        }

        private void CreateListView(TabPage tabPage)
        {
            _usersListView = new ListView
            {
                Location = new Point(10, 50),
                Size = new Size(1140, 650),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };

            // Add columns
            _usersListView.Columns.Add("ID", 50);
            _usersListView.Columns.Add("Username", 150);
            _usersListView.Columns.Add("User Type", 100);
            _usersListView.Columns.Add("Name", 200);
            _usersListView.Columns.Add("Email", 200);
            _usersListView.Columns.Add("Phone", 150);

            tabPage.Controls.Add(_usersListView);
        }

        private void CreateActionButtons(TabPage tabPage)
        {
            // Create Refresh Button
            _refreshButton = UIFactory.CreateButton(
                "Refresh Users",
                new Point(10, 10),
                new Size(120, 30),
                RefreshButtonClick
            );

            // Create Add Button
            _addButton = UIFactory.CreateButton(
                "Add User",
                new Point(140, 10),
                new Size(120, 30),
                AddButtonClick
            );

            // Create Edit Button
            _editButton = UIFactory.CreateButton(
                "Edit User",
                new Point(270, 10),
                new Size(120, 30),
                EditButtonClick
            );

            // Create Delete Button
            _deleteButton = UIFactory.CreateButton(
                "Delete User",
                new Point(400, 10),
                new Size(120, 30),
                DeleteButtonClick
            );

            // Add all buttons to the tab
            tabPage.Controls.Add(_refreshButton);
            tabPage.Controls.Add(_addButton);
            tabPage.Controls.Add(_editButton);
            tabPage.Controls.Add(_deleteButton);
        }

        private async void RefreshButtonClick(object sender, EventArgs e)
        {
            await RefreshTabAsync();
        }

        private void AddButtonClick(object sender, EventArgs e)
        {
            using (var regForm = new RegistrationForm(DbService))
            {
                if (regForm.ShowDialog() == DialogResult.OK)
                {
                    TaskManager.ExecuteAsync(RefreshTabAsync);
                }
            }
        }

        private void EditButtonClick(object sender, EventArgs e)
        {
            if (_usersListView.SelectedItems.Count == 0)
            {
                MessageDisplayer.ShowInfo("Please select a user to edit.", "No Selection");
                return;
            }

            // Get selected user details
            var item = _usersListView.SelectedItems[0];
            int userId = int.Parse(item.Text);
            string username = item.SubItems[1].Text;
            string userType = item.SubItems[2].Text;
            string name = item.SubItems[3].Text;
            string email = item.SubItems[4].Text;
            string phone = item.SubItems[5].Text;

            // Open editor
            using (var editForm = new UserEditForm(
                DbService, userId, username, userType, name, email, phone))
            {
                if (editForm.ShowDialog() == DialogResult.OK)
                {
                    TaskManager.ExecuteAsync(RefreshTabAsync);
                }
            }
        }

        private async void DeleteButtonClick(object sender, EventArgs e)
        {
            if (_usersListView.SelectedItems.Count == 0)
            {
                MessageDisplayer.ShowInfo("Please select a user to delete.", "No Selection");
                return;
            }

            // Get selected user
            var item = _usersListView.SelectedItems[0];
            int userId = int.Parse(item.Text);
            string username = item.SubItems[1].Text;

            // Confirm deletion
            var result = MessageDisplayer.ShowConfirmation(
                $"Are you sure you want to delete user {username}?",
                "Confirm Deletion"
            );

            if (result == DialogResult.Yes)
            {
                await DeleteUserAsync(userId, username);
            }
        }

        private async Task DeleteUserAsync(int userId, string username)
        {
            try
            {
                bool success = await DbService.DeleteUserAsync(userId);

                if (success)
                {
                    await RefreshTabAsync();
                    MessageDisplayer.ShowInfo(
                        $"User {username} deleted successfully.",
                        "User Deleted"
                    );
                }
                else
                {
                    MessageDisplayer.ShowError(
                        $"Could not delete user {username}. The user may not exist.",
                        "Deletion Failed"
                    );
                }
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error deleting user: {ex.Message}",
                    "Deletion Error"
                );
            }
        }

        public override async Task RefreshTabAsync()
        {
            await DataManager.LoadUsersAsync();
            await DisplayUsersAsync();
        }

        private async Task DisplayUsersAsync()
        {
            _usersListView.Items.Clear();

            var users = DataManager.Users;
            if (users == null)
            {
                return;
            }

            foreach (var user in users)
            {
                var item = new ListViewItem(user.Id.ToString());
                item.SubItems.Add(user.Username);
                item.SubItems.Add(user.UserType);
                item.SubItems.Add(user.Name);
                item.SubItems.Add(user.Email);
                item.SubItems.Add(user.Phone);

                _usersListView.Items.Add(item);
            }
        }
    }

    /// <summary>
    /// Controller for the Drivers tab
    /// </summary>
    public class DriversTabController : TabControllerBase
    {
        private ListView _driversListView;
        private Button _refreshButton;
        private GMapControl _mapControl;

        public DriversTabController(
            DatabaseService dbService,
            MapService mapService,
            DataManager dataManager)
            : base(dbService, mapService, dataManager)
        {
        }

        public override void InitializeTab(TabPage tabPage)
        {
            CreateListView(tabPage);
            CreateActionButtons(tabPage);
            CreateMapControl(tabPage);
        }

        private void CreateListView(TabPage tabPage)
        {
            _driversListView = new ListView
            {
                Location = new Point(10, 50),
                Size = new Size(1140, 400),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            // Add columns
            _driversListView.Columns.Add("ID", 50);
            _driversListView.Columns.Add("Driver Name", 150);
            _driversListView.Columns.Add("Vehicle Capacity", 80);
            _driversListView.Columns.Add("Start Location", 300);
            _driversListView.Columns.Add("Available Tomorrow", 120);
            _driversListView.Columns.Add("User ID", 80);

            tabPage.Controls.Add(_driversListView);
        }

        private void CreateActionButtons(TabPage tabPage)
        {
            // Create Refresh Button
            _refreshButton = UIFactory.CreateButton(
                "Refresh Drivers",
                new Point(10, 10),
                new Size(120, 30),
                RefreshButtonClick
            );
  
            // Add all buttons to the tab
            tabPage.Controls.Add(_refreshButton);
        }

        private void CreateMapControl(TabPage tabPage)
        {
            _mapControl = new GMapControl
            {
                Location = new Point(10, 460),
                Size = new Size(1140, 240),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 12,
                DragButton = MouseButtons.Left
            };

            tabPage.Controls.Add(_mapControl);
            MapService.InitializeGoogleMaps(_mapControl);
        }

        private async void RefreshButtonClick(object sender, EventArgs e)
        {
            await RefreshTabAsync();
        }



        public override async Task RefreshTabAsync()
        {
            await DataManager.LoadVehiclesAsync();
            await DisplayDriversAsync();
            DisplayVehiclesOnMap();
        }

        private async Task DisplayDriversAsync()
        {
            _driversListView.Items.Clear();

            var vehicles = DataManager.Vehicles;
            if (vehicles == null)
            {
                return;
            }

            foreach (var vehicle in vehicles)
            {
                var item = new ListViewItem(vehicle.Id.ToString());
                item.SubItems.Add(vehicle.DriverName ?? $"User {vehicle.UserId}");
                item.SubItems.Add(vehicle.Capacity.ToString());

                string location = !string.IsNullOrEmpty(vehicle.StartAddress)
                    ? vehicle.StartAddress
                    : $"({vehicle.StartLatitude:F4}, {vehicle.StartLongitude:F4})";
                item.SubItems.Add(location);

                item.SubItems.Add(vehicle.IsAvailableTomorrow ? "Yes" : "No");
                item.SubItems.Add(vehicle.UserId.ToString());

                _driversListView.Items.Add(item);
            }
        }

        private void DisplayVehiclesOnMap()
        {
            var vehicles = DataManager.Vehicles;
            var destination = DataManager.Destination;

            if (vehicles == null || _mapControl == null)
            {
                return;
            }

            _mapControl.Overlays.Clear();
            var overlay = new GMapOverlay("vehicles");

            foreach (var vehicle in vehicles)
            {
                var marker = MapOverlays.CreateVehicleMarker(vehicle);
                overlay.Markers.Add(marker);
            }

            // Add destination marker if available
            if (destination != default)
            {
                var destMarker = MapOverlays.CreateDestinationMarker(
                    destination.Latitude,
                    destination.Longitude
                );
                overlay.Markers.Add(destMarker);
            }

            _mapControl.Overlays.Add(overlay);

            // Center map on first vehicle or destination
            CenterMapOnFirstPointOfInterest(vehicles, destination);
        }

        private void CenterMapOnFirstPointOfInterest(
            List<Vehicle> vehicles,
            (int Id, string Name, double Latitude, double Longitude, string Address, string TargetTime) destination)
        {
            if (vehicles != null && vehicles.Any())
            {
                var firstVehicle = vehicles.First();
                _mapControl.Position = new GMap.NET.PointLatLng(
                    firstVehicle.StartLatitude,
                    firstVehicle.StartLongitude
                );
            }
            else if (destination != default)
            {
                _mapControl.Position = new GMap.NET.PointLatLng(
                    destination.Latitude,
                    destination.Longitude
                );
            }

            _mapControl.Zoom = 12;
        }
    }

    /// <summary>
    /// Controller for the Passengers tab
    /// </summary>
    public class PassengersTabController : TabControllerBase
    {
        private ListView _passengersListView;
        private Button _refreshButton;
        private GMapControl _mapControl;

        public PassengersTabController(
            DatabaseService dbService,
            MapService mapService,
            DataManager dataManager)
            : base(dbService, mapService, dataManager)
        {
        }

        public override void InitializeTab(TabPage tabPage)
        {
            CreateListView(tabPage);
            CreateActionButtons(tabPage);
            CreateMapControl(tabPage);
        }

        private void CreateListView(TabPage tabPage)
        {
            _passengersListView = new ListView
            {
                Location = new Point(10, 50),
                Size = new Size(1140, 400),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            // Add columns
            _passengersListView.Columns.Add("ID", 50);
            _passengersListView.Columns.Add("Name", 150);
            _passengersListView.Columns.Add("Location", 300);
            _passengersListView.Columns.Add("Available Tomorrow", 120);
            _passengersListView.Columns.Add("User ID", 80);

            tabPage.Controls.Add(_passengersListView);
        }

        private void CreateActionButtons(TabPage tabPage)
        {
            // Create Refresh Button
            _refreshButton = UIFactory.CreateButton(
                "Refresh Passengers",
                new Point(10, 10),
                new Size(120, 30),
                RefreshButtonClick
            );


            // Add all buttons to the tab
            tabPage.Controls.Add(_refreshButton);

        }

        private void CreateMapControl(TabPage tabPage)
        {
            _mapControl = new GMapControl
            {
                Location = new Point(10, 460),
                Size = new Size(1140, 240),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 12,
                DragButton = MouseButtons.Left
            };

            tabPage.Controls.Add(_mapControl);
            MapService.InitializeGoogleMaps(_mapControl);
        }

        private async void RefreshButtonClick(object sender, EventArgs e)
        {
            await RefreshTabAsync();
        }

 


        public override async Task RefreshTabAsync()
        {
            await DataManager.LoadPassengersAsync();
            await DisplayPassengersAsync();
            DisplayPassengersOnMap();
        }

        private async Task DisplayPassengersAsync()
        {
            _passengersListView.Items.Clear();

            var passengers = DataManager.Passengers;
            if (passengers == null)
            {
                return;
            }

            foreach (var passenger in passengers)
            {
                var item = new ListViewItem(passenger.Id.ToString());
                item.SubItems.Add(passenger.Name);

                string location = !string.IsNullOrEmpty(passenger.Address)
                    ? passenger.Address
                    : $"({passenger.Latitude:F4}, {passenger.Longitude:F4})";
                item.SubItems.Add(location);

                item.SubItems.Add(passenger.IsAvailableTomorrow ? "Yes" : "No");
                item.SubItems.Add(passenger.UserId.ToString());

                _passengersListView.Items.Add(item);
            }
        }

        private void DisplayPassengersOnMap()
        {
            var passengers = DataManager.Passengers;
            var destination = DataManager.Destination;

            if (passengers == null || _mapControl == null)
            {
                return;
            }

            _mapControl.Overlays.Clear();
            var overlay = new GMapOverlay("passengers");

            foreach (var passenger in passengers)
            {
                var marker = MapOverlays.CreatePassengerMarker(passenger);
                overlay.Markers.Add(marker);
            }

            // Add destination marker if available
            if (destination != default)
            {
                var destMarker = MapOverlays.CreateDestinationMarker(
                    destination.Latitude,
                    destination.Longitude
                );
                overlay.Markers.Add(destMarker);
            }

            _mapControl.Overlays.Add(overlay);

            // Center map on first passenger or destination
            CenterMapOnFirstPointOfInterest(passengers, destination);
        }

        private void CenterMapOnFirstPointOfInterest(
            List<Passenger> passengers,
            (int Id, string Name, double Latitude, double Longitude, string Address, string TargetTime) destination)
        {
            if (passengers != null && passengers.Any())
            {
                var firstPassenger = passengers.First();
                _mapControl.Position = new GMap.NET.PointLatLng(
                    firstPassenger.Latitude,
                    firstPassenger.Longitude
                );
            }
            else if (destination != default)
            {
                _mapControl.Position = new GMap.NET.PointLatLng(
                    destination.Latitude,
                    destination.Longitude
                );
            }

            _mapControl.Zoom = 12;
        }
    }

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
            DataManager dataManager)
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
            var dateLabel = UIFactory.CreateLabel(
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
            _loadButton = UIFactory.CreateButton(
                "Load Routes",
                new Point(350, 10),
                new Size(120, 30),
                LoadButtonClick
            );

            // Google Routes button
            _getGoogleRoutesButton = UIFactory.CreateButton(
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
            var routesPanel = UIFactory.CreatePanel(
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

    /// <summary>
    /// Controller for the Destination tab
    /// </summary>
    public class DestinationTabController : TabControllerBase
    {
        private TextBox _nameTextBox;
        private TextBox _timeTextBox;
        private TextBox _latTextBox;
        private TextBox _lngTextBox;
        private TextBox _addressTextBox;
        private Button _updateTimeButton;
        private Button _searchButton;
        private Button _saveButton;
        private GMapControl _mapControl;

        public DestinationTabController(
            DatabaseService dbService,
            MapService mapService)
            : base(dbService, mapService)
        {
        }

        public override void InitializeTab(TabPage tabPage)
        {
            CreateDestinationPanel(tabPage);
        }

        private void CreateDestinationPanel(TabPage tabPage)
        {
            // Main panel
            var panel = UIFactory.CreatePanel(
                new Point(10, 50),
                new Size(1140, 660),
                BorderStyle.FixedSingle
            );

            // Name label and textbox
            panel.Controls.Add(UIFactory.CreateLabel(
                "Destination Name:",
                new Point(20, 20),
                new Size(150, 25)
            ));
            _nameTextBox = UIFactory.CreateTextBox(
                new Point(180, 20),
                new Size(300, 25)
            );
            panel.Controls.Add(_nameTextBox);

            // Target time label and textbox
            panel.Controls.Add(UIFactory.CreateLabel(
                "Target Arrival Time:",
                new Point(20, 60),
                new Size(150, 25)
            ));
            _timeTextBox = UIFactory.CreateTextBox(
                new Point(180, 60),
                new Size(150, 25),
                "08:00:00"
            );
            panel.Controls.Add(_timeTextBox);

            // Update time button
            _updateTimeButton = UIFactory.CreateButton(
                "Update Arrival Time",
                new Point(340, 60),
                new Size(150, 25),
                UpdateTimeButtonClick
            );
            panel.Controls.Add(_updateTimeButton);

            // Map control
            _mapControl = new GMapControl
            {
                Location = new Point(20, 120),
                Size = new Size(800, 500),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 12,
                DragButton = MouseButtons.Left
            };
            panel.Controls.Add(_mapControl);
            MapService.InitializeGoogleMaps(_mapControl);

            // Location input fields
            CreateLocationInputFields(panel);

            // Add panel to tab
            tabPage.Controls.Add(panel);
        }

        private void CreateLocationInputFields(Panel panel)
        {
            // Latitude
            panel.Controls.Add(UIFactory.CreateLabel(
                "Latitude:",
                new Point(850, 120),
                new Size(80, 25)
            ));
            _latTextBox = UIFactory.CreateTextBox(
                new Point(940, 120),
                new Size(180, 25)
            );
            panel.Controls.Add(_latTextBox);

            // Longitude
            panel.Controls.Add(UIFactory.CreateLabel(
                "Longitude:",
                new Point(850, 155),
                new Size(80, 25)
            ));
            _lngTextBox = UIFactory.CreateTextBox(
                new Point(940, 155),
                new Size(180, 25)
            );
            panel.Controls.Add(_lngTextBox);

            // Address
            panel.Controls.Add(UIFactory.CreateLabel(
                "Address:",
                new Point(850, 190),
                new Size(80, 25)
            ));
            _addressTextBox = UIFactory.CreateTextBox(
                new Point(940, 190),
                new Size(180, 60),
                "",
                true
            );
            panel.Controls.Add(_addressTextBox);

            // Search button
            _searchButton = UIFactory.CreateButton(
                "Search Address",
                new Point(940, 260),
                new Size(180, 30),
                SearchButtonClick
            );
            panel.Controls.Add(_searchButton);

            // Save button
            _saveButton = UIFactory.CreateButton(
                "Save Destination",
                new Point(940, 310),
                new Size(180, 30),
                SaveButtonClick
            );
            panel.Controls.Add(_saveButton);
        }

        private async void UpdateTimeButtonClick(object sender, EventArgs e)
        {
            await UpdateTargetTimeAsync(_timeTextBox.Text);
        }

        private async void SearchButtonClick(object sender, EventArgs e)
        {
            await SearchAddressAsync();
        }

        private async void SaveButtonClick(object sender, EventArgs e)
        {
            await SaveDestinationAsync();
        }

        private async Task UpdateTargetTimeAsync(string timeString)
        {
            try
            {
                // Validate time format (HH:MM:SS)
                if (!TimeSpan.TryParse(timeString, out TimeSpan _))
                {
                    MessageDisplayer.ShowWarning(
                        "Please enter a valid time in format HH:MM:SS",
                        "Invalid Time Format"
                    );
                    return;
                }

                // Get current destination values
                var dest = await DbService.GetDestinationAsync();

                // Update just the target time
                bool success = await DbService.UpdateDestinationAsync(
                    dest.Name,
                    dest.Latitude,
                    dest.Longitude,
                    timeString,
                    dest.Address
                );

                if (success)
                {
                    MessageDisplayer.ShowInfo(
                        "Target arrival time updated successfully.",
                        "Success"
                    );
                }
                else
                {
                    MessageDisplayer.ShowError(
                        "Failed to update target arrival time.",
                        "Error"
                    );
                }
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error updating target time: {ex.Message}",
                    "Error"
                );
            }
        }

        private async Task SearchAddressAsync()
        {
            try
            {
                var result = await MapService.GeocodeAddressAsync(_addressTextBox.Text);
                if (result.HasValue)
                {
                    _latTextBox.Text = result.Value.Latitude.ToString();
                    _lngTextBox.Text = result.Value.Longitude.ToString();
                    _mapControl.Position = new GMap.NET.PointLatLng(
                        result.Value.Latitude,
                        result.Value.Longitude
                    );
                    _mapControl.Zoom = 15;

                    // Show marker
                    DisplayMarkerAtLocation(
                        result.Value.Latitude,
                        result.Value.Longitude
                    );
                }
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error searching address: {ex.Message}",
                    "Search Error"
                );
            }
        }

        private void DisplayMarkerAtLocation(double latitude, double longitude)
        {
            _mapControl.Overlays.Clear();
            var overlay = new GMapOverlay("destinationMarker");
            var marker = new GMap.NET.WindowsForms.Markers.GMarkerGoogle(
                new GMap.NET.PointLatLng(latitude, longitude),
                GMap.NET.WindowsForms.Markers.GMarkerGoogleType.red
            );
            overlay.Markers.Add(marker);
            _mapControl.Overlays.Add(overlay);
        }

        private async Task SaveDestinationAsync()
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
                {
                    MessageDisplayer.ShowWarning(
                        "Please enter a destination name.",
                        "Validation Error"
                    );
                    return;
                }

                if (!double.TryParse(_latTextBox.Text, out double lat) ||
                    !double.TryParse(_lngTextBox.Text, out double lng))
                {
                    MessageDisplayer.ShowWarning(
                        "Please enter valid coordinates.",
                        "Validation Error"
                    );
                    return;
                }

                // Save destination
                bool success = await DbService.UpdateDestinationAsync(
                    _nameTextBox.Text,
                    lat,
                    lng,
                    _timeTextBox.Text,
                    _addressTextBox.Text
                );

                if (success)
                {
                    MessageDisplayer.ShowInfo(
                        "Destination updated successfully.",
                        "Success"
                    );
                }
                else
                {
                    MessageDisplayer.ShowError(
                        "Failed to update destination.",
                        "Error"
                    );
                }
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error saving destination: {ex.Message}",
                    "Save Error"
                );
            }
        }

        public override async Task RefreshTabAsync()
        {
            try
            {
                var dest = await DbService.GetDestinationAsync();

                // Update UI with destination info
                _nameTextBox.Text = dest.Name;
                _timeTextBox.Text = dest.TargetTime;
                _latTextBox.Text = dest.Latitude.ToString();
                _lngTextBox.Text = dest.Longitude.ToString();
                _addressTextBox.Text = dest.Address;

                // Show on map
                _mapControl.Position = new GMap.NET.PointLatLng(
                    dest.Latitude,
                    dest.Longitude
                );
                _mapControl.Zoom = 15;

                // Show marker
                DisplayMarkerAtLocation(dest.Latitude, dest.Longitude);
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error loading destination: {ex.Message}",
                    "Loading Error"
                );
            }
        }
    }

    /// <summary>
    /// Controller for the Scheduling tab
    /// </summary>
    public class SchedulingTabController : TabControllerBase
    {
        private CheckBox _enabledCheckBox;
        private DateTimePicker _timeSelector;
        private CheckBox _useGoogleApiCheckBox;
        private ListView _historyListView;
        private Button _saveButton;
        private Button _runNowButton;
        private Button _refreshHistoryButton;

        public SchedulingTabController(
            DatabaseService dbService,
            MapService mapService,
            DataManager dataManager)
            : base(dbService, mapService, dataManager)
        {
        }

        public override void InitializeTab(TabPage tabPage)
        {
            CreateSchedulingPanel(tabPage);
        }

        private void CreateSchedulingPanel(TabPage tabPage)
        {
            // Main panel
            var panel = UIFactory.CreatePanel(
                new Point(10, 50),
                new Size(1140, 660),
                BorderStyle.FixedSingle
            );

            // Enable scheduling checkbox
            _enabledCheckBox = UIFactory.CreateCheckBox(
                "Enable Automatic Scheduling",
                new Point(20, 20),
                new Size(250, 25),
                false
            );
            panel.Controls.Add(_enabledCheckBox);

            // Time selection
            panel.Controls.Add(UIFactory.CreateLabel(
                "Run Scheduler At:",
                new Point(20, 60),
                new Size(150, 25)
            ));
            _timeSelector = new DateTimePicker
            {
                Location = new Point(180, 60),
                Size = new Size(120, 25),
                Format = DateTimePickerFormat.Time,
                ShowUpDown = true,
                Value = DateTime.Parse("00:00:00")
            };
            panel.Controls.Add(_timeSelector);

            // Create buttons
            CreateActionButtons(panel);

            // Google API setting
            _useGoogleApiCheckBox = UIFactory.CreateCheckBox(
                "Always Use Google Routes API",
                new Point(20, 140),
                new Size(270, 20),
                true
            );
            _useGoogleApiCheckBox.CheckedChanged += UseGoogleApiCheckBox_CheckedChanged;
            panel.Controls.Add(_useGoogleApiCheckBox);

            // History section
            CreateHistorySection(panel);

            // Add panel to tab
            tabPage.Controls.Add(panel);
        }

        private void CreateActionButtons(Panel panel)
        {
            // Save button
            _saveButton = UIFactory.CreateButton(
                "Save Settings",
                new Point(20, 100),
                new Size(120, 30),
                SaveButtonClick
            );
            panel.Controls.Add(_saveButton);

            // Run now button
            _runNowButton = UIFactory.CreateButton(
                "Run Scheduler Now",
                new Point(150, 100),
                new Size(150, 30),
                RunNowButtonClick
            );
            panel.Controls.Add(_runNowButton);
        }

        private void CreateHistorySection(Panel panel)
        {
            // Section label
            panel.Controls.Add(UIFactory.CreateLabel(
                "Scheduling History:",
                new Point(20, 170),
                new Size(150, 25)
            ));

            // History listview
            _historyListView = new ListView
            {
                Location = new Point(20, 200),
                Size = new Size(1100, 440),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            _historyListView.Columns.Add("Date", 150);
            _historyListView.Columns.Add("Status", 100);
            _historyListView.Columns.Add("Routes Generated", 150);
            _historyListView.Columns.Add("Passengers Assigned", 150);
            _historyListView.Columns.Add("Run Time", 200);
            panel.Controls.Add(_historyListView);

            // Refresh history button
            _refreshHistoryButton = UIFactory.CreateButton(
                "Refresh History",
                new Point(20, 650),
                new Size(150, 30),
                RefreshHistoryButtonClick
            );
            panel.Controls.Add(_refreshHistoryButton);
        }

        private async void SaveButtonClick(object sender, EventArgs e)
        {
            await SaveSettingsAsync();
        }

        private async void RunNowButtonClick(object sender, EventArgs e)
        {
            var result = MessageDisplayer.ShowConfirmation(
                "Are you sure you want to run the scheduler now? This will calculate routes for tomorrow.",
                "Confirm Run"
            );

            if (result == DialogResult.Yes)
            {
                await RunSchedulerAsync();
            }
        }

        private async void UseGoogleApiCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            await SaveGoogleApiSettingAsync();
        }

        private async void RefreshHistoryButtonClick(object sender, EventArgs e)
        {
            await RefreshHistoryAsync();
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                _saveButton.Enabled = false;
                _saveButton.Text = "Saving...";

                await DbService.SaveSchedulingSettingsAsync(
                    _enabledCheckBox.Checked,
                    _timeSelector.Value
                );

                MessageDisplayer.ShowInfo(
                    "Scheduling settings saved successfully.",
                    "Settings Saved"
                );
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error saving settings: {ex.Message}",
                    "Save Error"
                );
            }
            finally
            {
                _saveButton.Enabled = true;
                _saveButton.Text = "Save Settings";
            }
        }

        private async Task SaveGoogleApiSettingAsync()
        {
            try
            {
                await DbService.SaveSettingAsync(
                    "UseGoogleRoutesAPI",
                    _useGoogleApiCheckBox.Checked ? "1" : "0"
                );

                string message = _useGoogleApiCheckBox.Checked
                    ? "The scheduler will now use Google Routes API for all routes"
                    : "The scheduler will use estimated routes (no API calls)";

                MessageDisplayer.ShowInfo(message, "Setting Saved");
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error saving setting: {ex.Message}",
                    "Save Error"
                );
            }
        }

        private async Task RunSchedulerAsync()
        {
            try
            {
                _runNowButton.Enabled = false;
                _runNowButton.Text = "Running...";

                // Create a scheduling service and run it
                var schedulingService = new SchedulingService(
                    DbService,
                    MapService,
                    DataManager
                );

                await schedulingService.RunSchedulerAsync();
                await RefreshHistoryAsync();

                MessageDisplayer.ShowInfo(
                    "Scheduler completed successfully.",
                    "Scheduler Complete"
                );
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error running scheduler: {ex.Message}",
                    "Scheduler Error"
                );
            }
            finally
            {
                _runNowButton.Enabled = true;
                _runNowButton.Text = "Run Scheduler Now";
            }
        }

        private async Task RefreshHistoryAsync()
        {
            try
            {
                _refreshHistoryButton.Enabled = false;
                _refreshHistoryButton.Text = "Refreshing...";

                await RefreshHistoryListView();
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error refreshing history: {ex.Message}",
                    "Refresh Error"
                );
            }
            finally
            {
                _refreshHistoryButton.Enabled = true;
                _refreshHistoryButton.Text = "Refresh History";
            }
        }

        private async Task RefreshHistoryListView()
        {
            _historyListView.Items.Clear();

            try
            {
                var history = await DbService.GetSchedulingLogAsync();

                foreach (var entry in history)
                {
                    var item = new ListViewItem(entry.RunTime.ToString("yyyy-MM-dd"));
                    item.SubItems.Add(entry.Status);
                    item.SubItems.Add(entry.RoutesGenerated.ToString());
                    item.SubItems.Add(entry.PassengersAssigned.ToString());
                    item.SubItems.Add(entry.RunTime.ToString("HH:mm:ss"));

                    // Set item color based on status
                    if (entry.Status == "Success")
                    {
                        item.ForeColor = Color.Green;
                    }
                    else if (entry.Status == "Failed" || entry.Status == "Error")
                    {
                        item.ForeColor = Color.Red;
                    }
                    else if (entry.Status == "Skipped")
                    {
                        item.ForeColor = Color.Orange;
                    }

                    _historyListView.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error retrieving scheduling history: {ex.Message}",
                    "Database Error"
                );
            }
        }

        public override async Task RefreshTabAsync()
        {
            try
            {
                // Load scheduling settings
                var settings = await DbService.GetSchedulingSettingsAsync();
                _enabledCheckBox.Checked = settings.IsEnabled;
                _timeSelector.Value = settings.ScheduledTime;

                // Load Google API setting
                var useGoogleApi = await DbService.GetSettingAsync("UseGoogleRoutesAPI", "1");
                _useGoogleApiCheckBox.Checked = useGoogleApi == "1";

                // Refresh history
                await RefreshHistoryListView();
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error loading scheduling settings: {ex.Message}",
                    "Loading Error"
                );
            }
        }
    }

    /// <summary>
    /// Service to handle scheduling operations
    /// </summary>
    public class SchedulingService
    {
        private readonly DatabaseService _dbService;
        private readonly MapService _mapService;
        private readonly DataManager _dataManager;

        public SchedulingService(
            DatabaseService dbService,
            MapService mapService,
            DataManager dataManager)
        {
            _dbService = dbService;
            _mapService = mapService;
            _dataManager = dataManager;
        }

        public async Task RunSchedulerAsync()
        {
            try
            {
                // Get destination information
                var destination = await _dbService.GetDestinationAsync();

                // Get available vehicles and passengers
                var vehicles = await _dbService.GetAvailableVehiclesAsync();
                var passengers = await _dbService.GetAvailablePassengersAsync();

                // Only run if there are passengers and vehicles
                if (passengers.Count == 0 || vehicles.Count == 0)
                {
                    await LogSkippedRunAsync(passengers.Count, vehicles.Count);
                    return;
                }

                // Run the algorithm
                var solution = await RunRoutingAlgorithmAsync(
                    passengers,
                    vehicles,
                    destination
                );

                if (solution == null)
                {
                    await _dbService.LogSchedulingRunAsync(
                        DateTime.Now,
                        "Failed",
                        0,
                        0,
                        "Algorithm failed to find a valid solution"
                    );
                    throw new Exception("Algorithm failed to find a valid solution");
                }

                // Calculate routes
                await CalculateRoutesAsync(solution, destination);

                // Save the solution
                await SaveSolutionAsync(solution);
            }
            catch (Exception ex)
            {
                // Log exception
                await LogErrorAsync(ex);
                throw;
            }
        }

        private async Task LogSkippedRunAsync(int passengerCount, int vehicleCount)
        {
            await _dbService.LogSchedulingRunAsync(
                DateTime.Now,
                "Skipped",
                0,
                0,
                $"Insufficient participants: {passengerCount} passengers, {vehicleCount} vehicles"
            );

            throw new Exception(
                $"No routes generated: {passengerCount} passengers, {vehicleCount} vehicles available"
            );
        }

        private async Task<Solution> RunRoutingAlgorithmAsync(
            List<Passenger> passengers,
            List<Vehicle> vehicles,
            (int Id, string Name, double Latitude, double Longitude, string Address, string TargetTime) destination)
        {
            // Create the solver
            var solver = new RideSharingGenetic(
                passengers,
                vehicles,
                200, // Population size
                destination.Latitude,
                destination.Longitude,
                GetTargetTimeInMinutes(destination.TargetTime)
            );

            // Run the algorithm
            return solver.Solve(150); // Generations
        }

        private async Task CalculateRoutesAsync(
            Solution solution,
            (int Id, string Name, double Latitude, double Longitude, string Address, string TargetTime) destination)
        {
            // Create a routing service
            var routingService = new RoutingService(
                _mapService,
                destination.Latitude,
                destination.Longitude
            );

            // Calculate estimated routes
            routingService.CalculateEstimatedRouteDetails(solution);

            // Check if Google Routes API should be used
            string useGoogleApi = await _dbService.GetSettingAsync("UseGoogleRoutesAPI", "1");
            bool shouldUseGoogleApi = useGoogleApi == "1";

            if (shouldUseGoogleApi)
            {
                try
                {
                    // Try to get routes from Google API
                    await routingService.GetGoogleRoutesAsync(null, solution);
                }
                catch (Exception ex)
                {
                    // If Google API fails, we already have the estimated routes calculated
                    MessageDisplayer.ShowWarning(
                        $"Google API request failed: {ex.Message}. Using estimated routes instead.",
                        "API Error"
                    );
                }
            }

            // Calculate pickup times based on target arrival
            await CalculatePickupTimesAsync(solution, destination.TargetTime, routingService);
        }

        private async Task CalculatePickupTimesAsync(
            Solution solution,
            string targetTimeString,
            RoutingService routingService)
        {
            // Parse target arrival time
            if (!TimeSpan.TryParse(targetTimeString, out TimeSpan targetTime))
            {
                targetTime = new TimeSpan(8, 0, 0); // Default to 8:00 AM
            }

            // Get the target time as DateTime for today (we'll use just the time portion)
            DateTime targetDateTime = DateTime.Today.Add(targetTime);

            foreach (var vehicle in solution.Vehicles)
            {
                if (vehicle.AssignedPassengers == null || vehicle.AssignedPassengers.Count == 0)
                {
                    continue;
                }

                RouteDetails routeDetails = null;
                if (routingService.VehicleRouteDetails.ContainsKey(vehicle.Id))
                {
                    routeDetails = routingService.VehicleRouteDetails[vehicle.Id];
                }

                if (routeDetails == null)
                {
                    continue;
                }

                CalculateVehicleTimings(vehicle, routeDetails, targetDateTime);
            }
        }

        private void CalculateVehicleTimings(
            Vehicle vehicle,
            RouteDetails routeDetails,
            DateTime targetDateTime)
        {
            // Get total trip time from start to destination in minutes
            double totalTripTime = routeDetails.TotalTime;

            // Calculate when driver needs to start to arrive at destination at target time
            DateTime driverStartTime = targetDateTime.AddMinutes(-totalTripTime);

            // Store the driver's departure time
            vehicle.DepartureTime = driverStartTime.ToString("HH:mm");

            // Calculate each passenger's pickup time
            CalculatePassengerPickupTimes(vehicle, routeDetails, driverStartTime);
        }

        private void CalculatePassengerPickupTimes(
            Vehicle vehicle,
            RouteDetails routeDetails,
            DateTime driverStartTime)
        {
            for (int i = 0; i < vehicle.AssignedPassengers.Count; i++)
            {
                var passenger = vehicle.AssignedPassengers[i];

                // Find corresponding stop detail
                var stopDetail = routeDetails.StopDetails.FirstOrDefault(
                    s => s.PassengerId == passenger.Id
                );

                if (stopDetail != null)
                {
                    double cumulativeTime = stopDetail.CumulativeTime;

                    // Calculate pickup time based on driver start time plus cumulative time
                    DateTime pickupTime = driverStartTime.AddMinutes(cumulativeTime);
                    passenger.EstimatedPickupTime = pickupTime.ToString("HH:mm");
                }
            }
        }

        private async Task SaveSolutionAsync(Solution solution)
        {
            // Save the solution to database for tomorrow's date
            string tomorrowDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");
            int routeId = await _dbService.SaveSolutionAsync(solution, tomorrowDate);

            // Count assigned passengers and used vehicles
            int assignedPassengers = solution.Vehicles.Sum(
                v => v.AssignedPassengers?.Count ?? 0
            );
            int usedVehicles = solution.Vehicles.Count(
                v => v.AssignedPassengers?.Count > 0
            );

            // Log the scheduling run
            await _dbService.LogSchedulingRunAsync(
                DateTime.Now,
                "Success",
                usedVehicles,
                assignedPassengers,
                $"Created routes for {tomorrowDate}"
            );
        }

        private async Task LogErrorAsync(Exception ex)
        {
            try
            {
                await _dbService.LogSchedulingRunAsync(
                    DateTime.Now,
                    "Error",
                    0,
                    0,
                    ex.Message
                );
            }
            catch
            {
                // Just in case writing to the database also fails
            }
        }

        private int GetTargetTimeInMinutes(string targetTime)
        {
            // Convert a time string like "08:00:00" to minutes from midnight
            if (TimeSpan.TryParse(targetTime, out TimeSpan time))
            {
                return (int)time.TotalMinutes;
            }

            // Default to 8:00 AM (480 minutes)
            return 480;
        }
    }

    /// <summary>
    /// Class to handle route details formatting
    /// </summary>
    public static class RouteDetailsFormatter
    {
        public static void DisplayRouteDetails(RichTextBox textBox, Vehicle vehicle)
        {
            textBox.Clear();
            FormatHeader(textBox, $"Route Details for Vehicle {vehicle.Id}");

            textBox.AppendText($"Driver: {vehicle.DriverName ?? $"Driver {vehicle.Id}"}\n");
            textBox.AppendText($"Vehicle Capacity: {vehicle.Capacity}\n");
            textBox.AppendText($"Total Distance: {vehicle.TotalDistance:F2} km\n");
            textBox.AppendText($"Total Time: {vehicle.TotalTime:F2} minutes\n");

            // Add departure time if available
            if (!string.IsNullOrEmpty(vehicle.DepartureTime))
            {
                FormatBold(textBox, $"Departure Time: {vehicle.DepartureTime}\n");
            }

            textBox.AppendText("\n");
            FormatBold(textBox, "Pickup Order:\n");

            if (vehicle.AssignedPassengers == null || vehicle.AssignedPassengers.Count == 0)
            {
                textBox.AppendText("No passengers assigned to this vehicle.\n");
                return;
            }

            DisplayPassengerList(textBox, vehicle.AssignedPassengers);
        }

        private static void DisplayPassengerList(
            RichTextBox textBox,
            List<Passenger> passengers)
        {
            for (int i = 0; i < passengers.Count; i++)
            {
                var passenger = passengers[i];
                textBox.AppendText($"{i + 1}. {passenger.Name}\n");

                string location = !string.IsNullOrEmpty(passenger.Address)
                    ? passenger.Address
                    : $"({passenger.Latitude:F4}, {passenger.Longitude:F4})";
                textBox.AppendText($"   Pickup at: {location}\n");

                // Display estimated pickup time if available
                if (!string.IsNullOrEmpty(passenger.EstimatedPickupTime))
                {
                    FormatBold(textBox, $"   Estimated pickup time: {passenger.EstimatedPickupTime}\n");
                }

                textBox.AppendText("\n");
            }
        }

        public static void DisplayDetailedRoutes(
            RichTextBox textBox,
            Dictionary<int, RouteDetails> routeDetails,
            Solution solution,
            DatabaseService dbService)
        {
            textBox.Clear();

            if (routeDetails.Count == 0)
            {
                textBox.AppendText("No route details available.\n\n");
                textBox.AppendText("Load a route and use the 'Get Google Routes' button to see detailed timing information.");
                return;
            }

            foreach (var detail in routeDetails.Values.OrderBy(d => d.VehicleId))
            {
                DisplaySingleRouteDetails(textBox, detail, solution, dbService);
                textBox.AppendText("--------------------------------\n\n");
            }
        }

        private static void DisplaySingleRouteDetails(
            RichTextBox textBox,
            RouteDetails detail,
            Solution solution,
            DatabaseService dbService)
        {
            // Get vehicle info
            var vehicle = solution.Vehicles.FirstOrDefault(v => v.Id == detail.VehicleId);
            string startLocation = vehicle != null && !string.IsNullOrEmpty(vehicle.StartAddress)
                ? vehicle.StartAddress
                : $"({vehicle?.StartLatitude ?? 0:F4}, {vehicle?.StartLongitude ?? 0:F4})";

            FormatBold(textBox, $"Vehicle {detail.VehicleId}\n");
            textBox.AppendText($"Start Location: {startLocation}\n");
            textBox.AppendText($"Driver: {vehicle?.DriverName ?? "Unknown"}\n");
            textBox.AppendText($"Total Distance: {detail.TotalDistance:F2} km\n");
            textBox.AppendText($"Total Time: {detail.TotalTime:F2} min\n");

            if (!string.IsNullOrEmpty(vehicle?.DepartureTime))
            {
                FormatBold(textBox, $"Departure Time: {vehicle.DepartureTime}\n");
            }

            textBox.AppendText("\n");
            FormatBold(textBox, "Stop Details:\n");

            DisplayStopDetails(textBox, detail, vehicle, dbService);
        }

        private static void DisplayStopDetails(
            RichTextBox textBox,
            RouteDetails detail,
            Vehicle vehicle,
            DatabaseService dbService)
        {
            int stopNumber = 1;
            foreach (var stop in detail.StopDetails)
            {
                if (stop.PassengerId >= 0)
                {
                    DisplayPassengerStop(textBox, stop, vehicle, stopNumber);
                }
                else
                {
                    DisplayDestinationStop(textBox, stop, dbService, stopNumber);
                }

                // Display stats for this stop
                textBox.AppendText($"   Distance: {stop.DistanceFromPrevious:F2} km\n");
                textBox.AppendText($"   Time: {stop.TimeFromPrevious:F2} min\n");
                textBox.AppendText($"   Cumulative: {stop.CumulativeDistance:F2} km, {stop.CumulativeTime:F2} min\n\n");
                stopNumber++;
            }
        }

        private static void DisplayPassengerStop(
            RichTextBox textBox,
            StopDetail stop,
            Vehicle vehicle,
            int stopNumber)
        {
            var passenger = vehicle?.AssignedPassengers?.FirstOrDefault(p => p.Id == stop.PassengerId);
            string stopName = passenger != null ? passenger.Name : $"Passenger {stop.PassengerName}";
            string stopLocation = passenger != null && !string.IsNullOrEmpty(passenger.Address)
                ? passenger.Address
                : $"({passenger?.Latitude ?? 0:F4}, {passenger?.Longitude ?? 0:F4})";

            FormatBold(textBox, $"{stopNumber}. {stopName}\n");
            textBox.AppendText($"   Location: {stopLocation}\n");

            // Display estimated pickup time if available
            if (passenger != null && !string.IsNullOrEmpty(passenger.EstimatedPickupTime))
            {
                FormatBold(textBox, $"   Pickup Time: {passenger.EstimatedPickupTime}\n");
            }
        }

        private static void DisplayDestinationStop(
            RichTextBox textBox,
            StopDetail stop,
            DatabaseService dbService,
            int stopNumber)
        {
            string stopName = "Destination";
            string stopLocation = string.Empty;

            try
            {
                var dest = dbService.GetDestinationAsync().GetAwaiter().GetResult();
                stopLocation = !string.IsNullOrEmpty(dest.Address)
                    ? dest.Address
                    : $"({dest.Latitude:F4}, {dest.Longitude:F4})";
            }
            catch
            {
                stopLocation = "Destination coordinates unavailable";
            }

            FormatBold(textBox, $"{stopNumber}. {stopName}\n");
            textBox.AppendText($"   Location: {stopLocation}\n");
        }

        private static void FormatHeader(RichTextBox textBox, string text)
        {
            textBox.SelectionFont = new Font(textBox.Font, FontStyle.Bold);
            textBox.AppendText(text + "\n\n");
            textBox.SelectionFont = textBox.Font;
        }

        private static void FormatBold(RichTextBox textBox, string text)
        {
            textBox.SelectionFont = new Font(textBox.Font, FontStyle.Bold);
            textBox.AppendText(text);
            textBox.SelectionFont = textBox.Font;
        }
    }

    /// <summary>
    /// Factory for creating UI elements
    /// </summary>
    public static class UIFactory
    {
        public static Button CreateButton(
            string text,
            Point location,
            Size size,
            EventHandler clickHandler)
        {
            var button = new Button
            {
                Text = text,
                Location = location,
                Size = size
            };

            if (clickHandler != null)
            {
                button.Click += clickHandler;
            }

            return button;
        }

        public static Label CreateLabel(
            string text,
            Point location,
            Size size,
            ContentAlignment textAlign = ContentAlignment.MiddleLeft)
        {
            var label = new Label
            {
                Text = text,
                Location = location,
                Size = size,
                TextAlign = textAlign
            };

            return label;
        }

        public static TextBox CreateTextBox(
            Point location,
            Size size,
            string text = "",
            bool multiline = false)
        {
            return new TextBox
            {
                Location = location,
                Size = size,
                Text = text,
                Multiline = multiline
            };
        }

        public static CheckBox CreateCheckBox(
            string text,
            Point location,
            Size size,
            bool isChecked = false)
        {
            return new CheckBox
            {
                Text = text,
                Location = location,
                Size = size,
                Checked = isChecked
            };
        }

        public static Panel CreatePanel(
            Point location,
            Size size,
            BorderStyle borderStyle = BorderStyle.None)
        {
            return new Panel
            {
                Location = location,
                Size = size,
                BorderStyle = borderStyle
            };
        }
    }

    /// <summary>
    /// Central manager for application data
    /// </summary>
    public class DataManager
    {
        private readonly DatabaseService _dbService;

        public List<(int Id, string Username, string UserType, string Name, string Email, string Phone)> Users { get; private set; }
        public List<Vehicle> Vehicles { get; private set; }
        public List<Passenger> Passengers { get; private set; }
        public (int Id, string Name, double Latitude, double Longitude, string Address, string TargetTime) Destination { get; private set; }

        public DataManager(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        public async Task LoadAllDataAsync()
        {
            await LoadUsersAsync();
            await LoadVehiclesAsync();
            await LoadPassengersAsync();
            await LoadDestinationAsync();
        }

        public async Task LoadUsersAsync()
        {
            try
            {
                Users = await _dbService.GetAllUsersAsync();
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error loading users: {ex.Message}",
                    "Database Error"
                );

                // Fallback to sample data
                Users = new List<(int Id, string Username, string UserType, string Name, string Email, string Phone)>
                {
                    (1, "admin", "Admin", "Administrator", "", ""),
                    (2, "driver1", "Driver", "John Driver", "", ""),
                    (3, "passenger1", "Passenger", "Alice Passenger", "", "")
                };
            }
        }

        public async Task LoadVehiclesAsync()
        {
            try
            {
                Vehicles = await _dbService.GetAllVehiclesAsync();
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error loading vehicles: {ex.Message}",
                    "Database Error"
                );

                // Fallback to empty list
                Vehicles = new List<Vehicle>();
            }
        }

        public async Task LoadPassengersAsync()
        {
            try
            {
                Passengers = await _dbService.GetAllPassengersAsync();
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error loading passengers: {ex.Message}",
                    "Database Error"
                );

                // Fallback to empty list
                Passengers = new List<Passenger>();
            }
        }

        public async Task LoadDestinationAsync()
        {
            try
            {
                Destination = await _dbService.GetDestinationAsync();
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error loading destination: {ex.Message}",
                    "Database Error"
                );

                // Fallback to default
                Destination = (0, "Default Destination", 0, 0, "Unknown", "08:00:00");
            }
        }
    }

    /// <summary>
    /// Utility class for standard message dialogs
    /// </summary>
    public static class MessageDisplayer
    {
        public static void ShowInfo(string message, string title = "Information")
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        public static void ShowWarning(string message, string title = "Warning")
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }

        public static void ShowError(string message, string title = "Error")
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }

        public static DialogResult ShowConfirmation(string message, string title = "Confirm")
        {
            return MessageBox.Show(
                message,
                title,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
        }
    }

    /// <summary>
    /// Utility class for async task execution
    /// </summary>
    public static class TaskManager
    {
        public static async void ExecuteAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Operation failed: {ex.Message}",
                    "Error"
                );
            }
        }
    }
}
