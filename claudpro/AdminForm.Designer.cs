using claudpro.UI;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace claudpro
{
    partial class AdminForm
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
            // AdminForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Name = "AdminForm";
            this.Text = "AdminForm";
            this.Load += new System.EventHandler(this.AdminForm_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private void AdminForm_Load(object sender, EventArgs e)
        {
            // Initialization code for AdminForm
        }



        private void SetupDestinationTab()
        {
            // Create destination information panel
            var destinationInfoPanel = ControlExtensions.CreatePanel(
                new Point(10, 50),
                new Size(800, 200),
                BorderStyle.FixedSingle
            );
            destinationTab.Controls.Add(destinationInfoPanel);

            // Title
            destinationInfoPanel.Controls.Add(ControlExtensions.CreateLabel(
                "Destination Information",
                new Point(10, 10),
                new Size(300, 20),
                new Font("Arial", 10, FontStyle.Bold)
            ));

            // Add labels and text boxes for destination details
            destinationInfoPanel.Controls.Add(ControlExtensions.CreateLabel("Name:", new Point(10, 40), new Size(100, 20)));
            var nameTextBox = ControlExtensions.CreateTextBox(new Point(120, 40), new Size(200, 20), destinationName);
            destinationInfoPanel.Controls.Add(nameTextBox);

            destinationInfoPanel.Controls.Add(ControlExtensions.CreateLabel("Latitude:", new Point(10, 70), new Size(100, 20)));
            var latTextBox = ControlExtensions.CreateTextBox(new Point(120, 70), new Size(200, 20), destinationLat.ToString());
            destinationInfoPanel.Controls.Add(latTextBox);

            destinationInfoPanel.Controls.Add(ControlExtensions.CreateLabel("Longitude:", new Point(10, 100), new Size(100, 20)));
            var lngTextBox = ControlExtensions.CreateTextBox(new Point(120, 100), new Size(200, 20), destinationLng.ToString());
            destinationInfoPanel.Controls.Add(lngTextBox);

            destinationInfoPanel.Controls.Add(ControlExtensions.CreateLabel("Address:", new Point(10, 130), new Size(100, 20)));
            var addressTextBox = ControlExtensions.CreateTextBox(new Point(120, 130), new Size(400, 20), destinationAddress);
            destinationInfoPanel.Controls.Add(addressTextBox);

            destinationInfoPanel.Controls.Add(ControlExtensions.CreateLabel("Target Time:", new Point(10, 160), new Size(100, 20)));
            var timeTextBox = ControlExtensions.CreateTextBox(new Point(120, 160), new Size(200, 20), destinationTargetTime);
            destinationInfoPanel.Controls.Add(timeTextBox);

            // Save button
            var saveDestButton = ControlExtensions.CreateButton(
                "Save Destination",
                new Point(120, 190),
                new Size(150, 30),
                async (s, e) => {
                    try
                    {
                        double lat = double.Parse(latTextBox.Text);
                        double lng = double.Parse(lngTextBox.Text);
                        string name = nameTextBox.Text;
                        string address = addressTextBox.Text;
                        string targetTime = timeTextBox.Text;

                        bool success = await dbService.UpdateDestinationAsync(name, lat, lng, targetTime, address);

                        if (success)
                        {
                            destinationLat = lat;
                            destinationLng = lng;
                            destinationName = name;
                            destinationAddress = address;
                            destinationTargetTime = targetTime;

                            MessageBox.Show("Destination information saved successfully.", "Success",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show("Failed to save destination information.", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving destination: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            );
            destinationInfoPanel.Controls.Add(saveDestButton);

            // Add scheduling panel
            var schedulingPanel = ControlExtensions.CreatePanel(
                new Point(10, 260),
                new Size(800, 200),
                BorderStyle.FixedSingle
            );
            destinationTab.Controls.Add(schedulingPanel);

            // Title
            schedulingPanel.Controls.Add(ControlExtensions.CreateLabel(
                "Automatic Scheduling",
                new Point(10, 10),
                new Size(300, 20),
                new Font("Arial", 10, FontStyle.Bold)
            ));

            // Enable scheduling checkbox
            var enableSchedulingCheckBox = ControlExtensions.CreateCheckBox(
                "Enable automatic route calculation daily",
                new Point(10, 40),
                new Size(300, 20),
                false
            );
            schedulingPanel.Controls.Add(enableSchedulingCheckBox);

            // Time picker
            schedulingPanel.Controls.Add(ControlExtensions.CreateLabel("Time to run algorithm:", new Point(10, 70), new Size(150, 20)));
            var timePickerPanel = new Panel { Location = new Point(160, 70), Size = new Size(160, 20) };

            var hourUpDown = ControlExtensions.CreateNumericUpDown(new Point(0, 0), new Size(50, 20), 0, 23, 0);
            timePickerPanel.Controls.Add(hourUpDown);

            timePickerPanel.Controls.Add(ControlExtensions.CreateLabel(":", new Point(55, 0), new Size(10, 20)));

            var minuteUpDown = ControlExtensions.CreateNumericUpDown(new Point(70, 0), new Size(50, 20), 0, 59, 0);
            timePickerPanel.Controls.Add(minuteUpDown);

            schedulingPanel.Controls.Add(timePickerPanel);

            // Save button
            var saveSchedulingButton = ControlExtensions.CreateButton(
                "Save Scheduling Settings",
                new Point(10, 110),
                new Size(200, 30),
                async (s, e) => {
                    try
                    {
                        DateTime scheduledTime = DateTime.Today.AddHours((double)hourUpDown.Value).AddMinutes((double)minuteUpDown.Value);

                        await dbService.SaveSchedulingSettingsAsync(
                            enableSchedulingCheckBox.Checked,
                            scheduledTime
                        );

                        MessageBox.Show("Scheduling settings saved successfully.", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving scheduling settings: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            );
            schedulingPanel.Controls.Add(saveSchedulingButton);

            // Load settings
            destinationTab.Enter += async (s, e) => {
                try
                {
                    var settings = await dbService.GetSchedulingSettingsAsync();
                    enableSchedulingCheckBox.Checked = settings.IsEnabled;
                    hourUpDown.Value = settings.ScheduledTime.Hour;
                    minuteUpDown.Value = settings.ScheduledTime.Minute;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading settings: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
        }
    }
}