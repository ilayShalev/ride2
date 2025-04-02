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
        private RoutingService routingService;
        private RideSharingGenetic algorithmService;

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
            // Implementation details preserved for brevity
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

            // Additional implementation details omitted
        }

        private void SetupPassengersTab()
        {
            // Implementation details preserved for brevity
            // Create basic UI elements for this tab
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
        }

        private void SetupRoutesTab()
        {
            // Implementation details preserved for brevity
            // Create basic UI elements for the routes tab
            var routesPanel = ControlExtensions.CreatePanel(
                new Point(10, 50),
                new Size(1100, 600),
                BorderStyle.FixedSingle
            );
            routesTab.Controls.Add(routesPanel);
        }

        private void SetupDestinationTab()
        {
            // Implementation details preserved for brevity
            // Create basic UI elements for the destination tab
            var destinationPanel = ControlExtensions.CreatePanel(
                new Point(10, 50),
                new Size(1100, 600),
                BorderStyle.FixedSingle
            );
            destinationTab.Controls.Add(destinationPanel);
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

        // Other methods omitted for brevity
        #endregion
    }
}