using RideMatchProject.Services;
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
    /// Controller for the Scheduling tab
    /// </summary>
    public class SchedulingTabController : TabControllerBase
    {
        private CheckBox _enabledCheckBox;
        private DateTimePicker _timeSelector;
        private ListView _historyListView;
        private Button _saveButton;
        private Button _runNowButton;
        private Button _refreshHistoryButton;

        public SchedulingTabController(
            DatabaseService dbService,
            MapService mapService,
            AdminDataManager dataManager)
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
            var panel = AdminUIFactory.CreatePanel(
                new Point(10, 50),
                new Size(1140, 660),
                BorderStyle.FixedSingle
            );

            // Enable scheduling checkbox
            _enabledCheckBox = AdminUIFactory.CreateCheckBox(
                "Enable Automatic Scheduling",
                new Point(20, 20),
                new Size(250, 25),
                false
            );
            panel.Controls.Add(_enabledCheckBox);

            // Time selection
            panel.Controls.Add(AdminUIFactory.CreateLabel(
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

 

            // History section
            CreateHistorySection(panel);

            // Add panel to tab
            tabPage.Controls.Add(panel);
        }

        private void CreateActionButtons(Panel panel)
        {
            // Save button
            _saveButton = AdminUIFactory.CreateButton(
                "Save Settings",
                new Point(20, 100),
                new Size(120, 30),
                SaveButtonClick
            );
            panel.Controls.Add(_saveButton);

            // Run now button
            _runNowButton = AdminUIFactory.CreateButton(
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
            panel.Controls.Add(AdminUIFactory.CreateLabel(
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
            _refreshHistoryButton = AdminUIFactory.CreateButton(
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
}
