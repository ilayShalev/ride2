using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using claudpro.Models;
using claudpro.Services;
using claudpro.UI;
using claudpro.Utilities;

namespace claudpro
{
    public partial class MainForm : Form
    {
        // Configuration
        private string apiKey = ConfigurationManager.AppSettings["GoogleApiKey"];

        // Services
        private MapService mapService;
        private  RoutingService routingService;

        // Data
        private List<Passenger> passengers = new List<Passenger>();
        private List<Vehicle> vehicles = new List<Vehicle>();
        private Solution bestSolution;
        private List<Solution> evaluatedPopulation = new List<Solution>();

        // State
        private double destinationLat = 32.0741; //TEL - AVIV   
        private double destinationLng = 34.7922;
        private int targetTime = 30;
        private Random rnd = new Random();
        private bool isAddingManually = false;
        private bool isAddingPassenger = true;

        // UI Controls
        private TextBox logTextBox;
        private GMapControl gMapControl;
        private Panel routeDetailsPanel;
        private RichTextBox routeDetailsTextBox;

        // Control fields
        private TextBox latTextBox, lngTextBox, nameTextBox, capacityTextBox, destLatTextBox, destLngTextBox;
        private TextBox addressTextBox, destAddressTextBox;
        private Button searchAddressButton, searchDestAddressButton;
        private CheckBox useAddressCheckBox;
        private RadioButton passengerRadio, vehicleRadio;
        private Button addButton, clearButton;
        private NumericUpDown numPassengersUpDown, numVehiclesUpDown, generationsUpDown, populationSizeUpDown;
        private ComboBox mapTypeComboBox;
        private CheckBox useGoogleRoutesCheckBox;

        public MainForm()
        {
            InitializeComponent();

            // Initialize services
            mapService = new MapService(API_KEY);
            routingService = new RoutingService(mapService, destinationLat, destinationLng);

            SetupUI();
            mapService.InitializeGoogleMaps(gMapControl);

            Text = "Enhanced Ride Sharing Algorithm";
            Log("Application initialized");
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // Additional initialization that needs to happen after form load can go here
        }

        #region UI Setup

        private void SetupUI()
        {
            this.Size = new Size(1400, 800);

            int inputPanelWidth = 300;
            int mapWidth = 750;
            int detailsPanelWidth = 300;

            // Left panel for controls
            Panel controlPanel = ControlExtensions.CreatePanel(
                new Point(20, 20),
                new Size(inputPanelWidth, this.ClientSize.Height - 40),
                BorderStyle.FixedSingle
            );
            Controls.Add(controlPanel);

            // Map control
            gMapControl = new GMapControl
            {
                Location = new Point(inputPanelWidth + 40, 20),
                Size = new Size(mapWidth, this.ClientSize.Height - 250),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 12,
                DragButton = MouseButtons.Left
            };
            Controls.Add(gMapControl);
            gMapControl.MouseClick += GMapControl_MouseClick;

            // Route details panel
            routeDetailsPanel = ControlExtensions.CreatePanel(
                new Point(inputPanelWidth + mapWidth + 60, 20),
                new Size(detailsPanelWidth, this.ClientSize.Height - 40),
                BorderStyle.FixedSingle
            );
            routeDetailsPanel.Controls.Add(ControlExtensions.CreateLabel(
                "Route Details",
                new Point(10, 10),
                new Size(detailsPanelWidth - 20, 20),
                new Font("Arial", 12, FontStyle.Bold)
            ));

            routeDetailsTextBox = ControlExtensions.CreateRichTextBox(
                new Point(10, 40),
                new Size(detailsPanelWidth - 20, routeDetailsPanel.Height - 50),
                true
            );
            routeDetailsPanel.Controls.Add(routeDetailsTextBox);
            Controls.Add(routeDetailsPanel);

            // Log text box
            logTextBox = ControlExtensions.CreateTextBox(
                new Point(inputPanelWidth + 40, this.ClientSize.Height - 210),
                new Size(mapWidth, 190),
                "",
                true,
                true
            );
            Controls.Add(logTextBox);

            // Add controls to the control panel
            int y = 10;

            // Map type selection
            controlPanel.Controls.Add(ControlExtensions.CreateLabel("Map Type:", new Point(10, y), new Size(80, 20)));
            mapTypeComboBox = ControlExtensions.CreateComboBox(
                new Point(100, y),
                new Size(180, 25),
                new string[] { "Google Map", "Google Satellite", "Google Hybrid", "Google Terrain" }
            );
            mapTypeComboBox.SelectedIndexChanged += MapTypeComboBox_SelectedIndexChanged;
            controlPanel.Controls.Add(mapTypeComboBox);
            y += 30;

            // Destination input
            controlPanel.Controls.Add(ControlExtensions.CreateLabel(
                "Destination:",
                new Point(10, y),
                new Size(280, 20),
                new Font("Arial", 10, FontStyle.Bold)
            ));
            y += 25;
            controlPanel.Controls.Add(ControlExtensions.CreateLabel("Latitude:", new Point(10, y), new Size(80, 20)));
            destLatTextBox = ControlExtensions.CreateTextBox(
                new Point(100, y),
                new Size(180, 25),
                destinationLat.ToString()
            );
            controlPanel.Controls.Add(destLatTextBox);
            y += 30;
            controlPanel.Controls.Add(ControlExtensions.CreateLabel("Longitude:", new Point(10, y), new Size(80, 20)));
            destLngTextBox = ControlExtensions.CreateTextBox(
                new Point(100, y),
                new Size(180, 25),
                destinationLng.ToString()
            );
            controlPanel.Controls.Add(destLngTextBox);
            y += 30;

            // Add destination address input
            controlPanel.Controls.Add(ControlExtensions.CreateLabel("- OR -", new Point(140, y), new Size(100, 20),
                new Font("Arial", 9, FontStyle.Bold), ContentAlignment.MiddleCenter));
            y += 25;

            controlPanel.Controls.Add(ControlExtensions.CreateLabel("Address:", new Point(10, y), new Size(80, 20)));
            destAddressTextBox = ControlExtensions.CreateTextBox(new Point(100, y), new Size(180, 25));
            controlPanel.Controls.Add(destAddressTextBox);
            y += 30;

            searchDestAddressButton = ControlExtensions.CreateButton(
                "Search Destination",
                new Point(100, y),
                new Size(180, 30),
                SearchDestAddressButton_Click
            );
            controlPanel.Controls.Add(searchDestAddressButton);
            y += 30;

            Button setDestButton = ControlExtensions.CreateButton(
                "Set Destination",
                new Point(100, y),
                new Size(180, 30),
                SetDestButton_Click
            );
            controlPanel.Controls.Add(setDestButton);
            y += 70;

            // Divider
            controlPanel.Controls.Add(ControlExtensions.CreateLabel(
                "Manual Add Mode",
                new Point(10, y),
                new Size(280, 20),
                new Font("Arial", 10, FontStyle.Bold),
                ContentAlignment.MiddleCenter
            ));
            y += 25;

            // Radio buttons for passenger/vehicle
            passengerRadio = ControlExtensions.CreateRadioButton("Passenger", new Point(20, y), new Size(120, 20), true);
            vehicleRadio = ControlExtensions.CreateRadioButton("Vehicle", new Point(150, y), new Size(120, 20));
            passengerRadio.CheckedChanged += RadioButton_CheckedChanged;
            vehicleRadio.CheckedChanged += RadioButton_CheckedChanged;
            controlPanel.Controls.Add(passengerRadio);
            controlPanel.Controls.Add(vehicleRadio);
            y += 30;

            // Coordinates
            controlPanel.Controls.Add(ControlExtensions.CreateLabel("Latitude:", new Point(10, y), new Size(80, 20)));
            latTextBox = ControlExtensions.CreateTextBox(new Point(100, y), new Size(180, 25));
            controlPanel.Controls.Add(latTextBox);
            y += 30;
            controlPanel.Controls.Add(ControlExtensions.CreateLabel("Longitude:", new Point(10, y), new Size(80, 20)));
            lngTextBox = ControlExtensions.CreateTextBox(new Point(100, y), new Size(180, 25));
            controlPanel.Controls.Add(lngTextBox);
            y += 30;

            // Add address input option
            controlPanel.Controls.Add(ControlExtensions.CreateLabel("- OR -", new Point(140, y), new Size(100, 20),
                new Font("Arial", 9, FontStyle.Bold), ContentAlignment.MiddleCenter));
            y += 25;

            // Add address input field
            useAddressCheckBox = ControlExtensions.CreateCheckBox("Use Address:", new Point(10, y), new Size(100, 20), false);
            useAddressCheckBox.CheckedChanged += UseAddressCheckBox_CheckedChanged;
            controlPanel.Controls.Add(useAddressCheckBox);

            addressTextBox = ControlExtensions.CreateTextBox(new Point(100, y), new Size(180, 25));
            addressTextBox.Enabled = false;
            controlPanel.Controls.Add(addressTextBox);
            y += 30;

            searchAddressButton = ControlExtensions.CreateButton(
                "Search Address",
                new Point(100, y),
                new Size(180, 30),
                SearchAddressButton_Click
            );
            searchAddressButton.Enabled = false;
            controlPanel.Controls.Add(searchAddressButton);
            y += 40;

            // Name / Capacity field
            var dynamicLabel = ControlExtensions.CreateLabel("Name:", new Point(10, y), new Size(80, 20));
            dynamicLabel.Name = "dynamicLabel";
            controlPanel.Controls.Add(dynamicLabel);

            nameTextBox = ControlExtensions.CreateTextBox(new Point(100, y), new Size(180, 25));
            capacityTextBox = ControlExtensions.CreateTextBox(new Point(100, y), new Size(180, 25), "4");
            capacityTextBox.Visible = false;
            controlPanel.Controls.Add(nameTextBox);
            controlPanel.Controls.Add(capacityTextBox);
            y += 40;

            // Add buttons
            addButton = ControlExtensions.CreateButton(
                "Add Manually",
                new Point(10, y),
                new Size(130, 30),
                (s, e) => {
                    isAddingManually = true;
                    if (isAddingPassenger) AddPassenger(); else AddVehicle();
                    isAddingManually = false;
                }
            );
            controlPanel.Controls.Add(addButton);

            Button clickAddButton = ControlExtensions.CreateButton(
                "Click on Map",
                new Point(150, y),
                new Size(130, 30),
                (s, e) => {
                    isAddingManually = true;
                    MessageBox.Show($"Click on the map to place a {(isAddingPassenger ? "passenger" : "vehicle")}",
                        "Add Mode", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            );
            controlPanel.Controls.Add(clickAddButton);
            y += 40;

            // Generate random data section
            controlPanel.Controls.Add(ControlExtensions.CreateLabel(
                "Random Data Generation",
                new Point(10, y),
                new Size(280, 20),
                new Font("Arial", 10, FontStyle.Bold),
                ContentAlignment.MiddleCenter
            ));
            y += 30;
            controlPanel.Controls.Add(ControlExtensions.CreateLabel("Passengers:", new Point(10, y), new Size(80, 20)));
            numPassengersUpDown = ControlExtensions.CreateNumericUpDown(
                new Point(100, y),
                new Size(70, 25),
                1, 50, 20
            );
            controlPanel.Controls.Add(numPassengersUpDown);
            y += 30;
            controlPanel.Controls.Add(ControlExtensions.CreateLabel("Vehicles:", new Point(10, y), new Size(80, 20)));
            numVehiclesUpDown = ControlExtensions.CreateNumericUpDown(
                new Point(100, y),
                new Size(70, 25),
                1, 20, 5
            );
            controlPanel.Controls.Add(numVehiclesUpDown);
            y += 40;

            Button generateButton = ControlExtensions.CreateButton(
                "Generate Test Data",
                new Point(10, y),
                new Size(270, 30),
                (s, e) => {
                    try
                    {
                        GenerateTestData();
                    }
                    catch (Exception ex)
                    {
                        Log($"Error generating data: {ex.Message}");
                    }
                }
            );
            controlPanel.Controls.Add(generateButton);
            y += 40;

            // Clear data button
            clearButton = ControlExtensions.CreateButton(
                "Clear All",
                new Point(10, y),
                new Size(270, 30),
                (s, e) => {
                    passengers.Clear();
                    vehicles.Clear();
                    bestSolution = null;
                    routingService.VehicleRouteDetails.Clear();
                    routingService.DisplayDataOnMap(gMapControl, passengers, vehicles);
                    UpdateRouteDetailsDisplay();
                    Log("All data cleared");
                }
            );
            controlPanel.Controls.Add(clearButton);
            y += 60;

            // Algorithm section
            controlPanel.Controls.Add(ControlExtensions.CreateLabel(
                "Algorithm Controls",
                new Point(10, y),
                new Size(280, 20),
                new Font("Arial", 10, FontStyle.Bold),
                ContentAlignment.MiddleCenter
            ));
            y += 30;

            // Algorithm parameters
            controlPanel.Controls.Add(ControlExtensions.CreateLabel("Population:", new Point(10, y), new Size(80, 20)));
            populationSizeUpDown = ControlExtensions.CreateNumericUpDown(
                new Point(100, y),
                new Size(70, 25),
                50, 500, 200, 50
            );
            controlPanel.Controls.Add(populationSizeUpDown);
            y += 30;

            controlPanel.Controls.Add(ControlExtensions.CreateLabel("Generations:", new Point(10, y), new Size(80, 20)));
            generationsUpDown = ControlExtensions.CreateNumericUpDown(
                new Point(100, y),
                new Size(70, 25),
                50, 500, 150, 50
            );
            controlPanel.Controls.Add(generationsUpDown);
            y += 40;

            useGoogleRoutesCheckBox = ControlExtensions.CreateCheckBox(
                "Use Google Routes API",
                new Point(10, y),
                new Size(270, 20),
                true
            );
            controlPanel.Controls.Add(useGoogleRoutesCheckBox);
            y += 30;

            Button runButton = ControlExtensions.CreateButton(
                "Run Algorithm",
                new Point(10, y),
                new Size(270, 30),
                async (s, e) => {
                    try
                    {
                        RunAlgorithm();
                        if (useGoogleRoutesCheckBox.Checked && bestSolution != null)
                        {
                            await routingService.GetGoogleRoutesAsync(gMapControl, bestSolution);
                            UpdateRouteDetailsDisplay();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Error running algorithm: {ex.Message}");
                    }
                }
            );
            controlPanel.Controls.Add(runButton);
            y += 40;

            Button validateButton = ControlExtensions.CreateButton(
                "Validate Solution",
                new Point(10, y),
                new Size(270, 30),
                (s, e) => {
                    try
                    {
                        ValidateSolution();
                    }
                    catch (Exception ex)
                    {
                        Log($"Error validating solution: {ex.Message}");
                    }
                }
            );
            controlPanel.Controls.Add(validateButton);
            y += 40;

            Button getRoutes = ControlExtensions.CreateButton(
                "Get Google Routes",
                new Point(10, y),
                new Size(270, 30),
                async (s, e) => {
                    try
                    {
                        if (bestSolution == null)
                        {
                            MessageBox.Show("Please run the algorithm first!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        await routingService.GetGoogleRoutesAsync(gMapControl, bestSolution);
                        UpdateRouteDetailsDisplay();
                    }
                    catch (Exception ex)
                    {
                        Log($"Error getting routes: {ex.Message}");
                    }
                }
            );
            controlPanel.Controls.Add(getRoutes);
        }

        #endregion

        #region Event Handlers

        private void SetDestButton_Click(object sender, EventArgs e)
        {
            try
            {
                double lat = double.Parse(destLatTextBox.Text);
                double lng = double.Parse(destLngTextBox.Text);

                if (!GeoCalculator.IsValidLocation(lat, lng))
                {
                    MessageBox.Show("The destination coordinates are invalid or in water.", "Invalid Location", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                destinationLat = lat;
                destinationLng = lng;

                // Update routing service with new destination
                routingService = new RoutingService(mapService, destinationLat, destinationLng);

                routingService.DisplayDataOnMap(gMapControl, passengers, vehicles);
                Log($"Destination set to: {lat}, {lng}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Invalid coordinates: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RadioButton_CheckedChanged(object sender, EventArgs e)
        {
            isAddingPassenger = passengerRadio.Checked;
            Label dynamicLabel = (Label)passengerRadio.Parent.Controls.Find("dynamicLabel", false)[0];
            dynamicLabel.Text = isAddingPassenger ? "Name:" : "Capacity:";
            nameTextBox.Visible = isAddingPassenger;
            capacityTextBox.Visible = !isAddingPassenger;
        }

        // Update the GMapControl_MouseClick method to include reverse geocoding
        private async void GMapControl_MouseClick(object sender, MouseEventArgs e)
        {
            if (!isAddingManually) return;

            PointLatLng point = gMapControl.FromLocalToLatLng(e.X, e.Y);
            latTextBox.Text = point.Lat.ToString();
            lngTextBox.Text = point.Lng.ToString();

            // If address search is enabled, try to get the address
            if (useAddressCheckBox.Checked)
            {
                string address = await mapService.ReverseGeocodeAsync(point.Lat, point.Lng);
                if (!string.IsNullOrEmpty(address))
                {
                    addressTextBox.Text = address;
                }
            }

            if (isAddingPassenger)
                AddPassenger();
            else
                AddVehicle();

            isAddingManually = false;
        }

        private void MapTypeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            mapService.ChangeMapProvider(gMapControl, mapTypeComboBox.SelectedIndex);
        }

        private void UseAddressCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            // Toggle between address and coordinates input
            bool useAddress = useAddressCheckBox.Checked;

            latTextBox.Enabled = !useAddress;
            lngTextBox.Enabled = !useAddress;
            addressTextBox.Enabled = useAddress;
            searchAddressButton.Enabled = useAddress;
        }

        private async void SearchAddressButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(addressTextBox.Text))
            {
                MessageBox.Show("Please enter an address to search", "Invalid Address", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                searchAddressButton.Enabled = false;
                Log($"Searching for address: {addressTextBox.Text}");

                var result = await mapService.GeocodeAddressAsync(addressTextBox.Text);
                if (result.HasValue)
                {
                    latTextBox.Text = result.Value.Latitude.ToString();
                    lngTextBox.Text = result.Value.Longitude.ToString();

                    Log($"Address found: {addressTextBox.Text} => {result.Value.Latitude}, {result.Value.Longitude}");

                    // Center map on found location
                    gMapControl.Position = new PointLatLng(result.Value.Latitude, result.Value.Longitude);

                    // Create a temporary marker to show the found location
                    var overlay = new GMap.NET.WindowsForms.GMapOverlay("searchResult");
                    var marker = new GMap.NET.WindowsForms.Markers.GMarkerGoogle(
                        new PointLatLng(result.Value.Latitude, result.Value.Longitude),
                        GMap.NET.WindowsForms.Markers.GMarkerGoogleType.yellow);
                    overlay.Markers.Add(marker);
                    gMapControl.Overlays.Add(overlay);

                    // Remove the marker after 5 seconds
                    System.Threading.Tasks.Task.Delay(5000).ContinueWith(t =>
                    {
                        if (IsDisposed) return;

                        this.BeginInvoke(new Action(() =>
                        {
                            gMapControl.Overlays.Remove(overlay);
                            gMapControl.Refresh();
                        }));
                    });
                }
                else
                {
                    MessageBox.Show($"Could not find the address: {addressTextBox.Text}",
                        "Address Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error searching for address: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                searchAddressButton.Enabled = true;
            }
        }

        private async void SearchDestAddressButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(destAddressTextBox.Text))
            {
                MessageBox.Show("Please enter an address to search", "Invalid Address", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                searchDestAddressButton.Enabled = false;
                Log($"Searching for destination address: {destAddressTextBox.Text}");

                var result = await mapService.GeocodeAddressAsync(destAddressTextBox.Text);
                if (result.HasValue)
                {
                    destLatTextBox.Text = result.Value.Latitude.ToString();
                    destLngTextBox.Text = result.Value.Longitude.ToString();

                    Log($"Destination address found: {destAddressTextBox.Text} => {result.Value.Latitude}, {result.Value.Longitude}");

                    // Center map on found location
                    gMapControl.Position = new PointLatLng(result.Value.Latitude, result.Value.Longitude);

                    // Create a temporary marker to show the found location
                    var overlay = new GMap.NET.WindowsForms.GMapOverlay("destSearchResult");
                    var marker = new GMap.NET.WindowsForms.Markers.GMarkerGoogle(
                        new PointLatLng(result.Value.Latitude, result.Value.Longitude),
                        GMap.NET.WindowsForms.Markers.GMarkerGoogleType.orange);
                    overlay.Markers.Add(marker);
                    gMapControl.Overlays.Add(overlay);

                    // Remove the marker after 5 seconds
                    System.Threading.Tasks.Task.Delay(5000).ContinueWith(t =>
                    {
                        if (IsDisposed) return;

                        this.BeginInvoke(new Action(() =>
                        {
                            gMapControl.Overlays.Remove(overlay);
                            gMapControl.Refresh();
                        }));
                    });
                }
                else
                {
                    MessageBox.Show($"Could not find the address: {destAddressTextBox.Text}",
                        "Address Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error searching for address: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                searchDestAddressButton.Enabled = true;
            }
        }

        #endregion

        #region Data Operations
        // Update the AddPassenger and AddVehicle methods in MainForm.cs

        private async void AddPassenger()
        {
            try
            {
                double lat, lng;
                string address = null;

                if (useAddressCheckBox.Checked)
                {
                    if (string.IsNullOrWhiteSpace(addressTextBox.Text))
                    {
                        MessageBox.Show("Please enter an address or search for one first.",
                            "Missing Address", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(latTextBox.Text) || string.IsNullOrWhiteSpace(lngTextBox.Text))
                    {
                        MessageBox.Show("Please search for the address to get coordinates.",
                            "Missing Coordinates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    lat = double.Parse(latTextBox.Text);
                    lng = double.Parse(lngTextBox.Text);
                    address = addressTextBox.Text;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(latTextBox.Text) || string.IsNullOrWhiteSpace(lngTextBox.Text))
                    {
                        MessageBox.Show("Please enter latitude and longitude coordinates.",
                            "Missing Coordinates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    lat = double.Parse(latTextBox.Text);
                    lng = double.Parse(lngTextBox.Text);

                    // Get the address for these coordinates if possible
                    address = await mapService.ReverseGeocodeAsync(lat, lng);
                }

                if (!GeoCalculator.IsValidLocation(lat, lng))
                {
                    MessageBox.Show("The location is invalid or in water.",
                        "Invalid Location", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string name = string.IsNullOrEmpty(nameTextBox.Text) ? $"P{passengers.Count}" : nameTextBox.Text;
                passengers.Add(new Passenger
                {
                    Id = passengers.Count,
                    Name = name,
                    Latitude = lat,
                    Longitude = lng,
                    Address = address
                });

                routingService.DisplayDataOnMap(gMapControl, passengers, vehicles);

                string locationInfo = address != null
                    ? $"at {address}"
                    : $"at {lat}, {lng}";

                Log($"Added passenger {name} {locationInfo}");

                // Clear inputs
                nameTextBox.Text = "";
                if (useAddressCheckBox.Checked)
                {
                    addressTextBox.Text = "";
                }
                latTextBox.Text = "";
                lngTextBox.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Invalid input: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void AddVehicle()
        {
            try
            {
                double lat, lng;
                string address = null;

                if (useAddressCheckBox.Checked)
                {
                    if (string.IsNullOrWhiteSpace(addressTextBox.Text))
                    {
                        MessageBox.Show("Please enter an address or search for one first.",
                            "Missing Address", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(latTextBox.Text) || string.IsNullOrWhiteSpace(lngTextBox.Text))
                    {
                        MessageBox.Show("Please search for the address to get coordinates.",
                            "Missing Coordinates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    lat = double.Parse(latTextBox.Text);
                    lng = double.Parse(lngTextBox.Text);
                    address = addressTextBox.Text;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(latTextBox.Text) || string.IsNullOrWhiteSpace(lngTextBox.Text))
                    {
                        MessageBox.Show("Please enter latitude and longitude coordinates.",
                            "Missing Coordinates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    lat = double.Parse(latTextBox.Text);
                    lng = double.Parse(lngTextBox.Text);

                    // Get the address for these coordinates if possible
                    address = await mapService.ReverseGeocodeAsync(lat, lng);
                }

                int capacity;
                if (!int.TryParse(capacityTextBox.Text, out capacity) || capacity < 1)
                {
                    MessageBox.Show("Capacity must be at least 1.",
                        "Invalid Capacity", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!GeoCalculator.IsValidLocation(lat, lng))
                {
                    MessageBox.Show("The location is invalid or in water.",
                        "Invalid Location", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                vehicles.Add(new Vehicle
                {
                    Id = vehicles.Count,
                    Capacity = capacity,
                    StartLatitude = lat,
                    StartLongitude = lng,
                    StartAddress = address
                });

                routingService.DisplayDataOnMap(gMapControl, passengers, vehicles);

                string locationInfo = address != null
                    ? $"at {address}"
                    : $"at {lat}, {lng}";

                Log($"Added vehicle with capacity {capacity} {locationInfo}");

                // Clear inputs
                capacityTextBox.Text = "4";
                if (useAddressCheckBox.Checked)
                {
                    addressTextBox.Text = "";
                }
                latTextBox.Text = "";
                lngTextBox.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Invalid input: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void GenerateTestData()
        {
            int passengerCount = (int)numPassengersUpDown.Value;
            int vehicleCount = (int)numVehiclesUpDown.Value;
            double centerLat = gMapControl.Position.Lat;
            double centerLng = gMapControl.Position.Lng;

            passengers.Clear();
            for (int i = 0; i < passengerCount; i++)
            {
                double lat, lng;
                int attempts = 0;
                do
                {
                    lat = centerLat + (rnd.NextDouble() - 0.5) * 0.05;
                    lng = centerLng + (rnd.NextDouble() - 0.5) * 0.05;
                    attempts++;
                } while (!GeoCalculator.IsValidLocation(lat, lng) && attempts < 10);

                if (attempts < 10)
                    passengers.Add(new Passenger { Id = i, Name = $"P{i}", Latitude = lat, Longitude = lng });
            }

            vehicles.Clear();
            int totalCapacity = 0;
            for (int i = 0; i < vehicleCount; i++)
            {
                int capacity = rnd.Next(3, 6);
                totalCapacity += capacity;
                double lat, lng;
                int attempts = 0;
                do
                {
                    lat = centerLat + (rnd.NextDouble() - 0.5) * 0.05;
                    lng = centerLng + (rnd.NextDouble() - 0.5) * 0.05;
                    attempts++;
                } while (!GeoCalculator.IsValidLocation(lat, lng) && attempts < 10);

                if (attempts < 10)
                    vehicles.Add(new Vehicle
                    {
                        Id = i,
                        Capacity = capacity,
                        StartLatitude = lat,
                        StartLongitude = lng
                    });
            }

            // Ensure total capacity can handle all passengers
            if (totalCapacity < passengers.Count && vehicles.Count > 0)
                vehicles.Last().Capacity += passengers.Count - totalCapacity;

            Log($"Generated {passengers.Count} passengers and {vehicles.Count} vehicles");
            Log($"Total vehicle capacity: {vehicles.Sum(v => v.Capacity)}");

            bestSolution = null;
            evaluatedPopulation.Clear();
            routingService.VehicleRouteDetails.Clear();

            routingService.DisplayDataOnMap(gMapControl, passengers, vehicles);
            UpdateRouteDetailsDisplay();
        }

        #endregion

        #region Algorithm Operations

        private void RunAlgorithm()
        {
            if (passengers == null || passengers.Count == 0 || vehicles == null || vehicles.Count == 0)
            {
                Log("Please add passengers and vehicles first!");
                return;
            }

            try
            {
                Log("Running genetic algorithm...");
                int populationSize = (int)populationSizeUpDown.Value;
                int generations = (int)generationsUpDown.Value;

                var solver = new RideSharingGenetic(
                    passengers,
                    vehicles,
                    populationSize,
                    destinationLat,
                    destinationLng,
                    targetTime
                );

                bestSolution = solver.Solve(generations, evaluatedPopulation);

                if (bestSolution == null)
                {
                    Log("Algorithm failed to find a solution. Please try different parameters.");
                    return;
                }

                evaluatedPopulation = solver.GetLatestPopulation();

                int assignedCount = bestSolution.Vehicles.Sum(v => v.AssignedPassengers?.Count ?? 0);
                Log($"Algorithm completed with score {bestSolution.Score:F2}");
                Log($"Assigned passengers: {assignedCount}/{passengers.Count}");

                if (assignedCount < passengers.Count)
                    Log("WARNING: Not all passengers were assigned!");

                routingService.DisplaySolutionOnMap(gMapControl, bestSolution);
                routingService.CalculateEstimatedRouteDetails(bestSolution);
                UpdateRouteDetailsDisplay();
            }
            catch (Exception ex)
            {
                Log($"Error running algorithm: {ex.Message}");
                MessageBox.Show($"Error running algorithm: {ex.Message}\n\n{ex.StackTrace}",
                    "Algorithm Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void ValidateSolution()
        {
            if (bestSolution == null)
            {
                MessageBox.Show("Please run the algorithm first!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string validationReport = routingService.ValidateSolution(bestSolution, passengers);

            MessageBox.Show(validationReport, "Validation Results",
                MessageBoxButtons.OK,
                validationReport.Contains("All passengers assigned: True") &&
                !validationReport.Contains("Capacity exceeded: True")
                    ? MessageBoxIcon.Information
                    : MessageBoxIcon.Warning);
        }

        #endregion

        #region UI Utilities

        // Update the UpdateRouteDetailsDisplay method in MainForm.cs

        private void UpdateRouteDetailsDisplay()
        {
            routeDetailsTextBox.Clear();

            if (routingService.VehicleRouteDetails.Count == 0)
            {
                routeDetailsTextBox.AppendText("No route details available.\n\n");
                routeDetailsTextBox.AppendText("Run the algorithm to see detailed timing information for each route.");
                return;
            }

            foreach (var detail in routingService.VehicleRouteDetails.Values.OrderBy(d => d.VehicleId))
            {
                // Get vehicle info
                var vehicle = vehicles.FirstOrDefault(v => v.Id == detail.VehicleId);
                string startLocation = vehicle != null && !string.IsNullOrEmpty(vehicle.StartAddress)
                    ? vehicle.StartAddress
                    : $"({vehicle?.StartLatitude ?? 0:F4}, {vehicle?.StartLongitude ?? 0:F4})";

                routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
                routeDetailsTextBox.AppendText($"Vehicle {detail.VehicleId}\n");
                routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
                routeDetailsTextBox.AppendText($"Start Location: {startLocation}\n");
                routeDetailsTextBox.AppendText($"Total Distance: {detail.TotalDistance:F2} km\n");
                routeDetailsTextBox.AppendText($"Total Time: {detail.TotalTime:F2} min\n\n");

                routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
                routeDetailsTextBox.AppendText("Stop Details:\n");
                routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;

                int stopNumber = 1;
                foreach (var stop in detail.StopDetails)
                {
                    // For stops that are passengers
                    if (stop.PassengerId >= 0)
                    {
                        var passenger = passengers.FirstOrDefault(p => p.Id == stop.PassengerId);
                        string stopName = $"Passenger {stop.PassengerName}";
                        string stopLocation = passenger != null && !string.IsNullOrEmpty(passenger.Address)
                            ? passenger.Address
                            : $"({passenger?.Latitude ?? 0:F4}, {passenger?.Longitude ?? 0:F4})";

                        routeDetailsTextBox.AppendText($"{stopNumber}. {stopName}\n");
                        routeDetailsTextBox.AppendText($"   Location: {stopLocation}\n");
                    }
                    else // For destination stop
                    {
                        string stopName = "Destination";
                        string stopLocation = !string.IsNullOrEmpty(destAddressTextBox?.Text)
                            ? destAddressTextBox.Text
                            : $"({destinationLat:F4}, {destinationLng:F4})";

                        routeDetailsTextBox.AppendText($"{stopNumber}. {stopName}\n");
                        routeDetailsTextBox.AppendText($"   Location: {stopLocation}\n");
                    }

                    routeDetailsTextBox.AppendText($"   Distance: {stop.DistanceFromPrevious:F2} km\n");
                    routeDetailsTextBox.AppendText($"   Time: {stop.TimeFromPrevious:F2} min\n");
                    routeDetailsTextBox.AppendText($"   Cumulative: {stop.CumulativeDistance:F2} km, {stop.CumulativeTime:F2} min\n\n");
                    stopNumber++;
                }

                routeDetailsTextBox.AppendText("--------------------------------\n\n");
            }
        }

        private void Log(string message)
        {
            logTextBox.AppendLog(message);
        }

        #endregion
    }
}