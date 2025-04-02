using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using claudpro.Models;
using claudpro.Services;
using claudpro.UI;

namespace claudpro
{
    public partial class AdminForm : Form
    {
        private readonly DatabaseService dbService;
        private readonly MapService mapService;
        private readonly RoutingService routingService;
        private readonly RideSharingGenetic algorithmService;

        private TabControl tabControl;
        private TabPage usersTab;
        private TabPage vehiclesTab;
        private TabPage passengersTab;
        private TabPage routesTab;
        private TabPage destinationTab;

        // Lists to store data
        private List<(int Id, string Username, string UserType, string Name)> users;
        private List<Vehicle> vehicles;
        private List<Passenger> passengers;
        private Solution currentSolution;

        // Destination information
        private double destinationLat;
        private double destinationLng;
        private string destinationName;
        private string destinationAddress;
        private string destinationTargetTime;

        public AdminForm(DatabaseService dbService, MapService mapService)
        {
            this.dbService = dbService;
            this.mapService = mapService;

            InitializeComponent();
            SetupUI();

            // Load destination
            Task.Run(async () => {
                var dest = await dbService.GetDestinationAsync();
                destinationLat = dest.Latitude;
                destinationLng = dest.Longitude;
                destinationName = dest.Name;
                destinationAddress = dest.Address;
                destinationTargetTime = dest.TargetTime;

                // Initialize routing and algorithm services
                routingService = new RoutingService(mapService, destinationLat, destinationLng);

                // Load data on form load
                await LoadAllDataAsync();
            });
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "RideMatch - Administrator Interface";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;

            this.ResumeLayout(false);
        }

        private void SetupUI()
        {
            tabControl = new TabControl
            {
                Location = new Point(10, 10),
                Size = new Size(1170, 740),
                Dock = DockStyle.Fill
            };
            Controls.Add(tabControl);

            // Create tabs
            usersTab = new TabPage("Users");
            vehiclesTab = new TabPage("Vehicles");
            passengersTab = new TabPage("Passengers");
            routesTab = new TabPage("Routes");
            destinationTab = new TabPage("Destination");

            // Add tabs to tab control
            tabControl.TabPages.Add(usersTab);
            tabControl.TabPages.Add(vehiclesTab);
            tabControl.TabPages.Add(passengersTab);
            tabControl.TabPages.Add(routesTab);
            tabControl.TabPages.Add(destinationTab);

            // Setup each tab
            SetupUsersTab();
            SetupVehiclesTab();
            SetupPassengersTab();
            SetupRoutesTab();
            SetupDestinationTab();
        }

        #region Tab Setup

        private void SetupUsersTab()
        {
            // Users ListView
            var usersListView = new ListView
            {
                Location = new Point(10, 50),
                Size = new Size(850, 500),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            usersListView.Columns.Add("ID", 50);
            usersListView.Columns.Add("Username", 150);
            usersListView.Columns.Add("User Type", 100);
            usersListView.Columns.Add("Name", 200);
            usersListView.Columns.Add("Email", 200);
            usersListView.Columns.Add("Phone", 150);
            usersTab.Controls.Add(usersListView);

            // Refresh button
            var refreshButton = ControlExtensions.CreateButton(
                "Refresh Users",
                new Point(10, 10),
                new Size(120, 30),
                async (s, e) => {
                    await LoadUsersAsync();
                    await DisplayUsersAsync(usersListView);
                }
            );
            usersTab.Controls.Add(refreshButton);

            // Add User button
            var addUserButton = ControlExtensions.CreateButton(
                "Add User",
                new Point(140, 10),
                new Size(120, 30),
                (s, e) => {
                    using (var regForm = new RegistrationForm(dbService))
                    {
                        if (regForm.ShowDialog() == DialogResult.OK)
                        {
                            LoadUsersAsync().ContinueWith(_ => {
                                this.Invoke(new Action(async () => {
                                    await DisplayUsersAsync(usersListView);
                                }));
                            });
                        }
                    }
                }
            );
            usersTab.Controls.Add(addUserButton);
        }

        private void SetupVehiclesTab()
        {
            // Vehicles ListView
            var vehiclesListView = new ListView
            {
                Location = new Point(10, 50),
                Size = new Size(850, 500),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            vehiclesListView.Columns.Add("ID", 50);
            vehiclesListView.Columns.Add("Driver", 150);
            vehiclesListView.Columns.Add("Capacity", 80);
            vehiclesListView.Columns.Add("Start Location", 250);
            vehiclesListView.Columns.Add("Available Tomorrow", 120);
            vehiclesTab.Controls.Add(vehiclesListView);

            // Refresh button
            var refreshButton = ControlExtensions.CreateButton(
                "Refresh Vehicles",
                new Point(10, 10),
                new Size(120, 30),
                async (s, e) => {
                    await LoadVehiclesAsync();
                    await DisplayVehiclesAsync(vehiclesListView);
                }
            );
            vehiclesTab.Controls.Add(refreshButton);

            // Add Vehicle panel
            var addVehiclePanel = ControlExtensions.CreatePanel(
                new Point(870, 50),
                new Size(280, 500),
                BorderStyle.FixedSingle
            );
            vehiclesTab.Controls.Add(addVehiclePanel);

            // Title for add panel
            addVehiclePanel.Controls.Add(ControlExtensions.CreateLabel(
                "Add New Vehicle",
                new Point(10, 10),
                new Size(260, 25),
                new Font("Arial", 12, FontStyle.Bold),
                ContentAlignment.MiddleCenter
            ));

            // Form fields
            var y = 50;

            // Driver selection
            addVehiclePanel.Controls.Add(ControlExtensions.CreateLabel("Driver (User):", new Point(10, y), new Size(100, 20)));
            var driverComboBox = new ComboBox
            {
                Location = new Point(115, y),
                Size = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            addVehiclePanel.Controls.Add(driverComboBox);
            y += 40;

            // Capacity
            addVehiclePanel.Controls.Add(ControlExtensions.CreateLabel("Capacity:", new Point(10, y), new Size(100, 20)));
            var capacityNumeric = new NumericUpDown
            {
                Location = new Point(115, y),
                Size = new Size(150, 25),
                Minimum = 1,
                Maximum = 20,
                Value = 4
            };
            addVehiclePanel.Controls.Add(capacityNumeric);
            y += 40;

            // Latitude
            addVehiclePanel.Controls.Add(ControlExtensions.CreateLabel("Latitude:", new Point(10, y), new Size(100, 20)));
            var latTextBox = ControlExtensions.CreateTextBox(new Point(115, y), new Size(150, 20));
            addVehiclePanel.Controls.Add(latTextBox);
            y += 40;

            // Longitude
            addVehiclePanel.Controls.Add(ControlExtensions.CreateLabel("Longitude:", new Point(10, y), new Size(100, 20)));
            var lngTextBox = ControlExtensions.CreateTextBox(new Point(115, y), new Size(150, 20));
            addVehiclePanel.Controls.Add(lngTextBox);
            y += 40;

            // Address
            addVehiclePanel.Controls.Add(ControlExtensions.CreateLabel("Address:", new Point(10, y), new Size(100, 20)));
            var addressTextBox = ControlExtensions.CreateTextBox(new Point(115, y), new Size(150, 20));
            addVehiclePanel.Controls.Add(addressTextBox);
            y += 40;

            // Address search button
            var searchAddressButton = ControlExtensions.CreateButton(
                "Search Address",
                new Point(115, y),
                new Size(150, 30),
                async (s, e) => {
                    if (string.IsNullOrWhiteSpace(addressTextBox.Text))
                    {
                        MessageBox.Show("Please enter an address to search", "Invalid Address", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        var result = await mapService.GeocodeAddressAsync(addressTextBox.Text);
                        if (result.HasValue)
                        {
                            latTextBox.Text = result.Value.Latitude.ToString();
                            lngTextBox.Text = result.Value.Longitude.ToString();
                        }
                        else
                        {
                            MessageBox.Show("Could not find the address", "Search Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error searching for address: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            );
            addVehiclePanel.Controls.Add(searchAddressButton);
            y += 60;

            // Add button
            var addButton = ControlExtensions.CreateButton(
                "Add Vehicle",
                new Point(115, y),
                new Size(150, 30),
                async (s, e) => {
                    try
                    {
                        // Validate inputs
                        if (driverComboBox.SelectedItem == null)
                        {
                            MessageBox.Show("Please select a driver", "Missing Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        if (string.IsNullOrWhiteSpace(latTextBox.Text) || string.IsNullOrWhiteSpace(lngTextBox.Text))
                        {
                            MessageBox.Show("Please enter coordinates or use address search", "Missing Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        // Parse inputs
                        int userId = (int)driverComboBox.SelectedValue;
                        int capacity = (int)capacityNumeric.Value;
                        double lat = double.Parse(latTextBox.Text);
                        double lng = double.Parse(lngTextBox.Text);
                        string address = addressTextBox.Text;

                        // Add vehicle to database
                        int vehicleId = await dbService.AddVehicleAsync(userId, capacity, lat, lng, address);

                        if (vehicleId > 0)
                        {
                            MessageBox.Show("Vehicle added successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                            // Clear form
                            driverComboBox.SelectedIndex = -1;
                            capacityNumeric.Value = 4;
                            latTextBox.Text = "";
                            lngTextBox.Text = "";
                            addressTextBox.Text = "";

                            // Refresh list
                            await LoadVehiclesAsync();
                            await DisplayVehiclesAsync(vehiclesListView);
                        }
                        else
                        {
                            MessageBox.Show("Failed to add vehicle", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error adding vehicle: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            );
            addVehiclePanel.Controls.Add(addButton);

            // Load drivers into combo box when users are loaded
            LoadUsersAsync().ContinueWith(_ => {
                this.Invoke(new Action(() => {
                    driverComboBox.DataSource = null;
                    driverComboBox.DisplayMember = "Name";
                    driverComboBox.ValueMember = "Id";

                    // Filter for driver users
                    var drivers = users?.Where(u => u.UserType.ToLower() == "driver").ToList();
                    driverComboBox.DataSource = drivers;
                }));
            });
        }

        private void SetupPassengersTab()
        {
            // Passengers ListView
            var passengersListView = new ListView
            {
                Location = new Point(10, 50),
                Size = new Size(850, 500),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            passengersListView.Columns.Add("ID", 50);
            passengersListView.Columns.Add("Name", 150);
            passengersListView.Columns.Add("Location", 250);
            passengersListView.Columns.Add("Available Tomorrow", 120);
            passengersTab.Controls.Add(passengersListView);

            // Refresh button
            var refreshButton = ControlExtensions.CreateButton(
                "Refresh Passengers",
                new Point(10, 10),
                new Size(120, 30),
                async (s, e) => {
                    await LoadPassengersAsync();
                    await DisplayPassengersAsync(passengersListView);
                }
            );
            passengersTab.Controls.Add(refreshButton);

            // Add Passenger panel
            var addPassengerPanel = ControlExtensions.CreatePanel(
                new Point(870, 50),
                new Size(280, 500),
                BorderStyle.FixedSingle
            );
            passengersTab.Controls.Add(addPassengerPanel);

            // Title for add panel
            addPassengerPanel.Controls.Add(ControlExtensions.CreateLabel(
                "Add New Passenger",
                new Point(10, 10),
                new Size(260, 25),
                new Font("Arial", 12, FontStyle.Bold),
                ContentAlignment.MiddleCenter
            ));

            // Form fields
            var y = 50;

            // User selection
            addPassengerPanel.Controls.Add(ControlExtensions.CreateLabel("User:", new Point(10, y), new Size(100, 20)));
            var userComboBox = new ComboBox
            {
                Location = new Point(115, y),
                Size = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            addPassengerPanel.Controls.Add(userComboBox);
            y += 40;

            // Name
            addPassengerPanel.Controls.Add(ControlExtensions.CreateLabel("Name:", new Point(10, y), new Size(100, 20)));
            var nameTextBox = ControlExtensions.CreateTextBox(new Point(115, y), new Size(150, 20));
            addPassengerPanel.Controls.Add(nameTextBox);
            y += 40;

            // Latitude
            addPassengerPanel.Controls.Add(ControlExtensions.CreateLabel("Latitude:", new Point(10, y), new Size(100, 20)));
            var latTextBox = ControlExtensions.CreateTextBox(new Point(115, y), new Size(150, 20));
            addPassengerPanel.Controls.Add(latTextBox);
            y += 40;

            // Longitude
            addPassengerPanel.Controls.Add(ControlExtensions.CreateLabel("Longitude:", new Point(10, y), new Size(100, 20)));
            var lngTextBox = ControlExtensions.CreateTextBox(new Point(115, y), new Size(150, 20));
            addPassengerPanel.Controls.Add(lngTextBox);
            y += 40;

            // Address
            addPassengerPanel.Controls.Add(ControlExtensions.CreateLabel("Address:", new Point(10, y), new Size(100, 20)));
            var addressTextBox = ControlExtensions.CreateTextBox(new Point(115, y), new Size(150, 20));
            addPassengerPanel.Controls.Add(addressTextBox);
            y += 40;

            // Address search button
            var searchAddressButton = ControlExtensions.CreateButton(
                "Search Address",
                new Point(115, y),
                new Size(150, 30),
                async (s, e) => {
                    if (string.IsNullOrWhiteSpace(addressTextBox.Text))
                    {
                        MessageBox.Show("Please enter an address to search", "Invalid Address", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        var result = await mapService.GeocodeAddressAsync(addressTextBox.Text);
                        if (result.HasValue)
                        {
                            latTextBox.Text = result.Value.Latitude.ToString();
                            lngTextBox.Text = result.Value.Longitude.ToString();
                        }
                        else
                        {
                            MessageBox.Show("Could not find the address", "Search Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error searching for address: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            );
            addPassengerPanel.Controls.Add(searchAddressButton);
            y += 60;

            // Add button
            var addButton = ControlExtensions.CreateButton(
                "Add Passenger",
                new Point(115, y),
                new Size(150, 30),
                async (s, e) => {
                    try
                    {
                        // Validate inputs
                        if (userComboBox.SelectedItem == null)
                        {
                            MessageBox.Show("Please select a user", "Missing Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        if (string.IsNullOrWhiteSpace(nameTextBox.Text))
                        {
                            MessageBox.Show("Please enter a name", "Missing Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        if (string.IsNullOrWhiteSpace(latTextBox.Text) || string.IsNullOrWhiteSpace(lngTextBox.Text))
                        {
                            MessageBox.Show("Please enter coordinates or use address search", "Missing Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        // Parse inputs
                        int userId = (int)userComboBox.SelectedValue;
                        string name = nameTextBox.Text;
                        double lat = double.Parse(latTextBox.Text);
                        double lng = double.Parse(lngTextBox.Text);
                        string address = addressTextBox.Text;

                        // Add passenger to database
                        int passengerId = await dbService.AddPassengerAsync(userId, name, lat, lng, address);

                        if (passengerId > 0)
                        {
                            MessageBox.Show("Passenger added successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                            // Clear form
                            userComboBox.SelectedIndex = -1;
                            nameTextBox.Text = "";
                            latTextBox.Text = "";
                            lngTextBox.Text = "";
                            addressTextBox.Text = "";

                            // Refresh list
                            await LoadPassengersAsync();
                            await DisplayPassengersAsync(passengersListView);
                        }
                        else
                        {
                            MessageBox.Show("Failed to add passenger", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error adding passenger: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            );
            addPassengerPanel.Controls.Add(addButton);

            // Load users into combo box when users are loaded
            LoadUsersAsync().ContinueWith(_ => {
                this.Invoke(new Action(() => {
                    userComboBox.DataSource = null;
                    userComboBox.DisplayMember = "Name";
                    userComboBox.ValueMember = "Id";

                    // Filter for passenger users
                    var passengers = users?.Where(u => u.UserType.ToLower() == "passenger").ToList();
                    userComboBox.DataSource = passengers;
                }));
            });
        }

        private void SetupRoutesTab()
        {
            // Routes panel
            var routesPanel = ControlExtensions.CreatePanel(
                new Point(10, 50),
                new Size(1100, 600),
                BorderStyle.FixedSingle
            );
            routesTab.Controls.Add(routesPanel);

            // Map control
            var gMapControl = new GMap.NET.WindowsForms.GMapControl
            {
                Location = new Point(10, 10),
                Size = new Size(700, 580),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 13,
                DragButton = MouseButtons.Left
            };
            routesPanel.Controls.Add(gMapControl);
            mapService.InitializeGoogleMaps(gMapControl);

            // Control panel
            var controlPanel = ControlExtensions.CreatePanel(
                new Point(720, 10),
                new Size(370, 580),
                BorderStyle.FixedSingle
            );
            routesPanel.Controls.Add(controlPanel);

            // Control panel title
            controlPanel.Controls.Add(ControlExtensions.CreateLabel(
                "Route Generation Controls",
                new Point(10, 10),
                new Size(350, 25),
                new Font("Arial", 12, FontStyle.Bold),
                ContentAlignment.MiddleCenter
            ));

            var y = 50;

            // Date selection
            controlPanel.Controls.Add(ControlExtensions.CreateLabel("Date:", new Point(10, y), new Size(100, 20)));
            var dateTimePicker = new DateTimePicker
            {
                Location = new Point(120, y),
                Size = new Size(240, 20),
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now
            };
            controlPanel.Controls.Add(dateTimePicker);
            y += 40;

            // Algorithm parameters
            controlPanel.Controls.Add(ControlExtensions.CreateLabel("Population Size:", new Point(10, y), new Size(100, 20)));
            var populationSizeNumeric = new NumericUpDown
            {
                Location = new Point(120, y),
                Size = new Size(100, 25),
                Minimum = 50,
                Maximum = 500,
                Value = 200,
                Increment = 50
            };
            controlPanel.Controls.Add(populationSizeNumeric);
            y += 40;

            controlPanel.Controls.Add(ControlExtensions.CreateLabel("Generations:", new Point(10, y), new Size(100, 20)));
            var generationsNumeric = new NumericUpDown
            {
                Location = new Point(120, y),
                Size = new Size(100, 25),
                Minimum = 50,
                Maximum = 500,
                Value = 150,
                Increment = 50
            };
            controlPanel.Controls.Add(generationsNumeric);
            y += 40;

            // Load available vehicles and passengers
            var loadButton = ControlExtensions.CreateButton(
                "Load Available Data",
                new Point(10, y),
                new Size(350, 30),
                async (s, e) => {
                    try
                    {
                        await LoadVehiclesAsync();
                        await LoadPassengersAsync();

                        // Display on map
                        DisplayDataOnMap(gMapControl, vehicles, passengers);

                        // Update summary
                        DisplaySummary(controlPanel);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            );
            controlPanel.Controls.Add(loadButton);
            y += 50;

            // Run algorithm button
            var runAlgorithmButton = ControlExtensions.CreateButton(
                "Generate Routes",
                new Point(10, y),
                new Size(350, 30),
                async (s, e) => {
                    try
                    {
                        // Make sure data is loaded
                        if (vehicles == null || vehicles.Count == 0 || passengers == null || passengers.Count == 0)
                        {
                            MessageBox.Show("Please load available data first", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        int populationSize = (int)populationSizeNumeric.Value;
                        int generations = (int)generationsNumeric.Value;

                        // Create algorithm service
                        algorithmService = new RideSharingGenetic(
                            passengers,
                            vehicles,
                            populationSize,
                            destinationLat,
                            destinationLng,
                            30 // Target time in minutes - could be configurable
                        );

                        // Run algorithm
                        currentSolution = algorithmService.Solve(generations);

                        // Display solution on map
                        routingService.DisplaySolutionOnMap(gMapControl, currentSolution);

                        // Update summary
                        DisplaySolutionSummary(controlPanel, currentSolution);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error generating routes: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            );
            controlPanel.Controls.Add(runAlgorithmButton);
            y += 50;

            // Save solution button
            var saveSolutionButton = ControlExtensions.CreateButton(
                "Save Solution",
                new Point(10, y),
                new Size(350, 30),
                async (s, e) => {
                    try
                    {
                        if (currentSolution == null)
                        {
                            MessageBox.Show("Please generate routes first", "No Solution", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        // Get selected date in format yyyy-MM-dd
                        string date = dateTimePicker.Value.ToString("yyyy-MM-dd");

                        // Save solution to database
                        int routeId = await dbService.SaveSolutionAsync(currentSolution, date);

                        if (routeId > 0)
                        {
                            MessageBox.Show($"Solution saved successfully for {date}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show("Failed to save solution", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving solution: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            );
            controlPanel.Controls.Add(saveSolutionButton);
            y += 40;

            // Results section
            var resultsLabel = ControlExtensions.CreateLabel(
                "Results Summary",
                new Point(10, y),
                new Size(350, 25),
                new Font("Arial", 10, FontStyle.Bold),
                ContentAlignment.MiddleCenter
            );
            resultsLabel.Name = "resultsLabel";
            controlPanel.Controls.Add(resultsLabel);
            y += 30;

            var resultsTextBox = ControlExtensions.CreateTextBox(
                new Point(10, y),
                new Size(350, 200),
                "",
                true,
                true
            );
            resultsTextBox.Name = "resultsTextBox";
            controlPanel.Controls.Add(resultsTextBox);
        }

        private void SetupDestinationTab()
        {
            // Destination panel
            var destinationPanel = ControlExtensions.CreatePanel(
                new Point(10, 50),
                new Size(1100, 600),
                BorderStyle.FixedSingle
            );
            destinationTab.Controls.Add(destinationPanel);

            // Map control for destination
            var destMapControl = new GMap.NET.WindowsForms.GMapControl
            {
                Location = new Point(10, 10),
                Size = new Size(700, 580),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 13,
                DragButton = MouseButtons.Left
            };
            destinationPanel.Controls.Add(destMapControl);
            mapService.InitializeGoogleMaps(destMapControl);

            // Settings panel
            var settingsPanel = ControlExtensions.CreatePanel(
                new Point(720, 10),
                new Size(370, 580),
                BorderStyle.FixedSingle
            );
            destinationPanel.Controls.Add(settingsPanel);

            // Settings title
            settingsPanel.Controls.Add(ControlExtensions.CreateLabel(
                "Destination Settings",
                new Point(10, 10),
                new Size(350, 25),
                new Font("Arial", 12, FontStyle.Bold),
                ContentAlignment.MiddleCenter
            ));

            // Load current destination
            Task.Run(async () => {
                var destination = await dbService.GetDestinationAsync();
                this.Invoke(new Action(() => {
                    destMapControl.Position = new GMap.NET.PointLatLng(destination.Latitude, destination.Longitude);

                    // Add destination marker
                    var overlay = new GMap.NET.WindowsForms.GMapOverlay("destination");
                    var marker = MapOverlays.CreateDestinationMarker(destination.Latitude, destination.Longitude);
                    overlay.Markers.Add(marker);
                    destMapControl.Overlays.Add(overlay);

                    // Fill form fields
                    var nameTextBox = (TextBox)settingsPanel.Controls.Find("destNameTextBox", true).FirstOrDefault();
                    var latTextBox = (TextBox)settingsPanel.Controls.Find("destLatTextBox", true).FirstOrDefault();
                    var lngTextBox = (TextBox)settingsPanel.Controls.Find("destLngTextBox", true).FirstOrDefault();
                    var addressTextBox = (TextBox)settingsPanel.Controls.Find("destAddressTextBox", true).FirstOrDefault();
                    var timeTextBox = (TextBox)settingsPanel.Controls.Find("targetTimeTextBox", true).FirstOrDefault();

                    if (nameTextBox != null) nameTextBox.Text = destination.Name;
                    if (latTextBox != null) latTextBox.Text = destination.Latitude.ToString();
                    if (lngTextBox != null) lngTextBox.Text = destination.Longitude.ToString();
                    if (addressTextBox != null) addressTextBox.Text = destination.Address;
                    if (timeTextBox != null) timeTextBox.Text = destination.TargetTime;
                }));
            });

            // Setup form fields
            var y = 50;

            // Name
            settingsPanel.Controls.Add(ControlExtensions.CreateLabel("Name:", new Point(10, y), new Size(100, 20)));
            var destNameTextBox = ControlExtensions.CreateTextBox(new Point(115, y), new Size(240, 20));
            destNameTextBox.Name = "destNameTextBox";
            settingsPanel.Controls.Add(destNameTextBox);
            y += 40;

            // Latitude
            settingsPanel.Controls.Add(ControlExtensions.CreateLabel("Latitude:", new Point(10, y), new Size(100, 20)));
            var destLatTextBox = ControlExtensions.CreateTextBox(new Point(115, y), new Size(240, 20));
            destLatTextBox.Name = "destLatTextBox";
            settingsPanel.Controls.Add(destLatTextBox);
            y += 40;

            // Longitude
            settingsPanel.Controls.Add(ControlExtensions.CreateLabel("Longitude:", new Point(10, y), new Size(100, 20)));
            var destLngTextBox = ControlExtensions.CreateTextBox(new Point(115, y), new Size(240, 20));
            destLngTextBox.Name = "destLngTextBox";
            settingsPanel.Controls.Add(destLngTextBox);
            y += 40;

            // Address
            settingsPanel.Controls.Add(ControlExtensions.CreateLabel("Address:", new Point(10, y), new Size(100, 20)));
            var destAddressTextBox = ControlExtensions.CreateTextBox(new Point(115, y), new Size(240, 20));
            destAddressTextBox.Name = "destAddressTextBox";
            settingsPanel.Controls.Add(destAddressTextBox);
            y += 40;

            // Target arrival time
            settingsPanel.Controls.Add(ControlExtensions.CreateLabel("Target Time:", new Point(10, y), new Size(100, 20)));
            var targetTimeTextBox = ControlExtensions.CreateTextBox(new Point(115, y), new Size(240, 20));
            targetTimeTextBox.Name = "targetTimeTextBox";
            targetTimeTextBox.Text = "08:00:00";
            settingsPanel.Controls.Add(targetTimeTextBox);
            y += 40;

            // Search address button
            var searchAddressButton = ControlExtensions.CreateButton(
                "Search Address",
                new Point(115, y),
                new Size(240, 30),
                async (s, e) => {
                    if (string.IsNullOrWhiteSpace(destAddressTextBox.Text))
                    {
                        MessageBox.Show("Please enter an address to search", "Invalid Address", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        var result = await mapService.GeocodeAddressAsync(destAddressTextBox.Text);
                        if (result.HasValue)
                        {
                            destLatTextBox.Text = result.Value.Latitude.ToString();
                            destLngTextBox.Text = result.Value.Longitude.ToString();

                            // Update map
                            destMapControl.Position = new GMap.NET.PointLatLng(result.Value.Latitude, result.Value.Longitude);

                            // Update marker
                            if (destMapControl.Overlays.Count > 0)
                            {
                                var overlay = destMapControl.Overlays[0];
                                overlay.Markers.Clear();

                                var marker = MapOverlays.CreateDestinationMarker(result.Value.Latitude, result.Value.Longitude);
                                overlay.Markers.Add(marker);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Could not find the address", "Search Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error searching for address: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            );
            settingsPanel.Controls.Add(searchAddressButton);
            y += 60;

            // Save button
            var saveButton = ControlExtensions.CreateButton(
                "Save Destination",
                new Point(115, y),
                new Size(240, 30),
                async (s, e) => {
                    try
                    {
                        // Validate inputs
                        if (string.IsNullOrWhiteSpace(destNameTextBox.Text))
                        {
                            MessageBox.Show("Please enter a name for the destination", "Missing Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        if (string.IsNullOrWhiteSpace(destLatTextBox.Text) || string.IsNullOrWhiteSpace(destLngTextBox.Text))
                        {
                            MessageBox.Show("Please enter coordinates or use address search", "Missing Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        if (string.IsNullOrWhiteSpace(targetTimeTextBox.Text))
                        {
                            MessageBox.Show("Please enter a target arrival time", "Missing Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        // Parse inputs
                        string name = destNameTextBox.Text;
                        double lat = double.Parse(destLatTextBox.Text);
                        double lng = double.Parse(destLngTextBox.Text);
                        string address = destAddressTextBox.Text;
                        string targetTime = targetTimeTextBox.Text;

                        // Update destination in database
                        bool success = await dbService.UpdateDestinationAsync(name, lat, lng, targetTime, address);

                        if (success)
                        {
                            MessageBox.Show("Destination updated successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                            // Update global variables
                            destinationLat = lat;
                            destinationLng = lng;
                            destinationName = name;
                            destinationAddress = address;
                            destinationTargetTime = targetTime;

                            // Recreate routing service with new destination
                            routingService = new RoutingService(mapService, destinationLat, destinationLng);
                        }
                        else
                        {
                            MessageBox.Show("Failed to update destination", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error updating destination: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            );
            settingsPanel.Controls.Add(saveButton);
        }

        #endregion

        #region Data Loading and Display

        private async Task LoadAllDataAsync()
        {
            await LoadUsersAsync();
            await LoadVehiclesAsync();
            await LoadPassengersAsync();
        }

        private async Task LoadUsersAsync()
        {
            // This is a simplified implementation since we don't have the actual method in DatabaseService
            // In a real implementation, you would call the appropriate method from dbService

            // For demo purposes, we'll create some sample users
            users = new List<(int Id, string Username, string UserType, string Name)>
            {
                (1, "admin", "Admin", "Administrator"),
                (2, "driver1", "Driver", "John Driver"),
                (3, "passenger1", "Passenger", "Alice Passenger")
            };

            // In a real implementation, you would do something like:
            // users = await dbService.GetAllUsersAsync();
        }

        private async Task LoadVehiclesAsync()
        {
            // In a real implementation, this would be:
            vehicles = await dbService.GetAvailableVehiclesAsync();
        }

        private async Task LoadPassengersAsync()
        {
            // In a real implementation, this would be:
            passengers = await dbService.GetAvailablePassengersAsync();
        }

        private async Task DisplayUsersAsync(ListView listView)
        {
            listView.Items.Clear();

            if (users == null)
                return;

            foreach (var user in users)
            {
                var item = new ListViewItem(user.Id.ToString());
                item.SubItems.Add(user.Username);
                item.SubItems.Add(user.UserType);
                item.SubItems.Add(user.Name);

                // In a real implementation, these would come from the user object
                item.SubItems.Add(""); // Email
                item.SubItems.Add(""); // Phone

                listView.Items.Add(item);
            }
        }

        private async Task DisplayVehiclesAsync(ListView listView)
        {
            listView.Items.Clear();

            if (vehicles == null)
                return;

            foreach (var vehicle in vehicles)
            {
                var item = new ListViewItem(vehicle.Id.ToString());
                item.SubItems.Add(vehicle.DriverName ?? $"Vehicle {vehicle.Id}");
                item.SubItems.Add(vehicle.Capacity.ToString());

                string location = !string.IsNullOrEmpty(vehicle.StartAddress)
                    ? vehicle.StartAddress
                    : $"({vehicle.StartLatitude:F4}, {vehicle.StartLongitude:F4})";
                item.SubItems.Add(location);

                item.SubItems.Add(vehicle.IsAvailableTomorrow ? "Yes" : "No");

                listView.Items.Add(item);
            }
        }

        private async Task DisplayPassengersAsync(ListView listView)
        {
            listView.Items.Clear();

            if (passengers == null)
                return;

            foreach (var passenger in passengers)
            {
                var item = new ListViewItem(passenger.Id.ToString());
                item.SubItems.Add(passenger.Name);

                string location = !string.IsNullOrEmpty(passenger.Address)
                    ? passenger.Address
                    : $"({passenger.Latitude:F4}, {passenger.Longitude:F4})";
                item.SubItems.Add(location);

                item.SubItems.Add(passenger.IsAvailableTomorrow ? "Yes" : "No");

                listView.Items.Add(item);
            }
        }

        private void DisplayDataOnMap(GMap.NET.WindowsForms.GMapControl mapControl, List<Vehicle> vehicles, List<Passenger> passengers)
        {
            mapControl.Overlays.Clear();

            // Create overlays
            var vehiclesOverlay = new GMap.NET.WindowsForms.GMapOverlay("vehicles");
            var passengersOverlay = new GMap.NET.WindowsForms.GMapOverlay("passengers");
            var destinationOverlay = new GMap.NET.WindowsForms.GMapOverlay("destination");

            // Add destination marker
            var destinationMarker = MapOverlays.CreateDestinationMarker(destinationLat, destinationLng);
            destinationOverlay.Markers.Add(destinationMarker);

            // Add vehicle markers
            if (vehicles != null)
            {
                foreach (var vehicle in vehicles)
                {
                    var marker = MapOverlays.CreateVehicleMarker(vehicle);
                    vehiclesOverlay.Markers.Add(marker);
                }
            }

            // Add passenger markers
            if (passengers != null)
            {
                foreach (var passenger in passengers)
                {
                    var marker = MapOverlays.CreatePassengerMarker(passenger);
                    passengersOverlay.Markers.Add(marker);
                }
            }

            // Add overlays to map
            mapControl.Overlays.Add(vehiclesOverlay);
            mapControl.Overlays.Add(passengersOverlay);
            mapControl.Overlays.Add(destinationOverlay);

            // Center map - use destination as default
            mapControl.Position = new GMap.NET.PointLatLng(destinationLat, destinationLng);
            mapControl.Zoom = 13;
        }

        private void DisplaySummary(Panel panel)
        {
            var resultsTextBox = (TextBox)panel.Controls.Find("resultsTextBox", true).FirstOrDefault();
            if (resultsTextBox == null)
                return;

            resultsTextBox.Clear();
            resultsTextBox.AppendText("Current Data Summary:\n\n");

            if (vehicles != null)
            {
                resultsTextBox.AppendText($"Available Vehicles: {vehicles.Count}\n");
                resultsTextBox.AppendText($"Total Capacity: {vehicles.Sum(v => v.Capacity)}\n");
            }

            if (passengers != null)
            {
                resultsTextBox.AppendText($"Available Passengers: {passengers.Count}\n");
            }

            resultsTextBox.AppendText("\nReady to generate routes.");
        }

        private void DisplaySolutionSummary(Panel panel, Solution solution)
        {
            var resultsTextBox = (TextBox)panel.Controls.Find("resultsTextBox", true).FirstOrDefault();
            if (resultsTextBox == null || solution == null)
                return;

            resultsTextBox.Clear();
            resultsTextBox.AppendText("Solution Summary:\n\n");

            int assignedCount = solution.Vehicles.Sum(v => v.AssignedPassengers.Count);
            int totalPassengers = passengers?.Count ?? 0;

            resultsTextBox.AppendText($"Assigned Passengers: {assignedCount}/{totalPassengers}\n");
            resultsTextBox.AppendText($"Used Vehicles: {solution.Vehicles.Count(v => v.AssignedPassengers.Count > 0)}/{solution.Vehicles.Count}\n");
            resultsTextBox.AppendText($"Total Distance: {solution.Vehicles.Sum(v => v.TotalDistance):F2} km\n");
            resultsTextBox.AppendText($"Total Travel Time: {solution.Vehicles.Sum(v => v.TotalTime):F2} minutes\n");

            // Check if any passengers were not assigned
            if (assignedCount < totalPassengers)
            {
                resultsTextBox.AppendText("\nWARNING: Not all passengers were assigned!\n");
                resultsTextBox.AppendText("Consider adding more vehicles or adjusting parameters.\n");
            }

            resultsTextBox.AppendText("\nSolution ready to save.");
        }

        #endregion
    }
}