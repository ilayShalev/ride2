using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Http;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using Newtonsoft.Json;

namespace claudpro
{
    public partial class MainForm : Form
    {
        private List<Passenger> passengers = new List<Passenger>();
        private List<Vehicle> vehicles = new List<Vehicle>();
        private Solution bestSolution;
        private double destinationLat = 40.7500; // Times Square, NY
        private double destinationLng = -73.9900;
        private int targetTime = 30;
        private Random rnd = new Random();
        private TextBox logTextBox;
        private GMapControl gMapControl;
        private List<Solution> evaluatedPopulation = new List<Solution>();

        // Manual input fields
        private TextBox latTextBox, lngTextBox, nameTextBox, capacityTextBox, destLatTextBox, destLngTextBox;
        private RadioButton passengerRadio, vehicleRadio;
        private Button addButton, clearButton;
        private NumericUpDown numPassengersUpDown, numVehiclesUpDown, generationsUpDown, populationSizeUpDown;
        private ComboBox mapTypeComboBox;
        private CheckBox useGoogleRoutesCheckBox;

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private Panel routeDetailsPanel;
        private RichTextBox routeDetailsTextBox;

        // Drawing mode tracking
        private bool isAddingManually = false;
        private bool isAddingPassenger = true;

        // Google Maps API key
        private const string API_KEY = "AIzaSyA8gY0PbmE1EgDjxd-SdIMWWTaQf9Mi7vc"; // Replace with your actual API key
        private HttpClient httpClient = new HttpClient();

        // Cache for Google Maps routing results
        private Dictionary<string, List<PointLatLng>> routeCache = new Dictionary<string, List<PointLatLng>>();

        // Store detailed route information
        private Dictionary<int, RouteDetails> vehicleRouteDetails = new Dictionary<int, RouteDetails>();

        public MainForm()
        {
            InitializeComponent();
            SetupUI();
            InitializeGoogleMaps();
            static class Program
        {
            [STAThread]
            static void Main()
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.ClientSize = new Size(1400, 800);
        this.Name = "MainForm";
        this.Text = "Enhanced Ride Sharing Algorithm with Google Maps";
        this.FormClosing += (s, e) =>
        {
            gMapControl?.Dispose();
            httpClient?.Dispose();
        };
        this.ResumeLayout(false);
    }

    private void SetupUI()
    {
        this.Size = new Size(1400, 800);
        this.Text = "Enhanced Ride Sharing Algorithm with Google Maps";

        int inputPanelWidth = 300;
        int mapWidth = 750;
        int detailsPanelWidth = 300;

        // Left panel for controls
        Panel controlPanel = new Panel
        {
            Location = new Point(20, 20),
            Size = new Size(inputPanelWidth, this.ClientSize.Height - 40),
            BorderStyle = BorderStyle.FixedSingle,
            AutoScroll = true
        };
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
        routeDetailsPanel = new Panel
        {
            Location = new Point(inputPanelWidth + mapWidth + 60, 20),
            Size = new Size(detailsPanelWidth, this.ClientSize.Height - 40),
            BorderStyle = BorderStyle.FixedSingle
        };
        routeDetailsPanel.Controls.Add(new Label
        {
            Text = "Route Details",
            Font = new Font("Arial", 12, FontStyle.Bold),
            Location = new Point(10, 10),
            Size = new Size(detailsPanelWidth - 20, 20)
        });

        routeDetailsTextBox = new RichTextBox
        {
            Location = new Point(10, 40),
            Size = new Size(detailsPanelWidth - 20, routeDetailsPanel.Height - 50),
            ReadOnly = true,
            BackColor = Color.White,
            BorderStyle = BorderStyle.None
        };
        routeDetailsPanel.Controls.Add(routeDetailsTextBox);
        Controls.Add(routeDetailsPanel);

        // Log text box
        logTextBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(inputPanelWidth + 40, this.ClientSize.Height - 210),
            Size = new Size(mapWidth, 190),
            ReadOnly = true
        };
        Controls.Add(logTextBox);

        // Add controls to the control panel
        int y = 10;

        // Map type selection
        controlPanel.Controls.Add(new Label { Text = "Map Type:", Location = new Point(10, y), Size = new Size(80, 20) });
        mapTypeComboBox = new ComboBox
        {
            Location = new Point(100, y),
            Size = new Size(180, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        mapTypeComboBox.Items.AddRange(new string[] { "Google Map", "Google Satellite", "Google Hybrid", "Google Terrain" });
        mapTypeComboBox.SelectedIndex = 0;
        mapTypeComboBox.SelectedIndexChanged += MapTypeComboBox_SelectedIndexChanged;
        controlPanel.Controls.Add(mapTypeComboBox);
        y += 30;

        // Destination input
        controlPanel.Controls.Add(new Label { Text = "Destination:", Location = new Point(10, y), Size = new Size(280, 20), Font = new Font("Arial", 10, FontStyle.Bold) });
        y += 25;
        controlPanel.Controls.Add(new Label { Text = "Latitude:", Location = new Point(10, y), Size = new Size(80, 20) });
        destLatTextBox = new TextBox { Location = new Point(100, y), Size = new Size(180, 25), Text = destinationLat.ToString() };
        controlPanel.Controls.Add(destLatTextBox);
        y += 30;
        controlPanel.Controls.Add(new Label { Text = "Longitude:", Location = new Point(10, y), Size = new Size(80, 20) });
        destLngTextBox = new TextBox { Location = new Point(100, y), Size = new Size(180, 25), Text = destinationLng.ToString() };
        controlPanel.Controls.Add(destLngTextBox);

        Button setDestButton = new Button
        {
            Text = "Set Destination",
            Location = new Point(100, y + 30),
            Size = new Size(180, 30)
        };
        setDestButton.Click += SetDestButton_Click;
        controlPanel.Controls.Add(setDestButton);
        y += 70;

        // Divider
        controlPanel.Controls.Add(new Label
        {
            Text = "Manual Add Mode",
            Location = new Point(10, y),
            Size = new Size(280, 20),
            Font = new Font("Arial", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        });
        y += 25;

        // Radio buttons for passenger/vehicle
        passengerRadio = new RadioButton { Text = "Passenger", Location = new Point(20, y), Size = new Size(120, 20), Checked = true };
        vehicleRadio = new RadioButton { Text = "Vehicle", Location = new Point(150, y), Size = new Size(120, 20) };
        passengerRadio.CheckedChanged += RadioButton_CheckedChanged;
        vehicleRadio.CheckedChanged += RadioButton_CheckedChanged;
        controlPanel.Controls.Add(passengerRadio);
        controlPanel.Controls.Add(vehicleRadio);
        y += 30;

        // Coordinates
        controlPanel.Controls.Add(new Label { Text = "Latitude:", Location = new Point(10, y), Size = new Size(80, 20) });
        latTextBox = new TextBox { Location = new Point(100, y), Size = new Size(180, 25) };
        controlPanel.Controls.Add(latTextBox);
        y += 30;
        controlPanel.Controls.Add(new Label { Text = "Longitude:", Location = new Point(10, y), Size = new Size(80, 20) });
        lngTextBox = new TextBox { Location = new Point(100, y), Size = new Size(180, 25) };
        controlPanel.Controls.Add(lngTextBox);
        y += 30;

        // Name / Capacity field
        controlPanel.Controls.Add(new Label { Text = "Name:", Name = "dynamicLabel", Location = new Point(10, y), Size = new Size(80, 20) });
        nameTextBox = new TextBox { Location = new Point(100, y), Size = new Size(180, 25) };
        capacityTextBox = new TextBox { Location = new Point(100, y), Size = new Size(180, 25), Text = "4", Visible = false };
        controlPanel.Controls.Add(nameTextBox);
        controlPanel.Controls.Add(capacityTextBox);
        y += 40;

        // Add button
        addButton = new Button { Text = "Add Manually", Location = new Point(10, y), Size = new Size(130, 30) };
        addButton.Click += (s, e) =>
        {
            isAddingManually = true;
            if (isAddingPassenger) AddPassenger(); else AddVehicle();
            isAddingManually = false;
        };
        controlPanel.Controls.Add(addButton);

        Button clickAddButton = new Button { Text = "Click on Map", Location = new Point(150, y), Size = new Size(130, 30) };
        clickAddButton.Click += (s, e) =>
        {
            isAddingManually = true;
            MessageBox.Show($"Click on the map to place a {(isAddingPassenger ? "passenger" : "vehicle")}",
                "Add Mode", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        controlPanel.Controls.Add(clickAddButton);
        y += 40;

        // Generate random data section
        controlPanel.Controls.Add(new Label
        {
            Text = "Random Data Generation",
            Location = new Point(10, y),
            Size = new Size(280, 20),
            Font = new Font("Arial", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        });
        y += 30;
        controlPanel.Controls.Add(new Label { Text = "Passengers:", Location = new Point(10, y), Size = new Size(80, 20) });
        numPassengersUpDown = new NumericUpDown { Location = new Point(100, y), Size = new Size(70, 25), Minimum = 1, Maximum = 50, Value = 20 };
        controlPanel.Controls.Add(numPassengersUpDown);
        y += 30;
        controlPanel.Controls.Add(new Label { Text = "Vehicles:", Location = new Point(10, y), Size = new Size(80, 20) });
        numVehiclesUpDown = new NumericUpDown { Location = new Point(100, y), Size = new Size(70, 25), Minimum = 1, Maximum = 20, Value = 5 };
        controlPanel.Controls.Add(numVehiclesUpDown);
        y += 40;

        Button generateButton = new Button { Text = "Generate Test Data", Location = new Point(10, y), Size = new Size(270, 30) };
        generateButton.Click += (s, e) => { try { GenerateTestData(); } catch (Exception ex) { Log($"Error generating data: {ex.Message}"); } };
        controlPanel.Controls.Add(generateButton);
        y += 40;

        // Clear data button
        clearButton = new Button { Text = "Clear All", Location = new Point(10, y), Size = new Size(270, 30) };
        clearButton.Click += (s, e) =>
        {
            passengers.Clear();
            vehicles.Clear();
            bestSolution = null;
            vehicleRouteDetails.Clear();
            DisplayDataOnMap();
            UpdateRouteDetailsDisplay();
            Log("All data cleared");
        };
        controlPanel.Controls.Add(clearButton);
        y += 60;

        // Algorithm section
        controlPanel.Controls.Add(new Label
        {
            Text = "Algorithm Controls",
            Location = new Point(10, y),
            Size = new Size(280, 20),
            Font = new Font("Arial", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        });
        y += 30;

        // Algorithm parameters
        controlPanel.Controls.Add(new Label { Text = "Population:", Location = new Point(10, y), Size = new Size(80, 20) });
        populationSizeUpDown = new NumericUpDown { Location = new Point(100, y), Size = new Size(70, 25), Minimum = 50, Maximum = 500, Value = 200, Increment = 50 };
        controlPanel.Controls.Add(populationSizeUpDown);
        y += 30;

        controlPanel.Controls.Add(new Label { Text = "Generations:", Location = new Point(10, y), Size = new Size(80, 20) });
        generationsUpDown = new NumericUpDown { Location = new Point(100, y), Size = new Size(70, 25), Minimum = 50, Maximum = 500, Value = 150, Increment = 50 };
        controlPanel.Controls.Add(generationsUpDown);
        y += 40;

        useGoogleRoutesCheckBox = new CheckBox { Text = "Use Google Routes API", Location = new Point(10, y), Size = new Size(270, 20), Checked = true };
        controlPanel.Controls.Add(useGoogleRoutesCheckBox);
        y += 30;

        Button runButton = new Button { Text = "Run Algorithm", Location = new Point(10, y), Size = new Size(270, 30) };
        runButton.Click += async (s, e) =>
        {
            try
            {
                RunAlgorithm(evaluatedPopulation);
                if (useGoogleRoutesCheckBox.Checked && bestSolution != null)
                {
                    await GetGoogleRoutesAsync();
                }
            }
            catch (Exception ex)
            {
                Log($"Error running algorithm: {ex.Message}");
            }
        };
        controlPanel.Controls.Add(runButton);
        y += 40;

        Button validateButton = new Button { Text = "Validate Solution", Location = new Point(10, y), Size = new Size(270, 30) };
        validateButton.Click += (s, e) => { try { ValidateSolution(); } catch (Exception ex) { Log($"Error validating solution: {ex.Message}"); } };
        controlPanel.Controls.Add(validateButton);
        y += 40;

        Button getRoutes = new Button { Text = "Get Google Routes", Location = new Point(10, y), Size = new Size(270, 30) };
        getRoutes.Click += async (s, e) =>
        {
            try
            {
                await GetGoogleRoutesAsync();
            }
            catch (Exception ex)
            {
                Log($"Error getting routes: {ex.Message}");
            }
        };
        controlPanel.Controls.Add(getRoutes);
    }

    private void SetDestButton_Click(object sender, EventArgs e)
    {
        try
        {
            double lat = double.Parse(destLatTextBox.Text);
            double lng = double.Parse(destLngTextBox.Text);

            if (!IsValidLocation(lat, lng))
            {
                MessageBox.Show("The destination coordinates are invalid or in water.", "Invalid Location", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            destinationLat = lat;
            destinationLng = lng;
            DisplayDataOnMap();
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

    private void GMapControl_MouseClick(object sender, MouseEventArgs e)
    {
        if (!isAddingManually) return;
        PointLatLng point = gMapControl.FromLocalToLatLng(e.X, e.Y);
        latTextBox.Text = point.Lat.ToString();
        lngTextBox.Text = point.Lng.ToString();
        if (isAddingPassenger) AddPassenger(); else AddVehicle();
        isAddingManually = false;
    }

    private void MapTypeComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        switch (mapTypeComboBox.SelectedIndex)
        {
            case 0: gMapControl.MapProvider = GoogleMapProvider.Instance; break;
            case 1: gMapControl.MapProvider = GoogleSatelliteMapProvider.Instance; break;
            case 2: gMapControl.MapProvider = GoogleHybridMapProvider.Instance; break;
            case 3: gMapControl.MapProvider = GoogleTerrainMapProvider.Instance; break;
        }
        gMapControl.Refresh();
    }

    private void AddPassenger()
    {
        try
        {
            double lat = double.Parse(latTextBox.Text);
            double lng = double.Parse(lngTextBox.Text);

            if (!IsValidLocation(lat, lng))
            {
                MessageBox.Show("The location is invalid or in water.", "Invalid Location", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string name = string.IsNullOrEmpty(nameTextBox.Text) ? $"P{passengers.Count}" : nameTextBox.Text;
            passengers.Add(new Passenger { Id = passengers.Count, Name = name, Latitude = lat, Longitude = lng });
            DisplayDataOnMap();
            Log($"Added passenger {name} at {lat}, {lng}");
            nameTextBox.Text = "";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Invalid input: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddVehicle()
    {
        try
        {
            double lat = double.Parse(latTextBox.Text);
            double lng = double.Parse(lngTextBox.Text);
            int capacity = int.Parse(capacityTextBox.Text);

            if (!IsValidLocation(lat, lng))
            {
                MessageBox.Show("The location is invalid or in water.", "Invalid Location", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (capacity < 1)
            {
                MessageBox.Show("Capacity must be at least 1.", "Invalid Capacity", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            vehicles.Add(new Vehicle
            {
                Id = vehicles.Count,
                Capacity = capacity,
                StartLatitude = lat,
                StartLongitude = lng,
                AssignedPassengers = new List<Passenger>()
            });

            DisplayDataOnMap();
            Log($"Added vehicle with capacity {capacity} at {lat}, {lng}");
            capacityTextBox.Text = "4";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Invalid input: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void InitializeGoogleMaps()
    {
        GMaps.Instance.Mode = AccessMode.ServerAndCache;
        GoogleMapProvider.Instance.ApiKey = API_KEY;
        gMapControl.MapProvider = GoogleMapProvider.Instance;
        gMapControl.Position = new PointLatLng(40.7128, -74.0060); // New York
        gMapControl.MinZoom = 2;
        gMapControl.MaxZoom = 18;
        gMapControl.Zoom = 12;
        Log("Google Maps initialized");
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
            } while (!IsValidLocation(lat, lng) && attempts < 10);

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
            } while (!IsValidLocation(lat, lng) && attempts < 10);

            if (attempts < 10)
                vehicles.Add(new Vehicle { Id = i, Capacity = capacity, StartLatitude = lat, StartLongitude = lng, AssignedPassengers = new List<Passenger>() });
        }

        if (totalCapacity < passengers.Count && vehicles.Count > 0)
            vehicles.Last().Capacity += passengers.Count - totalCapacity;

        Log($"Generated {passengers.Count} passengers and {vehicles.Count} vehicles");
        Log($"Total vehicle capacity: {vehicles.Sum(v => v.Capacity)}");
        bestSolution = null;
        evaluatedPopulation.Clear();
        vehicleRouteDetails.Clear();
        DisplayDataOnMap();
        UpdateRouteDetailsDisplay();
    }

    private void RunAlgorithm(List<Solution> initialPopulation)
    {
        if (passengers.Count == 0 || vehicles.Count == 0)
        {
            Log("Please add passengers and vehicles first!");
            return;
        }

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

        bestSolution = solver.Solve(generations, initialPopulation);
        evaluatedPopulation = solver.GetLatestPopulation();

        int assignedCount = bestSolution.Vehicles.Sum(v => v.AssignedPassengers.Count);
        Log($"Algorithm completed with score {bestSolution.Score:F2}");
        Log($"Assigned passengers: {assignedCount}/{passengers.Count}");

        if (assignedCount < passengers.Count)
            Log("WARNING: Not all passengers were assigned!");

        DisplaySolutionOnMap();
        CalculateEstimatedRouteDetails();
        UpdateRouteDetailsDisplay();
    }

    private void ValidateSolution()
    {
        if (bestSolution == null)
        {
            MessageBox.Show("Please run the algorithm first!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var report = new StringBuilder();
        report.AppendLine("Validation Report:");
        report.AppendLine("=================");

        var assignedIds = new HashSet<int>();
        foreach (var vehicle in bestSolution.Vehicles)
            foreach (var passenger in vehicle.AssignedPassengers ?? new List<Passenger>())
                assignedIds.Add(passenger.Id);

        bool allAssigned = assignedIds.Count == passengers.Count;
        report.AppendLine($"All passengers assigned: {allAssigned}");
        if (!allAssigned)
        {
            report.AppendLine("Missing passengers:");
            foreach (var passenger in passengers)
                if (!assignedIds.Contains(passenger.Id))
                    report.AppendLine($"- Passenger {passenger.Name} (ID: {passenger.Id})");
        }

        report.AppendLine("\nVehicle Capacity Check:");
        bool overCapacity = false;
        foreach (var vehicle in bestSolution.Vehicles)
        {
            int count = vehicle.AssignedPassengers.Count;
            string status = count <= vehicle.Capacity ? "OK" : "EXCEEDED";
            report.AppendLine($"Vehicle {vehicle.Id}: {count}/{vehicle.Capacity} ({status})");
            if (count > vehicle.Capacity) overCapacity = true;
        }

        report.AppendLine($"\nCapacity exceeded: {overCapacity}");
        report.AppendLine($"Solution score: {bestSolution.Score:F2}");

        MessageBox.Show(report.ToString(), "Validation Results",
            MessageBoxButtons.OK, allAssigned && !overCapacity ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private void DisplayDataOnMap()
    {
        gMapControl.Overlays.Clear();
        var vehiclesOverlay = new GMapOverlay("vehicles");
        var passengersOverlay = new GMapOverlay("passengers");
        var destinationOverlay = new GMapOverlay("destination");

        foreach (var passenger in passengers)
        {
            var marker = new GMarkerGoogle(new PointLatLng(passenger.Latitude, passenger.Longitude), GMarkerGoogleType.blue)
            {
                ToolTipText = $"Passenger {passenger.Name} (ID: {passenger.Id})",
                ToolTipMode = MarkerTooltipMode.OnMouseOver
            };
            passengersOverlay.Markers.Add(marker);
        }

        foreach (var vehicle in vehicles)
        {
            var marker = new GMarkerGoogle(new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude), GMarkerGoogleType.green)
            {
                ToolTipText = $"Vehicle {vehicle.Id} (Capacity: {vehicle.Capacity})",
                ToolTipMode = MarkerTooltipMode.OnMouseOver
            };
            vehiclesOverlay.Markers.Add(marker);
        }

        var destMarker = new GMarkerGoogle(new PointLatLng(destinationLat, destinationLng), GMarkerGoogleType.red)
        {
            ToolTipText = "Destination",
            ToolTipMode = MarkerTooltipMode.OnMouseOver
        };
        destinationOverlay.Markers.Add(destMarker);

        gMapControl.Overlays.Add(passengersOverlay);
        gMapControl.Overlays.Add(vehiclesOverlay);
        gMapControl.Overlays.Add(destinationOverlay);

        if (passengers.Count > 0 || vehicles.Count > 0)
            gMapControl.ZoomAndCenterMarkers("passengers");
    }

    private void DisplaySolutionOnMap()
    {
        if (bestSolution == null) return;

        gMapControl.Overlays.Clear();
        var destinationOverlay = new GMapOverlay("destination");
        var destMarker = new GMarkerGoogle(new PointLatLng(destinationLat, destinationLng), GMarkerGoogleType.red)
        {
            ToolTipText = "Destination",
            ToolTipMode = MarkerTooltipMode.OnMouseOver
        };
        destinationOverlay.Markers.Add(destMarker);
        gMapControl.Overlays.Add(destinationOverlay);

        for (int i = 0; i < bestSolution.Vehicles.Count; i++)
        {
            var vehicle = bestSolution.Vehicles[i];
            if (vehicle.AssignedPassengers.Count == 0) continue;

            string routeName = $"Vehicle_{vehicle.Id}";
            var overlay = new GMapOverlay(routeName);

            var vehicleMarker = new GMarkerGoogle(new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude), GMarkerGoogleType.green)
            {
                ToolTipText = $"Vehicle {vehicle.Id} (Capacity: {vehicle.Capacity})",
                ToolTipMode = MarkerTooltipMode.OnMouseOver
            };
            overlay.Markers.Add(vehicleMarker);

            List<PointLatLng> routePoints = new List<PointLatLng> { new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude) };
            foreach (var passenger in vehicle.AssignedPassengers)
            {
                var passengerMarker = new GMarkerGoogle(new PointLatLng(passenger.Latitude, passenger.Longitude), GMarkerGoogleType.blue)
                {
                    ToolTipText = $"Passenger {passenger.Name} (ID: {passenger.Id})",
                    ToolTipMode = MarkerTooltipMode.OnMouseOver
                };
                overlay.Markers.Add(passengerMarker);
                routePoints.Add(new PointLatLng(passenger.Latitude, passenger.Longitude));
            }
            routePoints.Add(new PointLatLng(destinationLat, destinationLng));

            var route = new GMapRoute(routePoints, routeName) { Stroke = new Pen(GetRouteColor(i), 3) };
            overlay.Routes.Add(route);
            gMapControl.Overlays.Add(overlay);
        }

        gMapControl.ZoomAndCenterMarkers("destination");
    }

    private void CalculateEstimatedRouteDetails()
    {
        if (bestSolution == null) return;

        vehicleRouteDetails.Clear();

        foreach (var vehicle in bestSolution.Vehicles)
        {
            if (vehicle.AssignedPassengers.Count == 0) continue;

            var routeDetail = new RouteDetails
            {
                VehicleId = vehicle.Id,
                TotalDistance = 0,
                TotalTime = 0,
                StopDetails = new List<StopDetail>()
            };

            // Calculate time from vehicle start to first passenger
            double currentLat = vehicle.StartLatitude;
            double currentLng = vehicle.StartLongitude;
            double totalDistance = 0;
            double totalTime = 0;

            for (int i = 0; i < vehicle.AssignedPassengers.Count; i++)
            {
                var passenger = vehicle.AssignedPassengers[i];
                double distance = CalculateDistance(currentLat, currentLng, passenger.Latitude, passenger.Longitude);
                double time = (distance / 30.0) * 60; // Assuming 30 km/h average speed

                totalDistance += distance;
                totalTime += time;

                routeDetail.StopDetails.Add(new StopDetail
                {
                    StopNumber = i + 1,
                    PassengerId = passenger.Id,
                    PassengerName = passenger.Name,
                    DistanceFromPrevious = distance,
                    TimeFromPrevious = time,
                    CumulativeDistance = totalDistance,
                    CumulativeTime = totalTime
                });

                currentLat = passenger.Latitude;
                currentLng = passenger.Longitude;
            }

            // Calculate trip to final destination
            double distToDest = CalculateDistance(currentLat, currentLng, destinationLat, destinationLng);
            double timeToDest = (distToDest / 30.0) * 60;

            totalDistance += distToDest;
            totalTime += timeToDest;

            routeDetail.StopDetails.Add(new StopDetail
            {
                StopNumber = vehicle.AssignedPassengers.Count + 1,
                PassengerId = -1,
                PassengerName = "Destination",
                DistanceFromPrevious = distToDest,
                TimeFromPrevious = timeToDest,
                CumulativeDistance = totalDistance,
                CumulativeTime = totalTime
            });

            routeDetail.TotalDistance = totalDistance;
            routeDetail.TotalTime = totalTime;

            vehicleRouteDetails[vehicle.Id] = routeDetail;
        }

        UpdateRouteDetailsDisplay();
    }

    private void UpdateRouteDetailsDisplay()
    {
        routeDetailsTextBox.Clear();

        if (vehicleRouteDetails.Count == 0)
        {
            routeDetailsTextBox.AppendText("No route details available.\n\n");
            routeDetailsTextBox.AppendText("Run the algorithm to see detailed timing information for each route.");
            return;
        }

        foreach (var detail in vehicleRouteDetails.Values.OrderBy(d => d.VehicleId))
        {
            routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
            routeDetailsTextBox.AppendText($"Vehicle {detail.VehicleId}\n");
            routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
            routeDetailsTextBox.AppendText($"Total Distance: {detail.TotalDistance:F2} km\n");
            routeDetailsTextBox.AppendText($"Total Time: {detail.TotalTime:F2} min\n\n");

            routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
            routeDetailsTextBox.AppendText("Stop Details:\n");
            routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;

            int stopNumber = 1;
            foreach (var stop in detail.StopDetails)
            {
                string stopName = stop.PassengerId < 0 ? "Destination" : $"Passenger {stop.PassengerName}";
                routeDetailsTextBox.AppendText($"{stopNumber}. {stopName}\n");
                routeDetailsTextBox.AppendText($"   Distance: {stop.DistanceFromPrevious:F2} km\n");
                routeDetailsTextBox.AppendText($"   Time: {stop.TimeFromPrevious:F2} min\n");
                routeDetailsTextBox.AppendText($"   Cumulative: {stop.CumulativeDistance:F2} km, {stop.CumulativeTime:F2} min\n\n");
                stopNumber++;
            }

            routeDetailsTextBox.AppendText("--------------------------------\n\n");
        }
    }

    private async Task GetGoogleRoutesAsync()
    {
        if (bestSolution == null)
        {
            MessageBox.Show("Please run the algorithm first!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Log("Requesting routes from Google Maps Directions API...");
        gMapControl.Overlays.Clear();

        var destinationOverlay = new GMapOverlay("destination");
        var destMarker = new GMarkerGoogle(new PointLatLng(destinationLat, destinationLng), GMarkerGoogleType.red)
        {
            ToolTipText = "Destination",
            ToolTipMode = MarkerTooltipMode.OnMouseOver
        };
        destinationOverlay.Markers.Add(destMarker);
        gMapControl.Overlays.Add(destinationOverlay);

        int totalRoutes = 0;
        int successfulRoutes = 0;

        // Clear existing route details
        vehicleRouteDetails.Clear();

        for (int i = 0; i < bestSolution.Vehicles.Count; i++)
        {
            var vehicle = bestSolution.Vehicles[i];
            if (vehicle.AssignedPassengers.Count == 0) continue;

            totalRoutes++;
            string routeName = $"Vehicle_{vehicle.Id}";
            var overlay = new GMapOverlay(routeName);

            var vehicleMarker = new GMarkerGoogle(new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude), GMarkerGoogleType.green)
            {
                ToolTipText = $"Vehicle {vehicle.Id} (Capacity: {vehicle.Capacity})",
                ToolTipMode = MarkerTooltipMode.OnMouseOver
            };
            overlay.Markers.Add(vehicleMarker);

            var waypoints = new List<PointLatLng> { new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude) };
            foreach (var passenger in vehicle.AssignedPassengers)
            {
                var passengerMarker = new GMarkerGoogle(new PointLatLng(passenger.Latitude, passenger.Longitude), GMarkerGoogleType.blue)
                {
                    ToolTipText = $"Passenger {passenger.Name} (ID: {passenger.Id})",
                    ToolTipMode = MarkerTooltipMode.OnMouseOver
                };
                overlay.Markers.Add(passengerMarker);
                waypoints.Add(new PointLatLng(passenger.Latitude, passenger.Longitude));
            }
            waypoints.Add(new PointLatLng(destinationLat, destinationLng));

            try
            {
                // Get detailed directions from Google API
                string origin = $"{vehicle.StartLatitude},{vehicle.StartLongitude}";
                string destination = $"{destinationLat},{destinationLng}";
                string waypointsStr = string.Join("|", vehicle.AssignedPassengers.Select(p => $"{p.Latitude},{p.Longitude}"));

                string url = $"https://maps.googleapis.com/maps/api/directions/json?" +
                    $"origin={origin}" +
                    $"&destination={destination}" +
                    (vehicle.AssignedPassengers.Any() ? $"&waypoints={waypointsStr}" : "") +
                    $"&key={API_KEY}";

                var response = await httpClient.GetStringAsync(url);
                dynamic data = JsonConvert.DeserializeObject(response);

                if (data.status == "OK")
                {
                    var routePoints = new List<PointLatLng>();
                    var legs = new List<dynamic>();

                    foreach (var leg in data.routes[0].legs)
                    {
                        legs.Add(leg);
                        foreach (var step in leg.steps)
                            routePoints.AddRange(DecodePolyline(step.polyline.points));
                    }

                    var route = new GMapRoute(routePoints, routeName) { Stroke = new Pen(GetRouteColor(i), 3) };
                    overlay.Routes.Add(route);
                    successfulRoutes++;

                    // Update route details with actual Google data
                    await UpdateRouteDetailsWithGoogleData(vehicle, legs);

                    double totalDistance = Convert.ToDouble(data.routes[0].legs.Sum(leg => (double)leg.distance.value)) / 1000.0;
                    double totalDuration = Convert.ToDouble(data.routes[0].legs.Sum(leg => (double)leg.duration.value)) / 60.0;

                    Log($"Vehicle {vehicle.Id}: Route - {totalDistance:F1} km, {totalDuration:F0} min");
                }
                else
                {
                    Log($"Vehicle {vehicle.Id}: No route returned from Google API - Status: {data.status}");
                }
            }
            catch (Exception ex)
            {
                Log($"Error routing vehicle {vehicle.Id}: {ex.Message}");
            }

            gMapControl.Overlays.Add(overlay);
            await Task.Delay(200); // Avoid API rate limits
        }

        gMapControl.ZoomAndCenterMarkers("destination");
        Log($"Routing complete: {successfulRoutes}/{totalRoutes} routes created");

        // Update route details with actual Google data
        UpdateRouteDetailsDisplay();
    }

    private async Task UpdateRouteDetailsWithGoogleData(Vehicle vehicle, List<dynamic> legs)
    {
        if (!vehicleRouteDetails.ContainsKey(vehicle.Id))
        {
            vehicleRouteDetails[vehicle.Id] = new RouteDetails
            {
                VehicleId = vehicle.Id,
                TotalDistance = 0,
                TotalTime = 0,
                StopDetails = new List<StopDetail>()
            };
        }

        var routeDetail = vehicleRouteDetails[vehicle.Id];
        routeDetail.StopDetails.Clear();

        double totalDistance = 0;
        double totalTime = 0;

        for (int i = 0; i < legs.Count; i++)
        {
            string stopName = i < vehicle.AssignedPassengers.Count
                ? vehicle.AssignedPassengers[i].Name
                : "Destination";

            int passengerId = i < vehicle.AssignedPassengers.Count
                ? vehicle.AssignedPassengers[i].Id
                : -1;

            // Get distance and duration from Google's response
            double distance = Convert.ToDouble(legs[i].distance.value) / 1000.0; // Convert meters to km
            double time = Convert.ToDouble(legs[i].duration.value) / 60.0; // Convert seconds to minutes

            totalDistance += distance;
            totalTime += time;

            routeDetail.StopDetails.Add(new StopDetail
            {
                StopNumber = i + 1,
                PassengerId = passengerId,
                PassengerName = stopName,
                DistanceFromPrevious = distance,
                TimeFromPrevious = time,
                CumulativeDistance = totalDistance,
                CumulativeTime = totalTime
            });
        }

        routeDetail.TotalDistance = totalDistance;
        routeDetail.TotalTime = totalTime;
    }

    private async Task<List<PointLatLng>> GetGoogleDirectionsAsync(List<PointLatLng> waypoints)
    {
        if (waypoints.Count < 2) return null;

        string cacheKey = string.Join("|", waypoints.Select(p => $"{p.Lat},{p.Lng}"));
        if (routeCache.ContainsKey(cacheKey)) return routeCache[cacheKey];

        var origin = waypoints[0];
        var destination = waypoints.Last();
        var intermediates = waypoints.Skip(1).Take(waypoints.Count - 2).ToList();

        string url = $"https://maps.googleapis.com/maps/api/directions/json?" +
            $"origin={origin.Lat},{origin.Lng}&" +
            $"destination={destination.Lat},{destination.Lng}&" +
            (intermediates.Any() ? $"waypoints={string.Join("|", intermediates.Select(p => $"{p.Lat},{p.Lng}"))}&" : "") +
            $"key={API_KEY}";

        var response = await httpClient.GetStringAsync(url);
        dynamic data = JsonConvert.DeserializeObject(response);

        if (data.status != "OK") return null;

        var points = new List<PointLatLng>();
        foreach (var leg in data.routes[0].legs)
            foreach (var step in leg.steps)
                points.AddRange(DecodePolyline(step.polyline.points));

        routeCache[cacheKey] = points;
        return points;
    }

    private List<PointLatLng> DecodePolyline(string encoded)
    {
        if (string.IsNullOrEmpty(encoded))
            return new List<PointLatLng>();

        var poly = new List<PointLatLng>();
        int index = 0, len = encoded.Length;
        int lat = 0, lng = 0;

        while (index < len)
        {
            int b, shift = 0, result = 0;
            do
            {
                b = encoded[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);

            int dlat = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
            lat += dlat;

            shift = 0;
            result = 0;
            do
            {
                b = encoded[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);

            int dlng = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
            lng += dlng;

            var p = new PointLatLng(lat / 1E5, lng / 1E5);
            poly.Add(p);
        }

        return poly;
    }

    private double CalculateRouteDistance(List<PointLatLng> points)
    {
        double totalDistance = 0;
        for (int i = 0; i < points.Count - 1; i++)
        {
            var p1 = points[i];
            var p2 = points[i + 1];
            totalDistance += CalculateDistance(p1.Lat, p1.Lng, p2.Lat, p2.Lng);
        }
        return totalDistance;
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth's radius in km
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private double ToRad(double deg) => deg * Math.PI / 180;

    private Color GetRouteColor(int index)
    {
        Color[] routeColors = {
                Color.FromArgb(255, 128, 0), Color.FromArgb(128, 0, 128),
                Color.FromArgb(0, 128, 128), Color.FromArgb(128, 0, 0),
                Color.FromArgb(0, 128, 0), Color.FromArgb(0, 0, 128),
                Color.FromArgb(128, 128, 0), Color.FromArgb(128, 0, 64)
            };
        return routeColors[index % routeColors.Length];
    }

    private bool IsValidLocation(double lat, double lng)
    {
        // Basic validity check
        if (lat < -90 || lat > 90 || lng < -180 || lng > 180)
            return false;

        // Add more sophisticated checks here if needed
        // For a proper implementation, you might want to use actual map data
        // to check if the point is on land

        return true;
    }

    private void Log(string message)
    {
        logTextBox.AppendText($"{DateTime.Now:HH:mm:ss} - {message}\r\n");
        logTextBox.ScrollToCaret();
    }
}

public class Passenger
{
    public int Id { get; set; }
    public string Name { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class Vehicle
{
    public int Id { get; set; }
    public int Capacity { get; set; }
    public double StartLatitude { get; set; }
    public double StartLongitude { get; set; }
    public List<Passenger> AssignedPassengers { get; set; }
    public double TotalDistance { get; set; }
    public double TotalTime { get; set; }
}

public class StopDetail
{
    public int StopNumber { get; set; }
    public int PassengerId { get; set; }
    public string PassengerName { get; set; }
    public double DistanceFromPrevious { get; set; }
    public double TimeFromPrevious { get; set; }
    public double CumulativeDistance { get; set; }
    public double CumulativeTime { get; set; }
}

public class RouteDetails
{
    public int VehicleId { get; set; }
    public double TotalDistance { get; set; }
    public double TotalTime { get; set; }
    public List<StopDetail> StopDetails { get; set; }
}

public class Solution
{
    public List<Vehicle> Vehicles { get; set; }
    public double Score { get; set; }
}

public class RideSharingGenetic
{
    private List<Passenger> passengers;
    private List<Vehicle> vehicles;
    private int populationSize;
    private double destinationLat, destinationLng;
    private int targetTime;
    private Random rnd = new Random();
    private List<Solution> population;

    public RideSharingGenetic(List<Passenger> passengers, List<Vehicle> vehicles, int populationSize,
        double destinationLat, double destinationLng, int targetTime)
    {
        this.passengers = passengers;
        this.vehicles = vehicles;
        this.populationSize = populationSize;
        this.destinationLat = destinationLat;
        this.destinationLng = destinationLng;
        this.targetTime = targetTime;
    }

    public Solution Solve(int generations, List<Solution> initialPopulation)
    {
        population = initialPopulation != null && initialPopulation.Count > 0
            ? initialPopulation
            : GenerateInitialPopulation();

        for (int i = 0; i < generations; i++)
        {
            var newPopulation = new List<Solution>();
            newPopulation.Add(GetBestSolution()); // Elitism

            while (newPopulation.Count < populationSize)
            {
                var parent1 = TournamentSelection();
                var parent2 = TournamentSelection();
                var child = Crossover(parent1, parent2);
                Mutate(child);
                newPopulation.Add(child);
            }

            population = newPopulation;
        }

        return GetBestSolution();
    }

    public List<Solution> GetLatestPopulation() => population;

    private List<Solution> GenerateInitialPopulation()
    {
        var result = new List<Solution>();
        for (int i = 0; i < populationSize; i++)
        {
            var solution = new Solution { Vehicles = DeepCopyVehicles(), Score = 0 };
            var unassigned = passengers.ToList();

            // Shuffle passengers for variety in initial solutions
            unassigned = unassigned.OrderBy(x => rnd.Next()).ToList();

            foreach (var vehicle in solution.Vehicles)
            {
                int toAssign = Math.Min(vehicle.Capacity, unassigned.Count);
                for (int j = 0; j < toAssign; j++)
                {
                    var passenger = unassigned[0];
                    vehicle.AssignedPassengers.Add(passenger);
                    unassigned.RemoveAt(0);
                }
            }

            // Try to assign any remaining passengers
            if (unassigned.Count > 0)
            {
                foreach (var passenger in unassigned.ToList())
                {
                    var vehicle = solution.Vehicles
                        .OrderBy(v => v.AssignedPassengers.Count)
                        .FirstOrDefault();

                    if (vehicle != null)
                    {
                        vehicle.AssignedPassengers.Add(passenger);
                        unassigned.Remove(passenger);
                    }
                }
            }

            solution.Score = Evaluate(solution);
            result.Add(solution);
        }

        return result;
    }

    private List<Vehicle> DeepCopyVehicles()
    {
        return vehicles.Select(v => new Vehicle
        {
            Id = v.Id,
            Capacity = v.Capacity,
            StartLatitude = v.StartLatitude,
            StartLongitude = v.StartLongitude,
            AssignedPassengers = new List<Passenger>(),
            TotalDistance = 0,
            TotalTime = 0
        }).ToList();
    }

    private double Evaluate(Solution solution)
    {
        double totalDistance = 0;
        int assignedCount = 0;
        int usedVehicles = 0;

        foreach (var vehicle in solution.Vehicles)
        {
            if (vehicle.AssignedPassengers.Count == 0) continue;

            usedVehicles++;
            double vehicleDistance = 0;
            double currentLat = vehicle.StartLatitude;
            double currentLng = vehicle.StartLongitude;

            foreach (var passenger in vehicle.AssignedPassengers)
            {
                double legDistance = CalculateDistance(currentLat, currentLng,
                    passenger.Latitude, passenger.Longitude);
                vehicleDistance += legDistance;

                currentLat = passenger.Latitude;
                currentLng = passenger.Longitude;
            }

            // Add distance to final destination
            double destDistance = CalculateDistance(currentLat, currentLng,
                destinationLat, destinationLng);
            vehicleDistance += destDistance;

            vehicle.TotalDistance = vehicleDistance;
            vehicle.TotalTime = (vehicleDistance / 30.0) * 60; // Assuming 30 km/h, converting to minutes

            totalDistance += vehicleDistance;
            assignedCount += vehicle.AssignedPassengers.Count;
        }

        // Calculate score - lower distance is better, bonus for using fewer vehicles
        // and severe penalty for unassigned passengers
        double score = 10000.0 / (1 + totalDistance);

        // Penalty for unused capacity
        int unusedCapacity = vehicles.Sum(v => v.Capacity) - assignedCount;
        score -= unusedCapacity * 10;

        // Bonus for using fewer vehicles
        score += (vehicles.Count - usedVehicles) * 50;

        // Critical penalty for unassigned passengers
        if (assignedCount < passengers.Count)
        {
            score -= 1000 * (passengers.Count - assignedCount);
        }

        return score;
    }

    private Solution TournamentSelection()
    {
        int tournamentSize = Math.Min(5, population.Count);
        var competitors = new List<Solution>();

        for (int i = 0; i < tournamentSize; i++)
        {
            int idx = rnd.Next(population.Count);
            competitors.Add(population[idx]);
        }

        var winner = competitors.OrderByDescending(s => s.Score).First();
        return DeepCopySolution(winner);
    }

    private Solution DeepCopySolution(Solution solution)
    {
        return new Solution
        {
            Score = solution.Score,
            Vehicles = solution.Vehicles.Select(v => new Vehicle
            {
                Id = v.Id,
                Capacity = v.Capacity,
                StartLatitude = v.StartLatitude,
                StartLongitude = v.StartLongitude,
                AssignedPassengers = v.AssignedPassengers.ToList(),
                TotalDistance = v.TotalDistance,
                TotalTime = v.TotalTime
            }).ToList()
        };
    }

    private Solution Crossover(Solution parent1, Solution parent2)
    {
        var child = new Solution { Vehicles = DeepCopyVehicles(), Score = 0 };
        var assigned = new HashSet<int>();

        // First inherit from parent1 for half the vehicles
        for (int i = 0; i < child.Vehicles.Count / 2; i++)
        {
            var sourceVehicle = parent1.Vehicles[i];
            foreach (var passenger in sourceVehicle.AssignedPassengers)
            {
                if (!assigned.Contains(passenger.Id) &&
                    child.Vehicles[i].AssignedPassengers.Count < child.Vehicles[i].Capacity)
                {
                    child.Vehicles[i].AssignedPassengers.Add(passenger);
                    assigned.Add(passenger.Id);
                }
            }
        }

        // Then inherit from parent2 for the other half
        for (int i = child.Vehicles.Count / 2; i < child.Vehicles.Count; i++)
        {
            var sourceVehicle = parent2.Vehicles[i];
            foreach (var passenger in sourceVehicle.AssignedPassengers)
            {
                if (!assigned.Contains(passenger.Id) &&
                    child.Vehicles[i].AssignedPassengers.Count < child.Vehicles[i].Capacity)
                {
                    child.Vehicles[i].AssignedPassengers.Add(passenger);
                    assigned.Add(passenger.Id);
                }
            }
        }

        // Assign any remaining passengers
        var unassignedPassengers = passengers
            .Where(p => !assigned.Contains(p.Id))
            .ToList();

        foreach (var passenger in unassignedPassengers)
        {
            var availableVehicles = child.Vehicles
                .Where(v => v.AssignedPassengers.Count < v.Capacity)
                .OrderBy(v => v.AssignedPassengers.Count)
                .ToList();

            if (availableVehicles.Any())
            {
                availableVehicles.First().AssignedPassengers.Add(passenger);
                assigned.Add(passenger.Id);
            }
            else
            {
                // Overload a vehicle if necessary to ensure all passengers are assigned
                var vehicle = child.Vehicles.OrderBy(v => v.AssignedPassengers.Count).First();
                vehicle.AssignedPassengers.Add(passenger);
                assigned.Add(passenger.Id);
            }
        }

        child.Score = Evaluate(child);
        return child;
    }

    private void Mutate(Solution solution)
    {
        if (rnd.NextDouble() < 0.3) // 30% chance of mutation
        {
            int mutationType = rnd.Next(3);
            switch (mutationType)
            {
                case 0: // Swap passengers between vehicles
                    SwapPassengers(solution);
                    break;
                case 1: // Reorder passengers within a vehicle
                    ReorderPassengers(solution);
                    break;
                case 2: // Move a passenger to another vehicle
                    MovePassenger(solution);
                    break;
            }

            solution.Score = Evaluate(solution);
        }
    }

    private void SwapPassengers(Solution solution)
    {
        // Only proceed if we have at least 2 vehicles with passengers
        var vehiclesWithPassengers = solution.Vehicles
            .Where(v => v.AssignedPassengers.Count > 0)
            .ToList();

        if (vehiclesWithPassengers.Count < 2) return;

        // Select two random vehicles
        int idx1 = rnd.Next(vehiclesWithPassengers.Count);
        int idx2 = rnd.Next(vehiclesWithPassengers.Count);
        while (idx2 == idx1) idx2 = rnd.Next(vehiclesWithPassengers.Count);

        var vehicle1 = vehiclesWithPassengers[idx1];
        var vehicle2 = vehiclesWithPassengers[idx2];

        // Swap a random passenger from each vehicle
        if (vehicle1.AssignedPassengers.Count > 0 && vehicle2.AssignedPassengers.Count > 0)
        {
            int passengerIdx1 = rnd.Next(vehicle1.AssignedPassengers.Count);
            int passengerIdx2 = rnd.Next(vehicle2.AssignedPassengers.Count);

            var passenger1 = vehicle1.AssignedPassengers[passengerIdx1];
            var passenger2 = vehicle2.Assign
                var passenger1 = vehicle1.AssignedPassengers[passengerIdx1];
            var passenger2 = vehicle2.AssignedPassengers[passengerIdx2];

            vehicle1.AssignedPassengers[passengerIdx1] = passenger2;
            vehicle2.AssignedPassengers[passengerIdx2] = passenger1;
        }
    }

    private void ReorderPassengers(Solution solution)
    {
        // Select a random vehicle with multiple passengers
        var vehiclesWithMultiplePassengers = solution.Vehicles
            .Where(v => v.AssignedPassengers.Count > 1)
            .ToList();

        if (vehiclesWithMultiplePassengers.Count == 0) return;

        var vehicle = vehiclesWithMultiplePassengers[rnd.Next(vehiclesWithMultiplePassengers.Count)];

        // Shuffle the passengers in this vehicle
        vehicle.AssignedPassengers = vehicle.AssignedPassengers
            .OrderBy(x => rnd.Next())
            .ToList();
    }

    private void MovePassenger(Solution solution)
    {
        // Select a random vehicle with passengers
        var vehiclesWithPassengers = solution.Vehicles
            .Where(v => v.AssignedPassengers.Count > 0)
            .ToList();

        if (vehiclesWithPassengers.Count == 0) return;

        var sourceVehicle = vehiclesWithPassengers[rnd.Next(vehiclesWithPassengers.Count)];

        // Select a random passenger
        int passengerIdx = rnd.Next(sourceVehicle.AssignedPassengers.Count);
        var passenger = sourceVehicle.AssignedPassengers[passengerIdx];

        // Find a target vehicle that is not the source
        var targetVehicle = solution.Vehicles
            .Where(v => v.Id != sourceVehicle.Id)
            .OrderBy(x => rnd.Next())
            .FirstOrDefault();

        if (targetVehicle != null)
        {
            sourceVehicle.AssignedPassengers.RemoveAt(passengerIdx);
            targetVehicle.AssignedPassengers.Add(passenger);
        }
    }

    private Solution GetBestSolution() => population.OrderByDescending(s => s.Score).First();

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth's radius in km
        double dLat = ToRad(lat2 - lat1);
        double dLon = ToRad(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                  Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                  Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private double ToRad(double deg) => deg * Math.PI / 180;
}
}