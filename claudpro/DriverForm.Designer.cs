using claudpro.UI;
using GMap.NET;
using GMap.NET.WindowsForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace claudpro
{
    partial class DriverForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // DriverForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Name = "DriverForm";
            this.Text = "DriverForm";
            this.Load += new System.EventHandler(this.DriverForm_Load);
            this.ResumeLayout(false);

        }



        private void DisplayRouteOnMap()
        {
            if (gMapControl == null) return;

            try
            {
                gMapControl.Overlays.Clear();

                if (vehicle == null)
                    return;

                // Create overlays
                var vehiclesOverlay = new GMapOverlay("vehicles");
                var passengersOverlay = new GMapOverlay("passengers");
                var routesOverlay = new GMapOverlay("routes");
                var destinationOverlay = new GMapOverlay("destination");

                // Get destination from database
                Task.Run(async () => {
                    try
                    {
                        var destination = await dbService.GetDestinationAsync();

                        // Add destination marker
                        this.Invoke(new Action(() => {
                            try
                            {
                                var destinationMarker = MapOverlays.CreateDestinationMarker(destination.Latitude, destination.Longitude);
                                destinationOverlay.Markers.Add(destinationMarker);

                                // Show vehicle marker
                                var vehicleMarker = MapOverlays.CreateVehicleMarker(vehicle);
                                vehiclesOverlay.Markers.Add(vehicleMarker);

                                // Show passenger markers
                                if (assignedPassengers != null)
                                {
                                    foreach (var passenger in assignedPassengers)
                                    {
                                        if (passenger != null)
                                        {
                                            var passengerMarker = MapOverlays.CreatePassengerMarker(passenger);
                                            passengersOverlay.Markers.Add(passengerMarker);
                                        }
                                    }
                                }

                                // Show route
                                if (assignedPassengers != null && assignedPassengers.Count > 0)
                                {
                                    var points = new List<PointLatLng>();
                                    points.Add(new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude));

                                    foreach (var passenger in assignedPassengers)
                                    {
                                        if (passenger != null)
                                        {
                                            points.Add(new PointLatLng(passenger.Latitude, passenger.Longitude));
                                        }
                                    }

                                    points.Add(new PointLatLng(destination.Latitude, destination.Longitude));

                                    var routeColor = Color.Blue;
                                    var route = MapOverlays.CreateRoute(points, "Route", routeColor);
                                    routesOverlay.Routes.Add(route);
                                }

                                // Add overlays to map in the correct order
                                gMapControl.Overlays.Add(routesOverlay);
                                gMapControl.Overlays.Add(vehiclesOverlay);
                                gMapControl.Overlays.Add(passengersOverlay);
                                gMapControl.Overlays.Add(destinationOverlay);

                                // Center map on vehicle location
                                gMapControl.Position = new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude);
                                gMapControl.Zoom = 13;
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Error displaying route: {ex.Message}", "Map Display Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }));
                    }
                    catch (Exception ex)
                    {
                        this.Invoke(new Action(() => {
                            MessageBox.Show($"Error loading destination: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }));
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error preparing map display: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        private void DriverForm_Load(object sender, EventArgs e)
        {
            // Initialization code for DriverForm
        }
    }

}