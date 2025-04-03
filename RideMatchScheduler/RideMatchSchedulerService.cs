using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Timers;
using System.Threading.Tasks;
using System.IO;
using claudpro.Models;
using claudpro.Services;
using System.Configuration;

namespace RideMatchScheduler
{
    public class RideMatchSchedulerService : ServiceBase
    {
        private Timer schedulerTimer;
        private readonly DatabaseService dbService;
        private readonly MapService mapService;
        private readonly string logFilePath = "RideMatchScheduler.log";

        public RideMatchSchedulerService()
        {
            InitializeComponent();

            // Initialize services
            string dbPath = ConfigurationManager.AppSettings["DatabasePath"] ?? "ridematch.db";
            string apiKey = ConfigurationManager.AppSettings["GoogleApiKey"] ?? "AIzaSyA8gY0PbmE1EgDjxd-SdIMWWTaQf9Mi7vc";

            dbService = new DatabaseService(dbPath);
            mapService = new MapService(apiKey);

            // Set service name
            ServiceName = "RideMatchSchedulerService";
        }

        private void InitializeComponent()
        {
            // Service component initialization
            this.CanStop = true;
            this.CanPauseAndContinue = false;
            this.AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            Log("RideMatch Scheduler Service started");

            // Set timer to check every minute if it's time to run the algorithm
            schedulerTimer = new Timer(60000); // 60,000 ms = 1 minute
            schedulerTimer.Elapsed += CheckScheduleTime;
            schedulerTimer.Start();
        }

        protected override void OnStop()
        {
            Log("RideMatch Scheduler Service stopped");
            schedulerTimer?.Stop();
            schedulerTimer?.Dispose();
            dbService?.Dispose();
        }

        private async void CheckScheduleTime(object sender, ElapsedEventArgs e)
        {
            try
            {
                // Get current time
                DateTime now = DateTime.Now;

                // Get the scheduled time from database
                var settings = await dbService.GetSchedulingSettingsAsync();

                // Check if scheduling is enabled and if it's the correct time
                if (settings.IsEnabled &&
                    now.Hour == settings.ScheduledTime.Hour &&
                    now.Minute == settings.ScheduledTime.Minute)
                {
                    Log($"Running scheduled route calculation at {now}");
                    await RunAlgorithmAsync();
                }
            }
            catch (Exception ex)
            {
                Log($"Error in scheduler: {ex.Message}");
            }
        }

        private async Task RunAlgorithmAsync()
        {
            try
            {
                // Get destination information
                var destination = await dbService.GetDestinationAsync();

                // Get available vehicles and passengers
                var vehicles = await dbService.GetAvailableVehiclesAsync();
                var passengers = await dbService.GetAvailablePassengersAsync();

                // Only run if there are passengers and vehicles
                if (passengers.Count > 0 && vehicles.Count > 0)
                {
                    Log($"Running algorithm with {passengers.Count} passengers and {vehicles.Count} vehicles");

                    // Create a routing service
                    var routingService = new RoutingService(mapService, destination.Latitude, destination.Longitude);

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
                    var solution = solver.Solve(150); // Generations

                    // Calculate route details
                    routingService.CalculateEstimatedRouteDetails(solution);

                    // Save the solution to database for tomorrow's date
                    string tomorrowDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");
                    int routeId = await dbService.SaveSolutionAsync(solution, tomorrowDate);

                    Log($"Algorithm completed and saved as route #{routeId}");

                    // You could add here code to send notifications to users
                }
                else
                {
                    Log("No passengers or vehicles available for tomorrow - skipping algorithm run");
                }
            }
            catch (Exception ex)
            {
                Log($"Error running algorithm: {ex.Message}");
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

        private void Log(string message)
        {
            try
            {
                using (StreamWriter writer = File.AppendText(logFilePath))
                {
                    writer.WriteLine($"{DateTime.Now}: {message}");
                }
            }
            catch
            {
                // Logging should never crash the service
            }
        }
    }
}