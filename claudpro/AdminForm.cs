using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SQLite;
using claudpro.Models;
using claudpro.Services;
using claudpro.UI;

namespace claudpro
{
    public partial class AdminForm : Form
    {
        private readonly DatabaseService dbService;
        private readonly MapService mapService;
        private RoutingService routingService;
        private RideSharingGenetic algorithmService;

        private TabControl tabControl;
        private TabPage usersTab;
        private TabPage vehiclesTab; // Changed from driversTab to match usage
        private TabPage passengersTab;
        private TabPage routesTab;
        private TabPage destinationTab;
        private TabPage schedulingTab;

        // Lists to store data
        private List<(int Id, string Username, string UserType, string Name, string Email, string Phone)> users;
        private List<Vehicle> vehicles;
        private List<Passenger> passengers;
        private Solution currentSolution;
        private ListView usersListView;
        private ListView driversListView; // Added missing ListView

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

            // Use the designer-generated InitializeComponent
            InitializeComponent();

            // Call our custom UI setup
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

        private void SetupUI()
        {
            // Remove this if you move it to the Designer
            Text = "RideMatch - Administrator Interface";
            Size = new Size(1200, 800);
            StartPosition = FormStartPosition.CenterScreen;

            tabControl = new TabControl
            {
                Location = new Point(10, 10),
                Size = new Size(1170, 740),
                Dock = DockStyle.Fill
            };
            Controls.Add(tabControl);

            // Create tabs
            usersTab = new TabPage("Users");
            vehiclesTab = new TabPage("Vehicles"); // Changed from driversTab to match usage
            passengersTab = new TabPage("Passengers");
            routesTab = new TabPage("Routes");
            destinationTab = new TabPage("Destination");
            schedulingTab = new TabPage("Scheduling");

            // Add tabs to tab control
            tabControl.TabPages.Add(usersTab);
            tabControl.TabPages.Add(vehiclesTab);
            tabControl.TabPages.Add(passengersTab);
            tabControl.TabPages.Add(routesTab);
            tabControl.TabPages.Add(destinationTab);
            tabControl.TabPages.Add(schedulingTab);

            // Setup each tab
            SetupUsersTab();
            SetupVehiclesTab();
            SetupPassengersTab();
            SetupRoutesTab();
            SetupDestinationTab();
            SetupSchedulingTab();
        }

        #region Tab Setup

        private void SetupUsersTab()
        {

            // Users ListView
            usersListView = new ListView
            {
                Location = new Point(10, 50),
                Size = new Size(1140, 650),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
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

            // Edit User button
            var editUserButton = ControlExtensions.CreateButton(
                "Edit User",
                new Point(270, 10),
                new Size(120, 30),
                (s, e) => {
                    // Get the selected user
                    if (usersListView.SelectedItems.Count == 0)
                    {
                        MessageBox.Show("Please select a user to edit.",
                            "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    int userId = int.Parse(usersListView.SelectedItems[0].Text);
                    string username = usersListView.SelectedItems[0].SubItems[1].Text;
                    string userType = usersListView.SelectedItems[0].SubItems[2].Text;
                    string name = usersListView.SelectedItems[0].SubItems[3].Text;
                    string email = usersListView.SelectedItems[0].SubItems[4].Text;
                    string phone = usersListView.SelectedItems[0].SubItems[5].Text;

                    // Display an edit dialog
                    using (var editForm = new UserEditForm(dbService, userId, username, userType, name, email, phone))
                    {
                        if (editForm.ShowDialog() == DialogResult.OK)
                        {
                            // Refresh user list
                            LoadUsersAsync().ContinueWith(_ => {
                                this.Invoke(new Action(async () => {
                                    await DisplayUsersAsync(usersListView);
                                }));
                            });
                        }
                    }
                }
            );
            usersTab.Controls.Add(editUserButton);

            // Delete User button
            var deleteUserButton = ControlExtensions.CreateButton(
                "Delete User",
                new Point(400, 10),
                new Size(120, 30),
                async (s, e) => {
                    // Get the selected user
                    if (usersListView.SelectedItems.Count == 0)
                    {
                        MessageBox.Show("Please select a user to delete.",
                            "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    int userId = int.Parse(usersListView.SelectedItems[0].Text);
                    string username = usersListView.SelectedItems[0].SubItems[1].Text;

                    // Confirm deletion
                    if (MessageBox.Show($"Are you sure you want to delete user {username}?",
                            "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        try
                        {
                            // Delete the user
                            bool success = await dbService.DeleteUserAsync(userId);

                            if (success)
                            {
                                // Refresh user list
                                await LoadUsersAsync();
                                await DisplayUsersAsync(usersListView);
                                MessageBox.Show($"User {username} deleted successfully.",
                                    "User Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show($"Could not delete user {username}. The user may not exist.",
                                    "Deletion Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error deleting user: {ex.Message}",
                                "Deletion Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            );
            usersTab.Controls.Add(deleteUserButton);
        }

        private void SetupVehiclesTab()
        {
            // Initialize driversListView
            driversListView = new ListView
            {
                Location = new Point(10, 50),
                Size = new Size(1140, 400),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            // Change ListView column headers
            driversListView.Columns.Add("ID", 50);
            driversListView.Columns.Add("Driver Name", 150);
            driversListView.Columns.Add("Vehicle Capacity", 80);
            driversListView.Columns.Add("Start Location", 300);
            driversListView.Columns.Add("Available Tomorrow", 120);
            driversListView.Columns.Add("User ID", 80);

            // Add the ListView to the tab
            vehiclesTab.Controls.Add(driversListView);

            // Change button text
            var refreshButton = ControlExtensions.CreateButton(
                "Refresh Drivers",
                new Point(10, 10),
                new Size(120, 30),
                async (s, e) => {
                    await LoadVehiclesAsync();
                    await DisplayDriversAsync(driversListView);
                }
            );
            vehiclesTab.Controls.Add(refreshButton);

            var addDriverButton = ControlExtensions.CreateButton(
                "Add Driver",
                new Point(140, 10),
                new Size(120, 30),
                (s, e) => ShowVehicleEditForm(0)
            );
            vehiclesTab.Controls.Add(addDriverButton);

            // Edit Vehicle button
            var editVehicleButton = ControlExtensions.CreateButton(
                "Edit Vehicle",
                new Point(270, 10),
                new Size(120, 30),
                (s, e) => {
                    if (driversListView.SelectedItems.Count == 0)
                    {
                        MessageBox.Show("Please select a vehicle to edit.",
                            "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    int vehicleId = int.Parse(driversListView.SelectedItems[0].Text);
                    ShowVehicleEditForm(vehicleId);
                }
            );
            vehiclesTab.Controls.Add(editVehicleButton);

            // Delete Vehicle button
            var deleteVehicleButton = ControlExtensions.CreateButton(
                "Delete Vehicle",
                new Point(400, 10),
                new Size(120, 30),
                async (s, e) => {
                    if (driversListView.SelectedItems.Count == 0)
                    {
                        MessageBox.Show("Please select a vehicle to delete.",
                            "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    int vehicleId = int.Parse(driversListView.SelectedItems[0].Text);
                    string driverName = driversListView.SelectedItems[0].SubItems[1].Text;

                    if (MessageBox.Show($"Are you sure you want to delete vehicle {vehicleId} assigned to {driverName}?",
                            "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        try
                        {
                            // Delete vehicle logic
                            using (var cmd = new SQLiteCommand(dbService.GetConnection()))
                            {
                                cmd.CommandText = "DELETE FROM Vehicles WHERE VehicleID = @VehicleID";
                                cmd.Parameters.AddWithValue("@VehicleID", vehicleId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            await LoadVehiclesAsync();
                            await DisplayDriversAsync(driversListView);
                            MessageBox.Show("Vehicle deleted successfully.",
                                "Vehicle Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error deleting vehicle: {ex.Message}",
                                "Deletion Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            );
            vehiclesTab.Controls.Add(deleteVehicleButton);

            // Map for vehicle locations
            var gMapControl = new GMap.NET.WindowsForms.GMapControl
            {
                Location = new Point(10, 460),
                Size = new Size(1140, 240),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 12,
                DragButton = MouseButtons.Left
            };
            vehiclesTab.Controls.Add(gMapControl);
            mapService.InitializeGoogleMaps(gMapControl);

            // Display vehicles on map when tab is shown
            tabControl.SelectedIndexChanged += async (s, e) => {
                if (tabControl.SelectedTab == vehiclesTab)
                {
                    await LoadVehiclesAsync();
                    await DisplayDriversAsync(driversListView);
                    DisplayVehiclesOnMap(gMapControl);
                }
            };
        }

        private void SetupPassengersTab()
        {
            // Passengers ListView
            var passengersListView = new ListView
            {
                Location = new Point(10, 50),
                Size = new Size(1140, 400),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            passengersListView.Columns.Add("ID", 50);
            passengersListView.Columns.Add("Name", 150);
            passengersListView.Columns.Add("Location", 300);
            passengersListView.Columns.Add("Available Tomorrow", 120);
            passengersListView.Columns.Add("User ID", 80);
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

            // Add Passenger button
            var addPassengerButton = ControlExtensions.CreateButton(
                "Add Passenger",
                new Point(140, 10),
                new Size(120, 30),
                (s, e) => ShowPassengerEditForm(0)
            );
            passengersTab.Controls.Add(addPassengerButton);

            // Edit Passenger button
            var editPassengerButton = ControlExtensions.CreateButton(
                "Edit Passenger",
                new Point(270, 10),
                new Size(120, 30),
                (s, e) => {
                    if (passengersListView.SelectedItems.Count == 0)
                    {
                        MessageBox.Show("Please select a passenger to edit.",
                            "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    int passengerId = int.Parse(passengersListView.SelectedItems[0].Text);
                    ShowPassengerEditForm(passengerId);
                }
            );
            passengersTab.Controls.Add(editPassengerButton);

            // Delete Passenger button
            var deletePassengerButton = ControlExtensions.CreateButton(
                "Delete Passenger",
                new Point(400, 10),
                new Size(120, 30),
                async (s, e) => {
                    if (passengersListView.SelectedItems.Count == 0)
                    {
                        MessageBox.Show("Please select a passenger to delete.",
                            "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    int passengerId = int.Parse(passengersListView.SelectedItems[0].Text);
                    string passengerName = passengersListView.SelectedItems[0].SubItems[1].Text;

                    if (MessageBox.Show($"Are you sure you want to delete passenger {passengerName}?",
                            "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        try
                        {
                            // Delete passenger logic
                            using (var cmd = new SQLiteCommand(dbService.GetConnection()))
                            {
                                cmd.CommandText = "DELETE FROM Passengers WHERE PassengerID = @PassengerID";
                                cmd.Parameters.AddWithValue("@PassengerID", passengerId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            await LoadPassengersAsync();
                            await DisplayPassengersAsync(passengersListView);
                            MessageBox.Show($"Passenger {passengerName} deleted successfully.",
                                "Passenger Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error deleting passenger: {ex.Message}",
                                "Deletion Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            );
            passengersTab.Controls.Add(deletePassengerButton);

            // Map for passenger locations
            var gMapControl = new GMap.NET.WindowsForms.GMapControl
            {
                Location = new Point(10, 460),
                Size = new Size(1140, 240),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 12,
                DragButton = MouseButtons.Left
            };
            passengersTab.Controls.Add(gMapControl);
            mapService.InitializeGoogleMaps(gMapControl);

            // Display passengers on map when tab is shown
            tabControl.SelectedIndexChanged += async (s, e) => {
                if (tabControl.SelectedTab == passengersTab)
                {
                    await LoadPassengersAsync();
                    await DisplayPassengersAsync(passengersListView);
                    DisplayPassengersOnMap(gMapControl);
                }
            };
        }

        private void SetupRoutesTab()
        {
            // Routes panel with date selection
            var dateSelector = new DateTimePicker
            {
                Location = new Point(140, 10),
                Size = new Size(200, 25),
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today
            };
            routesTab.Controls.Add(dateSelector);

            routesTab.Controls.Add(ControlExtensions.CreateLabel(
                "Select Date:", new Point(10, 10), new Size(120, 25),
                null, ContentAlignment.MiddleRight));

            var loadButton = ControlExtensions.CreateButton(
                "Load Routes",
                new Point(350, 10),
                new Size(120, 30),
                async (s, e) => {
                    await LoadRoutesForDateAsync(dateSelector.Value);
                }
            );
            routesTab.Controls.Add(loadButton);

            // Routes display panel
            var routesPanel = ControlExtensions.CreatePanel(
                new Point(10, 50),
                new Size(1140, 660),
                BorderStyle.FixedSingle
            );
            routesTab.Controls.Add(routesPanel);

            // Map for route visualization 
            var gMapControl = new GMap.NET.WindowsForms.GMapControl
            {
                Location = new Point(10, 10),
                Size = new Size(700, 640),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 12,
                DragButton = MouseButtons.Left
            };
            routesPanel.Controls.Add(gMapControl);
            mapService.InitializeGoogleMaps(gMapControl);

            // Routes list
            var routesListView = new ListView
            {
                Location = new Point(720, 10),
                Size = new Size(410, 320),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            routesListView.Columns.Add("Vehicle", 80);
            routesListView.Columns.Add("Driver", 120);
            routesListView.Columns.Add("Passengers", 80);
            routesListView.Columns.Add("Distance", 80);
            routeDetailsTextBox = new RichTextBox
            {
                Location = new Point(720, 340),
                Size = new Size(410, 310),
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None
            };
            routesPanel.Controls.Add(routesListView);
            routesPanel.Controls.Add(routeDetailsTextBox);

            // Select route to view details
            routesListView.SelectedIndexChanged += (s, e) => {
                if (routesListView.SelectedItems.Count > 0)
                {
                    int vehicleId = int.Parse(routesListView.SelectedItems[0].SubItems[0].Text);
                    DisplayRouteDetails(vehicleId);
                }
            };
        }

        private void SetupDestinationTab()
        {
            // Destination edit panel
            var panel = ControlExtensions.CreatePanel(
                new Point(10, 50),
                new Size(1140, 660),
                BorderStyle.FixedSingle
            );
            destinationTab.Controls.Add(panel);

            // Name and time
            panel.Controls.Add(ControlExtensions.CreateLabel(
                "Destination Name:", new Point(20, 20), new Size(150, 25)));
            var nameTextBox = ControlExtensions.CreateTextBox(
                new Point(180, 20), new Size(300, 25));
            panel.Controls.Add(nameTextBox);

            panel.Controls.Add(ControlExtensions.CreateLabel(
                "Target Arrival Time:", new Point(20, 60), new Size(150, 25)));
            var timeTextBox = ControlExtensions.CreateTextBox(
                new Point(180, 60), new Size(150, 25), "08:00:00");
            panel.Controls.Add(timeTextBox);

            // Map for destination selection
            var gMapControl = new GMap.NET.WindowsForms.GMapControl
            {
                Location = new Point(20, 120),
                Size = new Size(800, 500),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 12,
                DragButton = MouseButtons.Left
            };
            panel.Controls.Add(gMapControl);
            mapService.InitializeGoogleMaps(gMapControl);

            // Location input
            panel.Controls.Add(ControlExtensions.CreateLabel(
                "Latitude:", new Point(850, 120), new Size(80, 25)));
            var latTextBox = ControlExtensions.CreateTextBox(
                new Point(940, 120), new Size(180, 25));
            panel.Controls.Add(latTextBox);

            panel.Controls.Add(ControlExtensions.CreateLabel(
                "Longitude:", new Point(850, 155), new Size(80, 25)));
            var lngTextBox = ControlExtensions.CreateTextBox(
                new Point(940, 155), new Size(180, 25));
            panel.Controls.Add(lngTextBox);

            // Address search
            panel.Controls.Add(ControlExtensions.CreateLabel(
                "Address:", new Point(850, 190), new Size(80, 25)));
            var addressTextBox = ControlExtensions.CreateTextBox(
                new Point(940, 190), new Size(180, 60), "", true);
            panel.Controls.Add(addressTextBox);

            var searchButton = ControlExtensions.CreateButton(
                "Search Address",
                new Point(940, 260),
                new Size(180, 30),
                async (s, e) => {
                    try
                    {
                        var result = await mapService.GeocodeAddressAsync(addressTextBox.Text);
                        if (result.HasValue)
                        {
                            latTextBox.Text = result.Value.Latitude.ToString();
                            lngTextBox.Text = result.Value.Longitude.ToString();
                            gMapControl.Position = new GMap.NET.PointLatLng(result.Value.Latitude, result.Value.Longitude);
                            gMapControl.Zoom = 15;

                            // Show marker
                            gMapControl.Overlays.Clear();
                            var overlay = new GMap.NET.WindowsForms.GMapOverlay("destinationMarker");
                            var marker = new GMap.NET.WindowsForms.Markers.GMarkerGoogle(
                                new GMap.NET.PointLatLng(result.Value.Latitude, result.Value.Longitude),
                                GMap.NET.WindowsForms.Markers.GMarkerGoogleType.red
                            );
                            overlay.Markers.Add(marker);
                            gMapControl.Overlays.Add(overlay);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error searching address: {ex.Message}",
                            "Search Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            );
            panel.Controls.Add(searchButton);

            // Save button
            var saveButton = ControlExtensions.CreateButton(
                "Save Destination",
                new Point(940, 310),
                new Size(180, 30),
                async (s, e) => {
                    try
                    {
                        // Validate inputs
                        if (string.IsNullOrWhiteSpace(nameTextBox.Text))
                        {
                            MessageBox.Show("Please enter a destination name.",
                                "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        if (!double.TryParse(latTextBox.Text, out double lat) ||
                            !double.TryParse(lngTextBox.Text, out double lng))
                        {
                            MessageBox.Show("Please enter valid coordinates.",
                                "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        // Save destination
                        bool success = await dbService.UpdateDestinationAsync(
                            nameTextBox.Text, lat, lng, timeTextBox.Text, addressTextBox.Text);

                        if (success)
                        {
                            MessageBox.Show("Destination updated successfully.",
                                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                            // Update stored values
                            destinationLat = lat;
                            destinationLng = lng;
                            destinationName = nameTextBox.Text;
                            destinationAddress = addressTextBox.Text;
                            destinationTargetTime = timeTextBox.Text;

                            // Reinitialize routing service with new destination
                            routingService = new RoutingService(mapService, destinationLat, destinationLng);
                        }
                        else
                        {
                            MessageBox.Show("Failed to update destination.",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving destination: {ex.Message}",
                            "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            );
            panel.Controls.Add(saveButton);

            // Load destination info when tab is selected
            tabControl.SelectedIndexChanged += async (s, e) => {
                if (tabControl.SelectedTab == destinationTab)
                {
                    var dest = await dbService.GetDestinationAsync();
                    nameTextBox.Text = dest.Name;
                    timeTextBox.Text = dest.TargetTime;
                    latTextBox.Text = dest.Latitude.ToString();
                    lngTextBox.Text = dest.Longitude.ToString();
                    addressTextBox.Text = dest.Address;

                    // Show on map
                    gMapControl.Position = new GMap.NET.PointLatLng(dest.Latitude, dest.Longitude);
                    gMapControl.Zoom = 15;

                    gMapControl.Overlays.Clear();
                    var overlay = new GMap.NET.WindowsForms.GMapOverlay("destinationMarker");
                    var marker = new GMap.NET.WindowsForms.Markers.GMarkerGoogle(
                        new GMap.NET.PointLatLng(dest.Latitude, dest.Longitude),
                        GMap.NET.WindowsForms.Markers.GMarkerGoogleType.red
                    );
                    overlay.Markers.Add(marker);
                    gMapControl.Overlays.Add(overlay);
                }
            };
        }

        private void SetupSchedulingTab()
        {
            // Scheduling configuration panel
            var panel = ControlExtensions.CreatePanel(
                new Point(10, 50),
                new Size(1140, 660),
                BorderStyle.FixedSingle
            );
            schedulingTab.Controls.Add(panel);

            // Enable scheduling checkbox
            var enabledCheckBox = ControlExtensions.CreateCheckBox(
                "Enable Automatic Scheduling",
                new Point(20, 20),
                new Size(250, 25),
                true
            );
            panel.Controls.Add(enabledCheckBox);

            // Time selection
            panel.Controls.Add(ControlExtensions.CreateLabel(
                "Run Scheduler At:", new Point(20, 60), new Size(150, 25)));

            var timeSelector = new DateTimePicker
            {
                Location = new Point(180, 60),
                Size = new Size(120, 25),
                Format = DateTimePickerFormat.Time,
                ShowUpDown = true,
                Value = DateTime.Parse("00:00:00")
            };
            panel.Controls.Add(timeSelector);

            // Save button
            var saveButton = ControlExtensions.CreateButton(
                "Save Settings",
                new Point(20, 100),
                new Size(120, 30),
                async (s, e) => {
                    try
                    {
                        await dbService.SaveSchedulingSettingsAsync(
                            enabledCheckBox.Checked,
                            timeSelector.Value
                        );

                        MessageBox.Show("Scheduling settings saved successfully.",
                            "Settings Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving settings: {ex.Message}",
                            "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            );
            panel.Controls.Add(saveButton);

            // Run now button
            // First declare the button variable
            var runNowButton = new Button();

            // Initialize properties
            runNowButton.Text = "Run Scheduler Now";
            runNowButton.Location = new Point(150, 100);
            runNowButton.Size = new Size(150, 30);

            // Add to panel
            panel.Controls.Add(runNowButton);

            // AFTER adding to panel, attach the event handler
            runNowButton.Click += async (s, e) => {
                if (MessageBox.Show("Are you sure you want to run the scheduler now? This will calculate routes for tomorrow.",
                        "Confirm Run", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try
                    {
                        runNowButton.Enabled = false;
                        runNowButton.Text = "Running...";

                        // This would trigger the same code that the Windows Service runs
                        // For demonstration purposes, we're calling a placeholder
                        await RunSchedulerAsync();

                        MessageBox.Show("Scheduler completed successfully.",
                            "Scheduler Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error running scheduler: {ex.Message}",
                            "Scheduler Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        runNowButton.Enabled = true;
                        runNowButton.Text = "Run Scheduler Now";
                    }
                }
            };

            // Scheduling history
            panel.Controls.Add(ControlExtensions.CreateLabel(
                "Scheduling History:", new Point(20, 150), new Size(150, 25),
                new Font("Arial", 10, FontStyle.Bold)));

            var historyListView = new ListView
            {
                Location = new Point(20, 180),
                Size = new Size(1100, 460),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            historyListView.Columns.Add("Date", 150);
            historyListView.Columns.Add("Status", 100);
            historyListView.Columns.Add("Routes Generated", 150);
            historyListView.Columns.Add("Passengers Assigned", 150);
            historyListView.Columns.Add("Run Time", 200);
            panel.Controls.Add(historyListView);

            // Load scheduling settings when tab is selected
            tabControl.SelectedIndexChanged += async (s, e) => {
                if (tabControl.SelectedTab == schedulingTab)
                {
                    var settings = await dbService.GetSchedulingSettingsAsync();
                    enabledCheckBox.Checked = settings.IsEnabled;
                    timeSelector.Value = settings.ScheduledTime;

                    // Would also load scheduling history here in a real implementation
                }
            };
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
            try
            {
                // Get all users from the database
                users = await dbService.GetAllUsersAsync();

                // Log the success
                Console.WriteLine($"Loaded {users.Count} users from database");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading users: {ex.Message}",
                    "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // If database loading fails, fall back to sample data for testing purposes
                users = new List<(int Id, string Username, string UserType, string Name, string Email, string Phone)>
                {
                    (1, "admin", "Admin", "Administrator", "", ""),
                    (2, "driver1", "Driver", "John Driver", "", ""),
                    (3, "passenger1", "Passenger", "Alice Passenger", "", "")
                };
            }
        }

        private async Task LoadVehiclesAsync()
        {
            try
            {
                // In a real implementation, this would be:
                vehicles = await dbService.GetAllVehiclesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading vehicles: {ex.Message}",
                    "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Fallback to empty list
                vehicles = new List<Vehicle>();
            }
        }

        private async Task LoadPassengersAsync()
        {
            try
            {
                // In a real implementation, this would be:
                passengers = await dbService.GetAllPassengersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading passengers: {ex.Message}",
                    "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Fallback to empty list
                passengers = new List<Passenger>();
            }
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
                item.SubItems.Add(user.Email);
                item.SubItems.Add(user.Phone);

                listView.Items.Add(item);
            }
        }

        private async Task DisplayDriversAsync(ListView listView)
        {
            listView.Items.Clear();

            if (vehicles == null)
                return;

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
                item.SubItems.Add(passenger.UserId.ToString());

                listView.Items.Add(item);
            }
        }

        private void DisplayVehiclesOnMap(GMap.NET.WindowsForms.GMapControl mapControl)
        {
            if (vehicles == null || mapControl == null) return;

            mapControl.Overlays.Clear();
            var overlay = new GMap.NET.WindowsForms.GMapOverlay("vehicles");

            foreach (var vehicle in vehicles)
            {
                var marker = MapOverlays.CreateVehicleMarker(vehicle);
                overlay.Markers.Add(marker);
            }

            // Add destination marker
            var destMarker = MapOverlays.CreateDestinationMarker(destinationLat, destinationLng);
            overlay.Markers.Add(destMarker);

            mapControl.Overlays.Add(overlay);

            // Center map on first vehicle or destination
            if (vehicles.Any())
            {
                var firstVehicle = vehicles.First();
                mapControl.Position = new GMap.NET.PointLatLng(
                    firstVehicle.StartLatitude, firstVehicle.StartLongitude);
            }
            else
            {
                mapControl.Position = new GMap.NET.PointLatLng(destinationLat, destinationLng);
            }

            mapControl.Zoom = 12;
        }

        private void DisplayPassengersOnMap(GMap.NET.WindowsForms.GMapControl mapControl)
        {
            if (passengers == null || mapControl == null) return;

            mapControl.Overlays.Clear();
            var overlay = new GMap.NET.WindowsForms.GMapOverlay("passengers");

            foreach (var passenger in passengers)
            {
                var marker = MapOverlays.CreatePassengerMarker(passenger);
                overlay.Markers.Add(marker);
            }

            // Add destination marker
            var destMarker = MapOverlays.CreateDestinationMarker(destinationLat, destinationLng);
            overlay.Markers.Add(destMarker);

            mapControl.Overlays.Add(overlay);

            // Center map on first passenger or destination
            if (passengers.Any())
            {
                var firstPassenger = passengers.First();
                mapControl.Position = new GMap.NET.PointLatLng(
                    firstPassenger.Latitude, firstPassenger.Longitude);
            }
            else
            {
                mapControl.Position = new GMap.NET.PointLatLng(destinationLat, destinationLng);
            }

            mapControl.Zoom = 12;
        }

        private async Task LoadRoutesForDateAsync(DateTime date)
        {
            try
            {
                string dateString = date.ToString("yyyy-MM-dd");
                currentSolution = await dbService.GetSolutionForDateAsync(dateString);

                if (currentSolution == null || currentSolution.Vehicles.Count == 0)
                {
                    MessageBox.Show($"No routes found for {date.ToShortDateString()}.",
                        "No Routes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Display routes on map
                var routesPanel = routesTab.Controls.OfType<Panel>().FirstOrDefault();
                var gMapControl = routesPanel?.Controls.OfType<GMap.NET.WindowsForms.GMapControl>().FirstOrDefault();
                var routesListView = routesPanel?.Controls.OfType<ListView>().FirstOrDefault();

                if (gMapControl != null)
                {
                    routingService.DisplaySolutionOnMap(gMapControl, currentSolution);
                }

                // Display routes in list
                if (routesListView != null)
                {
                    routesListView.Items.Clear();

                    foreach (var vehicle in currentSolution.Vehicles.Where(v => v.AssignedPassengers.Count > 0))
                    {
                        var item = new ListViewItem(vehicle.Id.ToString());
                        item.SubItems.Add(vehicle.DriverName ?? $"Driver {vehicle.Id}");
                        item.SubItems.Add(vehicle.AssignedPassengers.Count.ToString());
                        item.SubItems.Add($"{vehicle.TotalDistance:F2} km");
                        routesListView.Items.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading routes: {ex.Message}",
                    "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DisplayRouteDetails(int vehicleId)
        {
            if (currentSolution == null) return;

            var vehicle = currentSolution.Vehicles.FirstOrDefault(v => v.Id == vehicleId);
            if (vehicle == null) return;

            routeDetailsTextBox.Clear();
            routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
            routeDetailsTextBox.AppendText($"Route Details for Vehicle {vehicleId}\n\n");
            routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;

            routeDetailsTextBox.AppendText($"Driver: {vehicle.DriverName ?? $"Driver {vehicleId}"}\n");
            routeDetailsTextBox.AppendText($"Vehicle Capacity: {vehicle.Capacity}\n");
            routeDetailsTextBox.AppendText($"Total Distance: {vehicle.TotalDistance:F2} km\n");
            routeDetailsTextBox.AppendText($"Total Time: {vehicle.TotalTime:F2} minutes\n\n");

            routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
            routeDetailsTextBox.AppendText("Pickup Order:\n");
            routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;

            if (vehicle.AssignedPassengers.Count == 0)
            {
                routeDetailsTextBox.AppendText("No passengers assigned to this vehicle.\n");
                return;
            }

            for (int i = 0; i < vehicle.AssignedPassengers.Count; i++)
            {
                var passenger = vehicle.AssignedPassengers[i];
                routeDetailsTextBox.AppendText($"{i + 1}. {passenger.Name}\n");

                string location = !string.IsNullOrEmpty(passenger.Address)
                    ? passenger.Address
                    : $"({passenger.Latitude:F4}, {passenger.Longitude:F4})";
                routeDetailsTextBox.AppendText($"   Pickup at: {location}\n");

                if (!string.IsNullOrEmpty(passenger.EstimatedPickupTime))
                {
                    routeDetailsTextBox.AppendText($"   Estimated pickup time: {passenger.EstimatedPickupTime}\n");
                }

                routeDetailsTextBox.AppendText("\n");
            }
        }

        private RichTextBox routeDetailsTextBox;

        #endregion

        #region Helper Methods

        private void ShowVehicleEditForm(int vehicleId)
        {
            // This would be implemented to show a form for editing vehicle details
            MessageBox.Show("Vehicle edit functionality would be implemented here.",
                "Not Implemented", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowPassengerEditForm(int passengerId)
        {
            // This would be implemented to show a form for editing passenger details
            MessageBox.Show("Passenger edit functionality would be implemented here.",
                "Not Implemented", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task RunSchedulerAsync()
        {
            // Placeholder for running the scheduler
            // This would be connected to the actual scheduling algorithm in a real implementation
            await Task.Delay(2000); // Simulate processing

            // In a real implementation, this would:
            // 1. Get the destination info
            // 2. Get all available vehicles and passengers for tomorrow
            // 3. Run the ride-sharing algorithm
            // 4. Save the solution for tomorrow's date
            // 5. Optionally send notifications to users
        }

        private void AdminForm_Load(object sender, EventArgs e)
        {
            // Load initial data
            Task.Run(async () =>
            {
                await LoadAllDataAsync();

                // Update UI on main thread
                if (this.IsHandleCreated)
                {
                    this.Invoke(new Action(async () =>
                    {
                        await DisplayUsersAsync(usersListView);
                    }));
                }
            });
        }

        #endregion
    }

    /// <summary>
    /// Form for editing user information
    /// </summary>
    public class UserEditForm : Form
    {
        private readonly DatabaseService dbService;
        private readonly int userId;
        private TextBox nameTextBox;
        private TextBox emailTextBox;
        private TextBox phoneTextBox;
        private ComboBox userTypeComboBox;
        private Button saveButton;
        private Button cancelButton;


        public UserEditForm(DatabaseService dbService, int userId, string username, string userType, string name, string email, string phone)
        {
            this.dbService = dbService;
            this.userId = userId;

            InitializeComponent();
            SetupUI(username, userType, name, email, phone);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "Edit User";
            this.Size = new Size(400, 300);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            this.ResumeLayout(false);
        }

        private void SetupUI(string username, string userType, string name, string email, string phone)
        {
            int y = 20;
            int labelWidth = 120;
            int inputWidth = 230;
            int spacing = 30;

            // Username (read-only)
            Controls.Add(ControlExtensions.CreateLabel("Username:", new Point(20, y), new Size(labelWidth, 20)));
            var usernameTextBox = ControlExtensions.CreateTextBox(new Point(140, y), new Size(inputWidth, 20), username, false, true);
            Controls.Add(usernameTextBox);
            y += spacing;

            // User Type
            Controls.Add(ControlExtensions.CreateLabel("User Type:", new Point(20, y), new Size(labelWidth, 20)));
            userTypeComboBox = ControlExtensions.CreateComboBox(
                new Point(140, y),
                new Size(inputWidth, 20),
                new string[] { "Admin", "Driver", "Passenger" }
            );
            userTypeComboBox.SelectedItem = userType;
            Controls.Add(userTypeComboBox);
            y += spacing;

            // Name
            Controls.Add(ControlExtensions.CreateLabel("Name:", new Point(20, y), new Size(labelWidth, 20)));
            nameTextBox = ControlExtensions.CreateTextBox(new Point(140, y), new Size(inputWidth, 20), name);
            Controls.Add(nameTextBox);
            y += spacing;

            // Email
            Controls.Add(ControlExtensions.CreateLabel("Email:", new Point(20, y), new Size(labelWidth, 20)));
            emailTextBox = ControlExtensions.CreateTextBox(new Point(140, y), new Size(inputWidth, 20), email);
            Controls.Add(emailTextBox);
            y += spacing;

            // Phone
            Controls.Add(ControlExtensions.CreateLabel("Phone:", new Point(20, y), new Size(labelWidth, 20)));
            phoneTextBox = ControlExtensions.CreateTextBox(new Point(140, y), new Size(inputWidth, 20), phone);
            Controls.Add(phoneTextBox);
            y += spacing + 10;

            // Buttons
            saveButton = ControlExtensions.CreateButton(
                "Save Changes",
                new Point(140, y),
                new Size(110, 30),
                async (s, e) => await SaveChangesAsync()
            );
            Controls.Add(saveButton);

            cancelButton = ControlExtensions.CreateButton(
                "Cancel",
                new Point(260, y),
                new Size(110, 30),
                (s, e) => this.DialogResult = DialogResult.Cancel
            );
            Controls.Add(cancelButton);

            // Set enter key to trigger save
            AcceptButton = saveButton;
        }

        private async Task SaveChangesAsync()
        {
            if (string.IsNullOrWhiteSpace(nameTextBox.Text))
            {
                MessageBox.Show("Name cannot be empty.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                saveButton.Enabled = false;

                // Update user in database
                bool success = await dbService.UpdateUserProfileAsync(
                    userId,
                    userTypeComboBox.SelectedItem.ToString(),
                    nameTextBox.Text,
                    emailTextBox.Text,
                    phoneTextBox.Text
                );

                if (success)
                {
                    DialogResult = DialogResult.OK;
                    MessageBox.Show("User updated successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                }
                else
                {
                    MessageBox.Show("Failed to update user. The user may not exist.", "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating user: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                saveButton.Enabled = true;
            }
        }
    }
}