using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Timers;
using System.Threading.Tasks;
using System.IO;
using System.Configuration;
using RideMatchProject.Models;
using RideMatchProject.Services;

namespace RideMatchScheduler
{
    /// <summary>
    /// Main service class responsible for the Windows service lifecycle
    /// </summary>
    public class RideMatchCoordinatorService : ServiceBase
    {
        private readonly ScheduleManager _scheduleManager;
        private readonly ServiceLogger _logger;
        private readonly ServiceConfig _config;

        public RideMatchCoordinatorService()
        {
            _logger = new ServiceLogger(PathUtility.GetLogFilePath("RideMatchCoordinator.log"));
            _config = ConfigurationUtility.LoadServiceConfiguration();

            InitializeServiceSettings();
            _scheduleManager = CreateScheduleManager();

            ServiceName = "RideMatchCoordinatorService";
        }

        private void InitializeServiceSettings()
        {
            CanStop = true;
            CanPauseAndContinue = true;
            CanShutdown = true;
            AutoLog = true;
        }

        private ScheduleManager CreateScheduleManager()
        {
            try
            {
                var databaseService = new DatabaseServiceFactory().Create(_config.DatabasePath);
                var mapService = new MapServiceFactory().Create(_config.ApiKey);
                var routingService = InitializeRoutingService(databaseService, mapService);

                return new ScheduleManager(databaseService, mapService, routingService, _logger);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create schedule manager: {ex.Message}");
                throw;
            }
        }

        private RoutingService InitializeRoutingService(DatabaseService databaseService, MapService mapService)
        {
            try
            {
                var destinationData = databaseService.GetDestinationAsync().GetAwaiter().GetResult();
                var routingService = new RoutingService(mapService, destinationData.Latitude, destinationData.Longitude);
                _logger.LogInfo($"Routing service initialized with destination: {destinationData.Name}");
                return routingService;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Using default routing service: {ex.Message}");
                return new RoutingService(mapService, 0, 0);
            }
        }

        protected override void OnStart(string[] args)
        {
            _logger.LogInfo("RideMatch Coordinator Service starting");

            try
            {
                _scheduleManager.StartScheduling();
                LoadInitialSettings();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Service start failure: {ex.Message}");
                throw;
            }
        }

        private void LoadInitialSettings()
        {
            Task.Run(async () => {
                try
                {
                    var settingsData = await _scheduleManager.GetSchedulingSettingsAsync();
                    _logger.LogInfo($"Service started. Scheduling is {(settingsData.IsEnabled ? "enabled" : "disabled")}. " +
                        $"Scheduled time: {settingsData.ScheduledTime.ToString("HH:mm:ss")}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error loading settings: {ex.Message}");
                }
            });
        }

        protected override void OnStop()
        {
            _logger.LogInfo("RideMatch Coordinator Service stopping");
            _scheduleManager.StopScheduling();
            _logger.LogInfo("RideMatch Coordinator Service stopped");
        }

        protected override void OnPause()
        {
            _scheduleManager.PauseScheduling();
            _logger.LogInfo("RideMatch Coordinator Service paused");
        }

        protected override void OnContinue()
        {
            _scheduleManager.ResumeScheduling();
            _logger.LogInfo("RideMatch Coordinator Service resumed");
        }

        protected override void OnShutdown()
        {
            _logger.LogInfo("System shutdown detected");
            _scheduleManager.StopScheduling();
            _logger.LogInfo("RideMatch Coordinator Service shutdown complete");
        }
    }

    /// <summary>
    /// Manages scheduling and algorithm execution
    /// </summary>
    public class ScheduleManager : IDisposable
    {
        private readonly Timer _schedulerTimer;
        private readonly DatabaseService _dbService;
        private readonly MapService _mapService;
        private readonly RoutingService _routingService;
        private readonly ServiceLogger _logger;
        private readonly AlgorithmRunner _algorithmRunner;
        private bool _isRunningTask = false;
        private bool _disposed = false;

        public ScheduleManager(DatabaseService dbService, MapService mapService,
                              RoutingService routingService, ServiceLogger logger)
        {
            _dbService = dbService;
            _mapService = mapService;
            _routingService = routingService;
            _logger = logger;
            _algorithmRunner = new AlgorithmRunner(_dbService, _routingService, _logger);
            _schedulerTimer = new Timer(60000); // 1 minute interval
            _schedulerTimer.Elapsed += CheckScheduleTime;
        }

        public void StartScheduling()
        {
            _schedulerTimer.Start();
            _logger.LogInfo("Scheduling started");
        }

        public void StopScheduling()
        {
            _schedulerTimer.Stop();
            WaitForRunningTasks();
            Dispose();
            _logger.LogInfo("Scheduling stopped");
        }

        public void PauseScheduling()
        {
            _schedulerTimer.Stop();
            _logger.LogInfo("Scheduling paused");
        }

        public void ResumeScheduling()
        {
            _schedulerTimer.Start();
            _logger.LogInfo("Scheduling resumed");
        }

        public async Task<SchedulingSettings> GetSchedulingSettingsAsync()
        {
            var settingsData = await _dbService.GetSchedulingSettingsAsync();
            return new SchedulingSettings
            {
                IsEnabled = settingsData.IsEnabled,
                ScheduledTime = settingsData.ScheduledTime
            };
        }

        private async void CheckScheduleTime(object sender, ElapsedEventArgs e)
        {
            if (_isRunningTask)
            {
                return;
            }

            try
            {
                _isRunningTask = true;
                DateTime now = DateTime.Now;
                var settingsData = await _dbService.GetSchedulingSettingsAsync();

                var settings = new SchedulingSettings
                {
                    IsEnabled = settingsData.IsEnabled,
                    ScheduledTime = settingsData.ScheduledTime
                };

                bool shouldRunAlgorithm = settings.IsEnabled &&
                                         now.Hour == settings.ScheduledTime.Hour &&
                                         now.Minute == settings.ScheduledTime.Minute;

                if (shouldRunAlgorithm)
                {
                    _logger.LogInfo($"Running scheduled route calculation at {now}");
                    await _algorithmRunner.ExecuteAlgorithmAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Schedule check error: {ex.Message}");
            }
            finally
            {
                _isRunningTask = false;
            }
        }

        private void WaitForRunningTasks()
        {
            int waitCount = 0;
            int maxWaitTime = 30;

            while (_isRunningTask && waitCount < maxWaitTime)
            {
                System.Threading.Thread.Sleep(1000);
                waitCount++;
                _logger.LogInfo($"Waiting for running task to complete... ({waitCount}/{maxWaitTime})");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _schedulerTimer?.Stop();
                    _schedulerTimer?.Dispose();
                    _dbService?.Dispose();
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Runs the ride matching algorithm and processes results
    /// </summary>
    public class AlgorithmRunner
    {
        private readonly DatabaseService _dbService;
        private readonly RoutingService _routingService;
        private readonly ServiceLogger _logger;

        public AlgorithmRunner(DatabaseService dbService, RoutingService routingService, ServiceLogger logger)
        {
            _dbService = dbService;
            _routingService = routingService;
            _logger = logger;
        }

        public async Task ExecuteAlgorithmAsync()
        {
            try
            {
                var destinationData = await _dbService.GetDestinationAsync();
                _logger.LogInfo($"Using destination: {destinationData.Name}, Location: {destinationData.Latitude}, {destinationData.Longitude}");

                var vehicles = await _dbService.GetAvailableVehiclesAsync();
                var passengers = await _dbService.GetAvailablePassengersAsync();

                if (!HasSufficientParticipants(passengers, vehicles))
                {
                    await LogInsufficientParticipants(passengers.Count, vehicles.Count);
                    return;
                }

                await RunRideMatchingAlgorithm(passengers, vehicles, destinationData);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Algorithm execution error: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                await LogAlgorithmFailure(ex.Message);
                throw;
            }
        }

        private bool HasSufficientParticipants(List<Passenger> passengers, List<Vehicle> vehicles)
        {
            return passengers.Count > 0 && vehicles.Count > 0;
        }

        private async Task LogInsufficientParticipants(int passengerCount, int vehicleCount)
        {
            _logger.LogInfo($"No passengers or vehicles available for tomorrow - skipping algorithm run");
            await _dbService.LogSchedulingRunAsync(
                DateTime.Now,
                "Skipped",
                0,
                0,
                $"Insufficient participants: {passengerCount} passengers, {vehicleCount} vehicles"
            );
        }

        private async Task LogAlgorithmFailure(string errorMessage)
        {
            try
            {
                await _dbService.LogSchedulingRunAsync(
                    DateTime.Now,
                    "Error",
                    0,
                    0,
                    errorMessage
                );
            }
            catch
            {
                // Just in case writing to the database also fails
            }
        }

        private async Task RunRideMatchingAlgorithm(List<Passenger> passengers, List<Vehicle> vehicles,
                                          (int Id, string Name, double Latitude, double Longitude, string Address, string TargetTime) destinationData)
        {
            _logger.LogInfo($"Running algorithm with {passengers.Count} passengers and {vehicles.Count} vehicles");

            var solver = new RideSharingGenetic(
                passengers,
                vehicles,
                200, // Population size
                destinationData.Latitude,
                destinationData.Longitude,
                TimeUtility.ConvertTimeStringToMinutes(destinationData.TargetTime)
            );

            var solution = solver.Solve(150); // Generations

            if (solution != null)
            {
                await ProcessAndSaveSolution(solution, destinationData);
            }
            else
            {
                _logger.LogInfo("Algorithm failed to find a valid solution");
                await _dbService.LogSchedulingRunAsync(
                    DateTime.Now,
                    "Failed",
                    0,
                    0,
                    "Algorithm failed to find a valid solution"
                );
            }
        }

        private async Task ProcessAndSaveSolution(Solution solution,
                                      (int Id, string Name, double Latitude, double Longitude, string Address, string TargetTime) destinationData)
        {
            await CalculateRoutes(solution);
            await CalculatePickupTimes(solution, destinationData.TargetTime);

            string tomorrowDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");
            int routeId = await _dbService.SaveSolutionAsync(solution, tomorrowDate);

            int assignedPassengers = solution.Vehicles.Sum(v => v.AssignedPassengers?.Count ?? 0);
            int usedVehicles = solution.Vehicles.Count(v => v.AssignedPassengers?.Count > 0);

            _logger.LogInfo($"Algorithm completed and saved as route #{routeId}");
            _logger.LogInfo($"Assigned {assignedPassengers} passengers to {usedVehicles} vehicles");

            await _dbService.LogSchedulingRunAsync(
                DateTime.Now,
                "Success",
                usedVehicles,
                assignedPassengers,
                $"Created routes for {tomorrowDate}"
            );
        }

        private async Task CalculateRoutes(Solution solution)
        {
            try
            {
                // Always calculate estimated routes as fallback
                _routingService.CalculateEstimatedRouteDetails(solution);

                bool shouldUseGoogleApi = await ShouldUseGoogleRoutesApi();

                if (shouldUseGoogleApi)
                {
                    await TryUseGoogleRoutesApi(solution);
                }
                else
                {
                    _logger.LogInfo("Using estimated routes (Google API disabled in settings)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating routes: {ex.Message}");
            }
        }

        private async Task<bool> ShouldUseGoogleRoutesApi()
        {
            string useGoogleApi = await _dbService.GetSettingAsync("UseGoogleRoutesAPI", "1");
            return useGoogleApi == "1";
        }

        private async Task TryUseGoogleRoutesApi(Solution solution)
        {
            try
            {
                _logger.LogInfo("Fetching routes from Google Maps API...");
                await _routingService.GetGoogleRoutesAsync(null, solution);
                _logger.LogInfo("Successfully retrieved routes from Google Maps API");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Google API request failed: {ex.Message}. Using estimated routes instead.");
            }
        }

        private async Task CalculatePickupTimes(Solution solution, string targetTimeString)
        {
            TimeSpan targetTime = TimeUtility.ParseTimeStringOrDefault(targetTimeString, new TimeSpan(8, 0, 0));
            DateTime targetDateTime = DateTime.Today.Add(targetTime);

            foreach (var vehicle in solution.Vehicles.Where(v => HasAssignedPassengers(v)))
            {
                if (!_routingService.VehicleRouteDetails.TryGetValue(vehicle.Id, out RouteDetails routeDetails))
                {
                    continue;
                }

                SetVehicleDepartureTime(vehicle, targetDateTime, routeDetails.TotalTime);
                await SetPassengerPickupTimes(vehicle, routeDetails);
            }
        }

        private bool HasAssignedPassengers(Vehicle vehicle)
        {
            return vehicle.AssignedPassengers != null && vehicle.AssignedPassengers.Count > 0;
        }

        private void SetVehicleDepartureTime(Vehicle vehicle, DateTime targetTime, double totalTripTimeMinutes)
        {
            DateTime departureTime = targetTime.AddMinutes(-totalTripTimeMinutes);
            vehicle.DepartureTime = departureTime.ToString("HH:mm");
        }

        private Task SetPassengerPickupTimes(Vehicle vehicle, RouteDetails routeDetails)
        {
            DateTime driverStartTime = DateTime.Parse(vehicle.DepartureTime);

            foreach (var passenger in vehicle.AssignedPassengers)
            {
                var stopDetail = routeDetails.StopDetails.FirstOrDefault(s => s.PassengerId == passenger.Id);

                if (stopDetail != null)
                {
                    DateTime pickupTime = driverStartTime.AddMinutes(stopDetail.CumulativeTime);
                    passenger.EstimatedPickupTime = pickupTime.ToString("HH:mm");
                }
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Handles service logging
    /// </summary>
    public class ServiceLogger
    {
        private readonly string _logFilePath;

        public ServiceLogger(string logFilePath)
        {
            _logFilePath = logFilePath;
            EnsureLogDirectoryExists();
        }

        private void EnsureLogDirectoryExists()
        {
            string directory = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public void LogInfo(string message)
        {
            WriteLog(message);
        }

        public void LogWarning(string message)
        {
            WriteLog($"WARNING: {message}");
        }

        public void LogError(string message)
        {
            WriteLog($"ERROR: {message}");
        }

        private void WriteLog(string message)
        {
            try
            {
                lock (this)
                {
                    using (StreamWriter writer = File.AppendText(_logFilePath))
                    {
                        writer.WriteLine($"{DateTime.Now}: {message}");
                    }
                }
            }
            catch
            {
                // Logging should never crash the service
            }
        }
    }

    /// <summary>
    /// Factory for creating database service
    /// </summary>
    public class DatabaseServiceFactory
    {
        public DatabaseService Create(string dbPath)
        {
            string resolvedPath = EnsureAbsolutePath(dbPath);
            EnsureDatabaseDirectoryExists(resolvedPath);
            return new DatabaseService(resolvedPath);
        }

        private string EnsureAbsolutePath(string dbPath)
        {
            if (!Path.IsPathRooted(dbPath))
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbPath);
            }
            return dbPath;
        }

        private void EnsureDatabaseDirectoryExists(string dbPath)
        {
            string dbDirectory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
            }
        }
    }

    /// <summary>
    /// Factory for creating map service
    /// </summary>
    public class MapServiceFactory
    {
        public MapService Create(string apiKey)
        {
            return new MapService(apiKey);
        }
    }

    /// <summary>
    /// Utility class for service configuration
    /// </summary>
    public static class ConfigurationUtility
    {
        public static ServiceConfig LoadServiceConfiguration()
        {
            return new ServiceConfig
            {
                DatabasePath = ConfigurationManager.AppSettings["DatabasePath"] ?? "ridematch.db",
                ApiKey = ConfigurationManager.AppSettings["GoogleApiKey"] ?? ""
            };
        }
    }

    /// <summary>
    /// Utility class for path operations
    /// </summary>
    public static class PathUtility
    {
        public static string GetLogFilePath(string fileName)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        }
    }

    /// <summary>
    /// Utility class for time operations
    /// </summary>
    public static class TimeUtility
    {
        public static int ConvertTimeStringToMinutes(string timeString)
        {
            if (TimeSpan.TryParse(timeString, out TimeSpan time))
            {
                return (int)time.TotalMinutes;
            }
            return 480; // Default to 8:00 AM (480 minutes)
        }

        public static TimeSpan ParseTimeStringOrDefault(string timeString, TimeSpan defaultTime)
        {
            if (TimeSpan.TryParse(timeString, out TimeSpan time))
            {
                return time;
            }
            return defaultTime;
        }
    }

    /// <summary>
    /// Service configuration class
    /// </summary>
    public class ServiceConfig
    {
        public string DatabasePath { get; set; }
        public string ApiKey { get; set; }
    }

    /// <summary>
    /// Scheduling settings class
    /// </summary>
    public class SchedulingSettings
    {
        public bool IsEnabled { get; set; }
        public DateTime ScheduledTime { get; set; }
    }
}