using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using RideMatchProject.Models;
using RideMatchProject.Services;
using RideMatchProject.UI;

namespace RideMatchProject
{
    /// <summary>
    /// Main form for driver interface
    /// </summary>
    public partial class DriverForm : Form
    {
        private DriverDataManager dataManager;
        private DriverUIManager uiManager;
        private DriverMapManager mapManager;
        private DriverLocationManager locationManager;

        public DriverForm(DatabaseService dbService, MapService mapService, int userId, string username)
        {
            ValidateArguments(dbService, mapService, username);
            InitializeComponent();
            InitializeManagers(dbService, mapService, userId, username);
        }

        private void ValidateArguments(DatabaseService dbService, MapService mapService, string username)
        {
            if (dbService == null) throw new ArgumentNullException(nameof(dbService));
            if (mapService == null) throw new ArgumentNullException(nameof(mapService));
            if (string.IsNullOrEmpty(username)) throw new ArgumentNullException(nameof(username));
        }

        private void InitializeManagers(DatabaseService dbService, MapService mapService, int userId, string username)
        {
            dataManager = new DriverDataManager(dbService, userId, username);
            mapManager = new DriverMapManager(mapService, dbService);
            locationManager = new DriverLocationManager(mapService, dataManager);
            uiManager = new DriverUIManager(this, dataManager, mapManager, locationManager);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            try
            {
                uiManager.InitializeUI();
                mapManager.InitializeMap(uiManager.MapControl, 32.0741, 34.7922);

                Task.Run(async () => await LoadDataAndRefreshUI())
                    .ContinueWith(HandleLoadingError, TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error initializing driver form", ex.Message);
            }
        }

        // This method is required by the designer
        private void DriverForm_Load(object sender, EventArgs e)
        {
            // This is intentionally left empty as we use OnLoad instead
        }

        private void HandleLoadingError(Task task)
        {
            if (task.Exception == null) return;

            this.Invoke(new Action(() => {
                ShowErrorMessage("Error loading driver data",
                    task.Exception.InnerException?.Message ?? task.Exception.Message);
            }));
        }

        private async Task LoadDataAndRefreshUI()
        {
            await dataManager.LoadDriverDataAsync();

            this.Invoke(new Action(() => {
                uiManager.RefreshUI();
                mapManager.DisplayRouteOnMap(dataManager.Vehicle, dataManager.AssignedPassengers);
            }));
        }

        private void ShowErrorMessage(string title, string message)
        {
            MessageBox.Show($"{title}: {message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Models for data transfer
    /// </summary>
    public class RouteData
    {
        public Vehicle Vehicle { get; set; }
        public List<Passenger> Passengers { get; set; }
        public DateTime? PickupTime { get; set; }
    }

    /// <summary>
    /// Manages driver data and database operations
    /// </summary>
    public class DriverDataManager
    {
        private readonly DatabaseService dbService;
        private readonly int userId;
        private readonly string username;

        public Vehicle Vehicle { get; private set; }
        public List<Passenger> AssignedPassengers { get; private set; } = new List<Passenger>();
        public DateTime? PickupTime { get; private set; }

        public DriverDataManager(DatabaseService dbService, int userId, string username)
        {
            this.dbService = dbService;
            this.userId = userId;
            this.username = username;
        }

        public async Task LoadDriverDataAsync()
        {
            try
            {
                Vehicle = await dbService.GetVehicleByUserIdAsync(userId);

                if (Vehicle == null)
                {
                    InitializeDefaultVehicle();
                    return;
                }

                var destination = await dbService.GetDestinationAsync();
                string queryDate = CalculateQueryDate(destination.TargetTime);

                // The GetDriverRouteAsync returns a tuple (Vehicle, List<Passenger>, DateTime?)
                var routeData = await dbService.GetDriverRouteAsync(userId, queryDate);

                // Properly unpack the tuple
                if (routeData.Item1 != null)
                {
                    Vehicle = routeData.Item1;
                }

                AssignedPassengers = routeData.Item2 ?? new List<Passenger>();
                PickupTime = routeData.Item3;
            }
            catch (Exception ex)
            {
                LogError("Error loading driver data", ex);
                throw;
            }
        }

        private void InitializeDefaultVehicle()
        {
            Vehicle = new Vehicle
            {
                UserId = userId,
                Capacity = 4,
                IsAvailableTomorrow = true,
                DriverName = username
            };
        }

        private string CalculateQueryDate(string targetTimeString)
        {
            TimeSpan timeToAdd = TimeSpan.Parse(targetTimeString);
            DateTime now = DateTime.Now;
            DateTime targetTime = new DateTime(now.Year, now.Month, now.Day,
                                     timeToAdd.Hours, timeToAdd.Minutes, timeToAdd.Seconds);

            if (targetTime < DateTime.Now)
            {
                return now.AddDays(1).ToString("yyyy-MM-dd");
            }

            return now.ToString("yyyy-MM-dd");
        }

        private void ProcessRouteData(dynamic routeData)
        {
            if (routeData.Vehicle != null)
            {
                Vehicle = routeData.Vehicle;
            }

            AssignedPassengers = routeData.Passengers ?? new List<Passenger>();
            PickupTime = routeData.PickupTime;
        }

        public async Task<bool> UpdateVehicleAvailabilityAsync(bool isAvailable)
        {
            if (Vehicle == null) return false;

            bool success = await dbService.UpdateVehicleAvailabilityAsync(Vehicle.Id, isAvailable);

            if (success)
            {
                Vehicle.IsAvailableTomorrow = isAvailable;
            }

            return success;
        }

        public async Task<bool> UpdateVehicleCapacityAsync(int capacity)
        {
            if (Vehicle == null) return false;

            bool success = await dbService.UpdateVehicleCapacityAsync(userId, capacity);

            if (success)
            {
                Vehicle.Capacity = capacity;
            }

            return success;
        }

        public async Task<bool> UpdateVehicleLocationAsync(double latitude, double longitude, string address)
        {
            try
            {
                if (Vehicle == null || Vehicle.Id == 0)
                {
                    return await CreateNewVehicle(latitude, longitude, address);
                }

                return await UpdateExistingVehicle(latitude, longitude, address);
            }
            catch (Exception ex)
            {
                LogError("Error updating vehicle location", ex);
                return false;
            }
        }

        private async Task<bool> CreateNewVehicle(double latitude, double longitude, string address)
        {
            int vehicleId = await dbService.SaveDriverVehicleAsync(
                userId, Vehicle?.Capacity ?? 4, latitude, longitude, address);

            if (vehicleId > 0)
            {
                Vehicle = await dbService.GetVehicleByUserIdAsync(userId);
                return true;
            }

            return false;
        }

        private async Task<bool> UpdateExistingVehicle(double latitude, double longitude, string address)
        {
            bool success = await dbService.UpdateVehicleLocationAsync(
                userId, latitude, longitude, address);

            if (success)
            {
                Vehicle.StartLatitude = latitude;
                Vehicle.StartLongitude = longitude;
                Vehicle.StartAddress = address;
            }

            return success;
        }

        private void LogError(string message, Exception ex)
        {
            Console.WriteLine($"{message}: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Manages UI components and interactions
    /// </summary>
    public class DriverUIManager
    {
        private readonly Form parentForm;
        private readonly DriverDataManager dataManager;
        private readonly DriverMapManager mapManager;
        private readonly DriverLocationManager locationManager;

        // UI Controls
        private Panel leftPanel;
        private CheckBox availabilityCheckBox;
        private RichTextBox routeDetailsTextBox;
        private Button refreshButton;
        private Button logoutButton;
        private NumericUpDown capacityNumericUpDown;
        private Button updateCapacityButton;
        private Label locationInstructionsLabel;
        private Button setLocationButton;
        private TextBox addressTextBox;

        public GMapControl MapControl { get; private set; }

        public DriverUIManager(Form parentForm, DriverDataManager dataManager,
            DriverMapManager mapManager, DriverLocationManager locationManager)
        {
            this.parentForm = parentForm;
            this.dataManager = dataManager;
            this.mapManager = mapManager;
            this.locationManager = locationManager;
        }

        public void InitializeUI()
        {
            SetFormProperties();
            CreateTitleLabel();
            CreateLeftPanel();
            CreateMapControl();

            locationManager.SetMapControl(MapControl);
            locationManager.SetInstructionLabel(locationInstructionsLabel);
        }

        private void SetFormProperties()
        {
            parentForm.Text = "RideMatch - Driver Interface";
            parentForm.Size = new Size(1000, 700);
            parentForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            parentForm.StartPosition = FormStartPosition.CenterScreen;
        }

        private void CreateTitleLabel()
        {
            var titleLabel = new Label
            {
                Text = $"Welcome, {dataManager.Vehicle?.DriverName ?? "Driver"}",
                Location = new Point(20, 20),
                Size = new Size(960, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 16, FontStyle.Bold)
            };

            parentForm.Controls.Add(titleLabel);
        }

        private void CreateLeftPanel()
        {
            leftPanel = new Panel
            {
                Location = new Point(20, 70),
                Size = new Size(350, 580),
                BorderStyle = BorderStyle.FixedSingle
            };

            parentForm.Controls.Add(leftPanel);

            CreateAvailabilitySection();
            CreateCapacitySection();
            CreateRouteDetailsSection();
            CreateLocationSettingSection();
            CreateActionButtons();
        }

        private void CreateAvailabilitySection()
        {
            var availabilityLabel = new Label
            {
                Text = "Tomorrow's Status:",
                Location = new Point(20, 20),
                Size = new Size(150, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            leftPanel.Controls.Add(availabilityLabel);

            availabilityCheckBox = new CheckBox
            {
                Text = "I am available to drive tomorrow",
                Location = new Point(20, 50),
                Size = new Size(300, 30),
                Checked = true
            };

            availabilityCheckBox.CheckedChanged += AvailabilityCheckBox_CheckedChanged;
            leftPanel.Controls.Add(availabilityCheckBox);
        }

        private void CreateCapacitySection()
        {
            var capacityLabel = new Label
            {
                Text = "Vehicle Capacity:",
                Location = new Point(20, 90),
                Size = new Size(150, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            leftPanel.Controls.Add(capacityLabel);

            var seatsLabel = new Label
            {
                Text = "Number of seats:",
                Location = new Point(20, 120),
                Size = new Size(150, 20)
            };

            leftPanel.Controls.Add(seatsLabel);

            capacityNumericUpDown = new NumericUpDown
            {
                Location = new Point(180, 120),
                Size = new Size(80, 25),
                Minimum = 1,
                Maximum = 20,
                Value = 4
            };

            leftPanel.Controls.Add(capacityNumericUpDown);

            updateCapacityButton = new Button
            {
                Text = "Update Capacity",
                Location = new Point(180, 150),
                Size = new Size(150, 30)
            };

            updateCapacityButton.Click += UpdateCapacityButton_Click;
            leftPanel.Controls.Add(updateCapacityButton);

            AddSeparator(190);
        }

        private void CreateRouteDetailsSection()
        {
            var routeLabel = new Label
            {
                Text = "Your Route Details:",
                Location = new Point(20, 210),
                Size = new Size(200, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            leftPanel.Controls.Add(routeLabel);

            routeDetailsTextBox = new RichTextBox
            {
                Location = new Point(20, 240),
                Size = new Size(310, 160),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle
            };

            leftPanel.Controls.Add(routeDetailsTextBox);
            routeDetailsTextBox.AppendText("Loading driver data...\n");

            AddSeparator(410);
        }

        private void CreateLocationSettingSection()
        {
            var locationLabel = new Label
            {
                Text = "Set Your Starting Location:",
                Location = new Point(20, 420),
                Size = new Size(200, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            leftPanel.Controls.Add(locationLabel);

            setLocationButton = new Button
            {
                Text = "Set Location on Map",
                Location = new Point(20, 445),
                Size = new Size(150, 30)
            };

            setLocationButton.Click += SetLocationButton_Click;
            leftPanel.Controls.Add(setLocationButton);

            var addressLabel = new Label
            {
                Text = "Or Search Address:",
                Location = new Point(20, 472),
                Size = new Size(150, 20)
            };

            leftPanel.Controls.Add(addressLabel);

            addressTextBox = new TextBox
            {
                Location = new Point(20, 500),
                Size = new Size(220, 25)
            };

            leftPanel.Controls.Add(addressTextBox);

            var searchButton = new Button
            {
                Text = "Search",
                Location = new Point(250, 500),
                Size = new Size(80, 25)
            };

            searchButton.Click += SearchButton_Click;
            leftPanel.Controls.Add(searchButton);

            locationInstructionsLabel = new Label
            {
                Text = "Click on the map to set your starting location",
                Location = new Point(20, 550),
                Size = new Size(310, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Red,
                Visible = false
            };

            leftPanel.Controls.Add(locationInstructionsLabel);
        }

        private void CreateActionButtons()
        {
            refreshButton = new Button
            {
                Text = "Refresh",
                Location = new Point(20, 530),
                Size = new Size(150, 30)
            };

            refreshButton.Click += RefreshButton_Click;
            leftPanel.Controls.Add(refreshButton);

            logoutButton = new Button
            {
                Text = "Logout",
                Location = new Point(180, 530),
                Size = new Size(150, 30)
            };

            logoutButton.Click += (s, e) => parentForm.Close();
            leftPanel.Controls.Add(logoutButton);
        }

        private void CreateMapControl()
        {
            MapControl = new GMapControl
            {
                Location = new Point(390, 70),
                Size = new Size(580, 580),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 13,
                DragButton = MouseButtons.Left
            };

            parentForm.Controls.Add(MapControl);
        }

        private void AddSeparator(int yPosition)
        {
            var separatorPanel = new Panel
            {
                Location = new Point(20, yPosition),
                Size = new Size(310, 2),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Gray
            };

            leftPanel.Controls.Add(separatorPanel);
        }

        public void RefreshUI()
        {
            if (dataManager.Vehicle != null)
            {
                availabilityCheckBox.Checked = dataManager.Vehicle.IsAvailableTomorrow;
                capacityNumericUpDown.Value = dataManager.Vehicle.Capacity;
            }

            UpdateRouteDetailsText();
        }

        private void UpdateRouteDetailsText()
        {
            routeDetailsTextBox.Clear();

            if (dataManager.Vehicle == null)
            {
                routeDetailsTextBox.AppendText("No vehicle assigned.\n");
                return;
            }

            DisplayVehicleDetails();
            DisplayPassengerDetails();
        }

        private void DisplayVehicleDetails()
        {
            routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
            routeDetailsTextBox.AppendText("Your Vehicle Details:\n");
            routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
            routeDetailsTextBox.AppendText($"Capacity: {dataManager.Vehicle.Capacity} seats\n");

            DisplayLocationInfo();

            if (!string.IsNullOrEmpty(dataManager.Vehicle.DepartureTime))
            {
                routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
                routeDetailsTextBox.AppendText($"\nDeparture Time: {dataManager.Vehicle.DepartureTime}\n\n");
                routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
            }
        }

        private void DisplayLocationInfo()
        {
            var vehicle = dataManager.Vehicle;

            if (vehicle.StartLatitude == 0 && vehicle.StartLongitude == 0)
            {
                routeDetailsTextBox.AppendText("Starting Location: Not set\n\n");
                routeDetailsTextBox.AppendText("Please set your starting location using the options below.\n");
                return;
            }

            if (!string.IsNullOrEmpty(vehicle.StartAddress))
            {
                routeDetailsTextBox.AppendText($"Starting Location: {vehicle.StartAddress}\n");
                return;
            }

            routeDetailsTextBox.AppendText(
                $"Starting Location: ({vehicle.StartLatitude:F6}, {vehicle.StartLongitude:F6})\n");
        }

        private void DisplayPassengerDetails()
        {
            var passengers = dataManager.AssignedPassengers;

            if (passengers == null || passengers.Count == 0)
            {
                routeDetailsTextBox.AppendText("\nNo passengers assigned for today's route.\n");
                return;
            }

            routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
            routeDetailsTextBox.AppendText("Assigned Passengers:\n");
            routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;

            for (int i = 0; i < passengers.Count; i++)
            {
                var passenger = passengers[i];
                if (passenger == null) continue;

                routeDetailsTextBox.AppendText($"{i + 1}. {passenger.Name}\n");
                DisplayPassengerAddress(passenger);
                DisplayPickupTime(passenger, i);
                routeDetailsTextBox.AppendText("\n");
            }
        }

        private void DisplayPassengerAddress(Passenger passenger)
        {
            if (!string.IsNullOrEmpty(passenger.Address))
            {
                routeDetailsTextBox.AppendText($"   Pick-up: {passenger.Address}\n");
                return;
            }

            routeDetailsTextBox.AppendText(
                $"   Pick-up: ({passenger.Latitude:F6}, {passenger.Longitude:F6})\n");
        }

        private void DisplayPickupTime(Passenger passenger, int index)
        {
            routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);

            if (!string.IsNullOrEmpty(passenger.EstimatedPickupTime))
            {
                routeDetailsTextBox.AppendText($"   Pick-up Time: {passenger.EstimatedPickupTime}\n");
            }
            else if (index == 0 && dataManager.PickupTime.HasValue)
            {
                routeDetailsTextBox.AppendText(
                    $"   Pick-up Time: {dataManager.PickupTime.Value.ToString("HH:mm")}\n");
            }

            routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
        }

        private async void AvailabilityCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                bool success = await dataManager.UpdateVehicleAvailabilityAsync(availabilityCheckBox.Checked);

                if (success)
                {
                    string message = availabilityCheckBox.Checked ?
                        "You are now marked as available to drive tomorrow." :
                        "You are now marked as unavailable to drive tomorrow.";

                    MessageBox.Show(message, "Availability Updated",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                MessageBox.Show("Failed to update availability. Please try again.",
                    "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                RevertAvailabilityCheckbox();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating availability: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                RevertAvailabilityCheckbox();
            }
        }

        private void RevertAvailabilityCheckbox()
        {
            availabilityCheckBox.CheckedChanged -= AvailabilityCheckBox_CheckedChanged;
            availabilityCheckBox.Checked = dataManager.Vehicle?.IsAvailableTomorrow ?? true;
            availabilityCheckBox.CheckedChanged += AvailabilityCheckBox_CheckedChanged;
        }

        private async void UpdateCapacityButton_Click(object sender, EventArgs e)
        {
            try
            {
                int newCapacity = (int)capacityNumericUpDown.Value;
                bool success = await dataManager.UpdateVehicleCapacityAsync(newCapacity);

                if (success)
                {
                    MessageBox.Show($"Vehicle capacity updated to {newCapacity} seats.",
                        "Capacity Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    UpdateRouteDetailsText();
                    return;
                }

                MessageBox.Show("Failed to update vehicle capacity. Please try again.",
                    "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                capacityNumericUpDown.Value = dataManager.Vehicle?.Capacity ?? 4;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating vehicle capacity: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                capacityNumericUpDown.Value = dataManager.Vehicle?.Capacity ?? 4;
            }
        }

        private void SetLocationButton_Click(object sender, EventArgs e)
        {
            locationManager.EnableLocationSelection();
        }

        private async void SearchButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(addressTextBox.Text)) return;

            await locationManager.SearchAddressAsync(addressTextBox.Text);
        }

        private async void RefreshButton_Click(object sender, EventArgs e)
        {
            refreshButton.Enabled = false;
            routeDetailsTextBox.Clear();
            routeDetailsTextBox.AppendText("Loading route data...\n");

            try
            {
                await dataManager.LoadDriverDataAsync();
                RefreshUI();
                mapManager.DisplayRouteOnMap(dataManager.Vehicle, dataManager.AssignedPassengers);
            }
            catch (Exception ex)
            {
                routeDetailsTextBox.Clear();
                routeDetailsTextBox.AppendText($"Error loading data: {ex.Message}\n");
            }
            finally
            {
                refreshButton.Enabled = true;
            }
        }
    }

    /// <summary>
    /// Manages map-related operations
    /// </summary>
    public class DriverMapManager
    {
        private readonly MapService mapService;
        private readonly DatabaseService dbService;
        private GMapControl mapControl;

        public DriverMapManager(MapService mapService, DatabaseService dbService)
        {
            this.mapService = mapService ?? throw new ArgumentNullException(nameof(mapService));
            this.dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
        }

        public void InitializeMap(GMapControl mapControl, double latitude, double longitude)
        {
            this.mapControl = mapControl ?? throw new ArgumentNullException(nameof(mapControl));
            mapService.InitializeGoogleMaps(this.mapControl, latitude, longitude);
        }

        public void DisplayRouteOnMap(Vehicle vehicle, List<Passenger> passengers)
        {
            if (mapControl == null || vehicle == null) return;

            try
            {
                mapControl.Overlays.Clear();

                var vehiclesOverlay = new GMapOverlay("vehicles");
                var passengersOverlay = new GMapOverlay("passengers");
                var routesOverlay = new GMapOverlay("routes");
                var destinationOverlay = new GMapOverlay("destination");

                AddVehicleToMap(vehicle, vehiclesOverlay);
                AddPassengersToMap(passengers, passengersOverlay);

                Task.Run(async () => await AddDestinationAndRouteAsync(vehicle, passengers,
                                                                      routesOverlay, destinationOverlay));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error displaying route: {ex.Message}",
                    "Map Display Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AddVehicleToMap(Vehicle vehicle, GMapOverlay vehiclesOverlay)
        {
            if (vehicle.StartLatitude == 0 && vehicle.StartLongitude == 0) return;

            var vehicleMarker = MapOverlays.CreateVehicleMarker(vehicle);
            vehiclesOverlay.Markers.Add(vehicleMarker);

            mapControl.Position = new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude);
            mapControl.Zoom = 12;

            mapControl.Overlays.Add(vehiclesOverlay);
        }

        private void AddPassengersToMap(List<Passenger> passengers, GMapOverlay passengersOverlay)
        {
            if (passengers == null || passengers.Count == 0) return;

            foreach (var passenger in passengers)
            {
                if (passenger == null) continue;

                var passengerMarker = MapOverlays.CreatePassengerMarker(passenger);
                passengersOverlay.Markers.Add(passengerMarker);
            }

            mapControl.Overlays.Add(passengersOverlay);
        }

        private async Task AddDestinationAndRouteAsync(
            Vehicle vehicle, List<Passenger> passengers,
            GMapOverlay routesOverlay, GMapOverlay destinationOverlay)
        {
            try
            {
                var destination = await dbService.GetDestinationAsync();

                mapControl.Invoke(new Action(() => {
                    try
                    {
                        // Create a marker for the destination location
                        var destMarker = MapOverlays.CreateDestinationMarker(
                            destination.Latitude, destination.Longitude);
                        destinationOverlay.Markers.Add(destMarker);
                        mapControl.Overlays.Add(destinationOverlay);

                        // Handle route creation
                        Task.Run(async () => {
                            try
                            {
                                await AddRouteToMapAsync(vehicle, passengers,
                                    destination, routesOverlay);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error adding route: {ex.Message}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error adding destination: {ex.Message}");
                    }
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting destination: {ex.Message}");
            }
        }

        private void ConvertToDestination(dynamic dbDestination)
        {
            // Removed - not needed anymore
        }

        private void AddDestinationMarker(dynamic destination, GMapOverlay destinationOverlay)
        {
            var destMarker = MapOverlays.CreateDestinationMarker(
                destination.Latitude, destination.Longitude);
            destinationOverlay.Markers.Add(destMarker);
        }

        private async Task AddRouteToMapAsync(
            Vehicle vehicle, List<Passenger> passengers,
            dynamic destination, GMapOverlay routesOverlay)
        {
            try
            {
                List<PointLatLng> routePoints = new List<PointLatLng>();

                // Add vehicle starting point
                routePoints.Add(new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude));

                // Add passenger points
                if (passengers != null)
                {
                    foreach (var passenger in passengers)
                    {
                        if (passenger != null)
                        {
                            routePoints.Add(new PointLatLng(passenger.Latitude, passenger.Longitude));
                        }
                    }
                }

                // Add destination point
                routePoints.Add(new PointLatLng(destination.Latitude, destination.Longitude));

                if (routePoints.Count < 2) return;

                // Try to get the Google directions route
                List<PointLatLng> finalRoutePoints;

                try
                {
                    finalRoutePoints = await mapService.GetGoogleDirectionsAsync(routePoints);

                    if (finalRoutePoints == null || finalRoutePoints.Count == 0)
                    {
                        finalRoutePoints = routePoints;
                    }
                }
                catch
                {
                    finalRoutePoints = routePoints;
                }

                mapControl.Invoke(new Action(() => {
                    var route = MapOverlays.CreateRoute(finalRoutePoints, "DriverRoute", Color.Blue);
                    routesOverlay.Routes.Add(route);
                    mapControl.Overlays.Add(routesOverlay);
                    mapControl.Refresh();
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding route: {ex.Message}");
            }
        }

        private List<PointLatLng> CollectRoutePoints(
            Vehicle vehicle, List<Passenger> passengers, Destination destination)
        {
            // Method removed - no longer needed
            return null;
        }
    }

    /// <summary>
    /// Model for destination location
    /// </summary>
    public class Destination
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Address { get; set; }
        public string TargetTime { get; set; }
    }

    /// <summary>
    /// Manages location operations
    /// </summary>
    public class DriverLocationManager
    {
        private readonly MapService mapService;
        private readonly DriverDataManager dataManager;
        private GMapControl mapControl;
        private Label instructionsLabel;
        private bool isSettingLocation;

        public DriverLocationManager(MapService mapService, DriverDataManager dataManager)
        {
            this.mapService = mapService;
            this.dataManager = dataManager;
        }

        public void SetMapControl(GMapControl mapControl)
        {
            this.mapControl = mapControl;
            this.mapControl.MouseClick += MapControl_MouseClick;
        }

        public void SetInstructionLabel(Label instructionsLabel)
        {
            this.instructionsLabel = instructionsLabel;
        }

        public void EnableLocationSelection()
        {
            try
            {
                isSettingLocation = true;

                if (instructionsLabel != null)
                {
                    instructionsLabel.Visible = true;
                }

                if (mapControl != null)
                {
                    mapControl.Cursor = Cursors.Hand;
                }

                MessageBox.Show("Click on the map to set your starting location",
                    "Set Location", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error enabling location selection: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                isSettingLocation = false;
            }
        }

        public async Task SearchAddressAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;

            try
            {
                mapControl.Parent.Cursor = Cursors.WaitCursor;

                var result = await mapService.GeocodeAddressAsync(address);

                if (result.HasValue)
                {
                    mapControl.Position = new PointLatLng(result.Value.Latitude, result.Value.Longitude);
                    mapControl.Zoom = 15;

                    await UpdateLocationAsync(result.Value.Latitude, result.Value.Longitude);
                }
                else
                {
                    MessageBox.Show("Address not found. Please try again.",
                        "Search Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error searching: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                mapControl.Parent.Cursor = Cursors.Default;
            }
        }

        private void MapControl_MouseClick(object sender, MouseEventArgs e)
        {
            if (!isSettingLocation) return;

            try
            {
                PointLatLng point = mapControl.FromLocalToLatLng(e.X, e.Y);

                Task.Run(async () => {
                    try
                    {
                        string address = await mapService.ReverseGeocodeAsync(point.Lat, point.Lng);

                        mapControl.Invoke(new Action(async () => {
                            await UpdateLocationAsync(point.Lat, point.Lng, address);
                            DisableLocationSelection();
                        }));
                    }
                    catch (Exception ex)
                    {
                        mapControl.Invoke(new Action(() => {
                            MessageBox.Show($"Error getting address: {ex.Message}",
                                "Geocoding Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                            DisableLocationSelection();
                        }));
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing map click: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                DisableLocationSelection();
            }
        }

        private void DisableLocationSelection()
        {
            isSettingLocation = false;

            if (instructionsLabel != null)
            {
                instructionsLabel.Visible = false;
            }

            if (mapControl != null)
            {
                mapControl.Cursor = Cursors.Default;
            }
        }

        public async Task UpdateLocationAsync(double latitude, double longitude, string address = null)
        {
            try
            {
                mapControl.Parent.Cursor = Cursors.WaitCursor;

                if (string.IsNullOrEmpty(address))
                {
                    address = await mapService.ReverseGeocodeAsync(latitude, longitude);
                }

                bool success = await dataManager.UpdateVehicleLocationAsync(latitude, longitude, address);

                if (success)
                {
                    MessageBox.Show($"Your starting location has been set to:\n{address}",
                        "Location Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Failed to update location. Please try again.",
                        "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating location: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                mapControl.Parent.Cursor = Cursors.Default;
            }
        }
    }
}