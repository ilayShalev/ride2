using GMap.NET;
using RideMatchProject.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.DatabaseServiceClasses
{
    /// <summary>
    /// Service for route-related database operations
    /// </summary>
    public class RouteService
    {
        private readonly DatabaseManager _dbManager;
        private readonly SQLiteConnection _connection;

        public RouteService(DatabaseManager dbManager)
        {
            _dbManager = dbManager ?? throw new ArgumentNullException(nameof(dbManager));
            _connection = dbManager.GetConnection();
        }

        public async Task<(Vehicle AssignedVehicle, DateTime? PickupTime)> GetPassengerAssignmentAsync(
           int userId, string date)
        {
            var passengerService = new PassengerService(_dbManager);
            var passenger = await passengerService.GetPassengerByUserIdAsync(userId);

            if (passenger == null)
            {
                return (null, null);
            }

            int routeId = await GetRouteIdForDateAsync(date);

            if (routeId <= 0)
            {
                return (null, null);
            }

            var assignment = await FindPassengerAssignmentAsync(routeId, passenger.Id);

            if (assignment.VehicleId <= 0)
            {
                return (null, null);
            }

            string pickupTime = assignment.PickupTime ?? passenger.EstimatedPickupTime;
            var vehicle = await GetAssignedVehicleAsync(assignment.VehicleId);

            DateTime? pickupDateTime = null;
            if (!string.IsNullOrEmpty(pickupTime))
            {
                pickupDateTime = DateTime.Parse(pickupTime);
            }

            return (vehicle, pickupDateTime);
        }

        public async Task<(Vehicle Vehicle, List<Passenger> Passengers, DateTime? PickupTime)>
            GetDriverRouteAsync(int userId, string date)
        {
            var vehicleService = new VehicleService(_dbManager);
            var vehicle = await vehicleService.GetVehicleByUserIdAsync(userId);

            if (vehicle == null)
            {
                return (null, null, null);
            }

            int routeId = await GetRouteIdForDateAsync(date);

            if (routeId <= 0)
            {
                return (vehicle, new List<Passenger>(), null);
            }

            var routeDetail = await GetRouteDetailForVehicleAsync(routeId, vehicle.Id);

            if (routeDetail.Item1 <= 0)
            {
                return (vehicle, new List<Passenger>(), null);
            }

            vehicle.TotalDistance = routeDetail.Item2;
            vehicle.TotalTime = routeDetail.Item3;
            vehicle.DepartureTime = routeDetail.Item4;

            var passengers = await GetPassengersForRouteDetailAsync(routeDetail.Item1);
            vehicle.AssignedPassengers = passengers;

            DateTime? firstPickupTime = CalculateFirstPickupTime(passengers, vehicle.DepartureTime);

            return (vehicle, passengers, firstPickupTime);
        }

        private async Task<(int VehicleId, string PickupTime)> FindPassengerAssignmentAsync(
            int routeId, int passengerId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@RouteID", routeId },
                { "@PassengerID", passengerId }
            };

            string query = @"
                SELECT rd.VehicleID, pa.EstimatedPickupTime
                FROM PassengerAssignments pa
                JOIN RouteDetails rd ON pa.RouteDetailID = rd.RouteDetailID
                WHERE rd.RouteID = @RouteID AND pa.PassengerID = @PassengerID";

            return await _dbManager.ExecuteReaderSingleAsync<(int, string)>(
                query,
                async reader => (
                    reader.GetInt32(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1)
                ),
                parameters
            );
        }

        private async Task<Vehicle> GetAssignedVehicleAsync(int vehicleId)
        {
            var parameters = new Dictionary<string, object> { { "@VehicleID", vehicleId } };

            string query = @"
                SELECT v.VehicleID, v.Capacity, v.StartLatitude, v.StartLongitude, v.StartAddress, 
                       v.DepartureTime, u.Name
                FROM Vehicles v
                JOIN Users u ON v.UserID = u.UserID
                WHERE v.VehicleID = @VehicleID";

            return await _dbManager.ExecuteReaderSingleAsync<Vehicle>(
                query,
                async reader => new Vehicle
                {
                    Id = reader.GetInt32(0),
                    Capacity = reader.GetInt32(1),
                    StartLatitude = reader.GetDouble(2),
                    StartLongitude = reader.GetDouble(3),
                    StartAddress = reader.IsDBNull(4) ? null : reader.GetString(4),
                    DepartureTime = reader.IsDBNull(5) ? null : reader.GetString(5),
                    DriverName = reader.GetString(6),
                    Model = "Standard Vehicle",
                    Color = "White",
                    LicensePlate = $"V-{vehicleId:D4}"
                },
                parameters
            );
        }

        public async Task<int> SaveSolutionAsync(Solution solution, string date)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    int routeId = await InsertRouteAsync(date, transaction);

                    if (routeId <= 0)
                    {
                        transaction.Rollback();
                        return -1;
                    }

                    foreach (var vehicle in solution.Vehicles)
                    {
                        if (vehicle.AssignedPassengers.Count == 0)
                        {
                            continue;
                        }

                        await SaveVehicleRouteAsync(routeId, vehicle, transaction);
                    }

                    transaction.Commit();
                    return routeId;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private async Task<int> InsertRouteAsync(string date, SQLiteTransaction transaction)
        {
            string query = @"
                INSERT INTO Routes (SolutionDate)
                VALUES (@SolutionDate);
                SELECT last_insert_rowid();";

            using (var cmd = new SQLiteCommand(query, _connection, transaction))
            {
                cmd.Parameters.AddWithValue("@SolutionDate", date);
                object result = await cmd.ExecuteScalarAsync();

                if (result != null && int.TryParse(result.ToString(), out int routeId))
                {
                    return routeId;
                }

                return -1;
            }
        }

        private async Task SaveVehicleRouteAsync(int routeId, Vehicle vehicle, SQLiteTransaction transaction)
        {
            Console.WriteLine($"Starting to save route for vehicle {vehicle.Id} with {vehicle.AssignedPassengers?.Count ?? 0} passengers");

            try
            {
                // Step 1: Save route details
                int routeDetailId = await InsertRouteDetailAsync(routeId, vehicle, transaction);
                Console.WriteLine($"Saved route detail with ID {routeDetailId} for vehicle {vehicle.Id}");

                if (routeDetailId <= 0)
                {
                    Console.WriteLine($"ERROR: Failed to insert route details for vehicle {vehicle.Id}");
                    return;
                }

                // Step 2: Update vehicle departure time
                await UpdateVehicleDepartureTimeAsync(vehicle.Id, vehicle.DepartureTime, transaction);
                Console.WriteLine($"Updated departure time to {vehicle.DepartureTime} for vehicle {vehicle.Id}");

                // Step 3: Save passenger assignments
                if (vehicle.AssignedPassengers != null)
                {
                    for (int i = 0; i < vehicle.AssignedPassengers.Count; i++)
                    {
                        var passenger = vehicle.AssignedPassengers[i];
                        await SavePassengerAssignmentAsync(routeDetailId, passenger, i + 1, transaction);
                        await UpdatePassengerPickupTimeAsync(passenger.Id, passenger.EstimatedPickupTime, transaction);
                        Console.WriteLine($"Saved passenger {passenger.Id} assignment with pickup time {passenger.EstimatedPickupTime}");
                    }
                }

                // Step 4: Save route path if available
                if (vehicle.RoutePath != null && vehicle.RoutePath.Count > 0)
                {
                    Console.WriteLine($"Found {vehicle.RoutePath.Count} path points for vehicle {vehicle.Id} - saving to database");
                    await SaveRoutePathAsync(routeDetailId, vehicle.RoutePath, transaction);
                }
                else
                {
                    Console.WriteLine($"WARNING: No route path available for vehicle {vehicle.Id}. Direct line will be used instead of road routes.");

                    // Optionally create empty path to prevent future attempts to fetch it
                    //await SaveRoutePathAsync(routeDetailId, new List<PointLatLng>(), transaction);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in SaveVehicleRouteAsync for vehicle {vehicle.Id}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                // Re-throw to allow transaction to be rolled back by calling method
                throw;
            }
        }

        private async Task SaveRoutePathAsync(int routeDetailId, List<PointLatLng> routePath, SQLiteTransaction transaction)
        {
            try
            {
                // Delete existing path points first to avoid duplicates
                string deleteQuery = "DELETE FROM RoutePathPoints WHERE RouteDetailID = @RouteDetailID";
                using (var cmd = new SQLiteCommand(deleteQuery, _connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@RouteDetailID", routeDetailId);
                    int deletedRows = await cmd.ExecuteNonQueryAsync();
                    Console.WriteLine($"Deleted {deletedRows} existing path points for route detail {routeDetailId}");
                }

                // If we have no points or an empty list, just log and return
                if (routePath == null || routePath.Count == 0)
                {
                    Console.WriteLine($"No path points to save for route detail {routeDetailId}");
                    return;
                }

                // Insert new path points
                string insertQuery = @"
            INSERT INTO RoutePathPoints (RouteDetailID, PointOrder, Latitude, Longitude)
            VALUES (@RouteDetailID, @PointOrder, @Latitude, @Longitude)";

                int insertedCount = 0;
                for (int i = 0; i < routePath.Count; i++)
                {
                    using (var cmd = new SQLiteCommand(insertQuery, _connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@RouteDetailID", routeDetailId);
                        cmd.Parameters.AddWithValue("@PointOrder", i);
                        cmd.Parameters.AddWithValue("@Latitude", routePath[i].Lat);
                        cmd.Parameters.AddWithValue("@Longitude", routePath[i].Lng);

                        await cmd.ExecuteNonQueryAsync();
                        insertedCount++;
                    }
                }

                Console.WriteLine($"Successfully inserted {insertedCount} path points for route detail {routeDetailId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR saving route path for route detail {routeDetailId}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw; // Re-throw to allow transaction to be rolled back
            }
        }

        private async Task<int> InsertRouteDetailAsync(int routeId, Vehicle vehicle, SQLiteTransaction transaction)
        {
            string query = @"
                INSERT INTO RouteDetails (RouteID, VehicleID, TotalDistance, TotalTime, DepartureTime)
                VALUES (@RouteID, @VehicleID, @TotalDistance, @TotalTime, @DepartureTime);
                SELECT last_insert_rowid();";

            using (var cmd = new SQLiteCommand(query, _connection, transaction))
            {
                cmd.Parameters.AddWithValue("@RouteID", routeId);
                cmd.Parameters.AddWithValue("@VehicleID", vehicle.Id);
                cmd.Parameters.AddWithValue("@TotalDistance", vehicle.TotalDistance);
                cmd.Parameters.AddWithValue("@TotalTime", vehicle.TotalTime);
                cmd.Parameters.AddWithValue("@DepartureTime",
                    vehicle.DepartureTime != null ? (object)vehicle.DepartureTime : DBNull.Value);

                object result = await cmd.ExecuteScalarAsync();

                if (result != null && int.TryParse(result.ToString(), out int routeDetailId))
                {
                    return routeDetailId;
                }

                return -1;
            }
        }

        private async Task UpdateVehicleDepartureTimeAsync(int vehicleId, string departureTime,
            SQLiteTransaction transaction)
        {
            string query = @"
                UPDATE Vehicles 
                SET DepartureTime = @DepartureTime
                WHERE VehicleID = @VehicleID";

            using (var cmd = new SQLiteCommand(query, _connection, transaction))
            {
                cmd.Parameters.AddWithValue("@VehicleID", vehicleId);
                cmd.Parameters.AddWithValue("@DepartureTime",
                    departureTime != null ? (object)departureTime : DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task SavePassengerAssignmentAsync(int routeDetailId, Passenger passenger,
            int stopOrder, SQLiteTransaction transaction)
        {
            string query = @"
                INSERT INTO PassengerAssignments (RouteDetailID, PassengerID, StopOrder, EstimatedPickupTime)
                VALUES (@RouteDetailID, @PassengerID, @StopOrder, @EstimatedPickupTime)";

            using (var cmd = new SQLiteCommand(query, _connection, transaction))
            {
                cmd.Parameters.AddWithValue("@RouteDetailID", routeDetailId);
                cmd.Parameters.AddWithValue("@PassengerID", passenger.Id);
                cmd.Parameters.AddWithValue("@StopOrder", stopOrder);
                cmd.Parameters.AddWithValue("@EstimatedPickupTime",
                    !string.IsNullOrEmpty(passenger.EstimatedPickupTime) ?
                    (object)passenger.EstimatedPickupTime : DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task UpdatePassengerPickupTimeAsync(int passengerId, string pickupTime,
            SQLiteTransaction transaction)
        {
            string query = @"
                UPDATE Passengers 
                SET EstimatedPickupTime = @EstimatedPickupTime
                WHERE PassengerID = @PassengerID";

            using (var cmd = new SQLiteCommand(query, _connection, transaction))
            {
                cmd.Parameters.AddWithValue("@PassengerID", passengerId);
                cmd.Parameters.AddWithValue("@EstimatedPickupTime",
                    !string.IsNullOrEmpty(pickupTime) ? (object)pickupTime : DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<Solution> GetSolutionForDateAsync(string date)
        {
            int routeId = await GetRouteIdForDateAsync(date);

            if (routeId <= 0)
            {
                return null;
            }

            var solution = new Solution { Vehicles = new List<Vehicle>() };
            var routeDetails = await GetRouteDetailsAsync(routeId);

            if (routeDetails.Count == 0)
            {
                return null;
            }

            var vehicles = await LoadVehiclesForRouteAsync(routeDetails);
            solution.Vehicles = vehicles;

            return solution;
        }

        private async Task<int> GetRouteIdForDateAsync(string date)
        {
            var parameters = new Dictionary<string, object> { { "@SolutionDate", date } };

            string query = @"
                SELECT RouteID 
                FROM Routes 
                WHERE SolutionDate = @SolutionDate 
                ORDER BY GeneratedTime DESC 
                LIMIT 1";

            return await _dbManager.ExecuteScalarAsync<int>(query, parameters);
        }

        private async Task<Dictionary<int, (int VehicleId, double TotalDistance, double TotalTime, string DepartureTime)>>
            GetRouteDetailsAsync(int routeId)
        {
            var parameters = new Dictionary<string, object> { { "@RouteID", routeId } };

            string query = @"
                SELECT RouteDetailID, VehicleID, TotalDistance, TotalTime, DepartureTime 
                FROM RouteDetails 
                WHERE RouteID = @RouteID";

            var routeDetails = await _dbManager.ExecuteReaderAsync<(int, int, double, double, string)>(
                query,
                async reader => (
                    reader.GetInt32(0),
                    reader.GetInt32(1),
                    reader.GetDouble(2),
                    reader.GetDouble(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4)
                ),
                parameters
            );

            var result = new Dictionary<int, (int, double, double, string)>();

            foreach (var detail in routeDetails)
            {
                result[detail.Item1] = (detail.Item2, detail.Item3, detail.Item4, detail.Item5);
            }

            return result;
        }

        private async Task<List<Vehicle>> LoadVehiclesForRouteAsync(
            Dictionary<int, (int VehicleId, double TotalDistance, double TotalTime, string DepartureTime)> routeDetails)
        {
            var vehicleService = new VehicleService(_dbManager);
            var vehicles = await vehicleService.GetAllVehiclesAsync();
            var vehicleMap = vehicles.ToDictionary(v => v.Id);
            var result = new List<Vehicle>();

            foreach (var detail in routeDetails)
            {
                int vehicleId = detail.Value.VehicleId;

                if (!vehicleMap.TryGetValue(vehicleId, out Vehicle vehicle))
                {
                    continue;
                }

                vehicle.TotalDistance = detail.Value.TotalDistance;
                vehicle.TotalTime = detail.Value.TotalTime;
                vehicle.DepartureTime = detail.Value.DepartureTime;

                await LoadPassengersForVehicleAsync(detail.Key, vehicle);
                result.Add(vehicle);
            }

            return result;
        }

        private async Task LoadPassengersForVehicleAsync(int routeDetailId, Vehicle vehicle)
        {
            var parameters = new Dictionary<string, object> { { "@RouteDetailID", routeDetailId } };

            string query = @"
                SELECT pa.PassengerID, pa.StopOrder, pa.EstimatedPickupTime, 
                       p.Name, p.Latitude, p.Longitude, p.Address
                FROM PassengerAssignments pa
                JOIN Passengers p ON pa.PassengerID = p.PassengerID
                WHERE pa.RouteDetailID = @RouteDetailID
                ORDER BY pa.StopOrder";

            var passengers = await _dbManager.ExecuteReaderAsync<Passenger>(
                query,
                async reader => new Passenger
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(3),
                    Latitude = reader.GetDouble(4),
                    Longitude = reader.GetDouble(5),
                    Address = reader.IsDBNull(6) ? null : reader.GetString(6),
                    EstimatedPickupTime = reader.IsDBNull(2) ? null : reader.GetString(2)
                },
                parameters
            );

            vehicle.AssignedPassengers = passengers;
            await LoadRoutePathAsync(routeDetailId, vehicle);
        }
        private async Task LoadRoutePathAsync(int routeDetailId, Vehicle vehicle)
        {
            var parameters = new Dictionary<string, object> { { "@RouteDetailID", routeDetailId } };

            string query = @"
                SELECT Latitude, Longitude
                FROM RoutePathPoints
                WHERE RouteDetailID = @RouteDetailID
                ORDER BY PointOrder";

            var points = await _dbManager.ExecuteReaderAsync<PointLatLng>(
                query,
                async reader => new PointLatLng(
                    reader.GetDouble(0),
                    reader.GetDouble(1)
                ),
                parameters
            );

            vehicle.RoutePath = points;
        }

        private async Task<(int RouteDetailId, double TotalDistance, double TotalTime, string DepartureTime)>
            GetRouteDetailForVehicleAsync(int routeId, int vehicleId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@RouteID", routeId },
                { "@VehicleID", vehicleId }
            };

            string query = @"
                SELECT RouteDetailID, TotalDistance, TotalTime, DepartureTime
                FROM RouteDetails 
                WHERE RouteID = @RouteID AND VehicleID = @VehicleID";

            return await _dbManager.ExecuteReaderSingleAsync<(int, double, double, string)>(
                query,
                async reader => (
                    reader.GetInt32(0),
                    reader.GetDouble(1),
                    reader.GetDouble(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3)
                ),
                parameters
            );
        }

        private async Task<List<Passenger>> GetPassengersForRouteDetailAsync(int routeDetailId)
        {
            var parameters = new Dictionary<string, object> { { "@RouteDetailID", routeDetailId } };

            string query = @"
                SELECT pa.PassengerID, pa.StopOrder, pa.EstimatedPickupTime, 
                       p.Name, p.Latitude, p.Longitude, p.Address
                FROM PassengerAssignments pa
                JOIN Passengers p ON pa.PassengerID = p.PassengerID
                WHERE pa.RouteDetailID = @RouteDetailID
                ORDER BY pa.StopOrder";

            return await _dbManager.ExecuteReaderAsync<Passenger>(
                query,
                async reader => new Passenger
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(3),
                    Latitude = reader.GetDouble(4),
                    Longitude = reader.GetDouble(5),
                    Address = reader.IsDBNull(6) ? null : reader.GetString(6),
                    EstimatedPickupTime = reader.IsDBNull(2) ? null : reader.GetString(2)
                },
                parameters
            );
        }

        private DateTime? CalculateFirstPickupTime(List<Passenger> passengers, string departureTime)
        {
            if (passengers.Count == 0)
            {
                return null;
            }

            // Try to get pickup time from first passenger
            if (!string.IsNullOrEmpty(passengers[0].EstimatedPickupTime))
            {
                try
                {
                    return DateTime.Parse(passengers[0].EstimatedPickupTime);
                }
                catch
                {
                    // Continue if parsing fails
                }
            }

            // If no pickup time available, estimate based on departure time
            if (!string.IsNullOrEmpty(departureTime))
            {
                try
                {
                    DateTime departure = DateTime.Parse(departureTime);
                    DateTime estimatedPickup = departure.AddMinutes(15);

                    if (string.IsNullOrEmpty(passengers[0].EstimatedPickupTime))
                    {
                        passengers[0].EstimatedPickupTime = estimatedPickup.ToString("HH:mm");
                    }

                    return estimatedPickup;
                }
                catch
                {
                    // Continue if parsing fails
                }
            }

            return null;
        }

        public async Task<bool> UpdatePickupTimesAsync(int routeDetailId,
            Dictionary<int, string> passengerPickupTimes)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    foreach (var entry in passengerPickupTimes)
                    {
                        await UpdateAssignmentPickupTimeAsync(routeDetailId, entry.Key, entry.Value, transaction);
                        await UpdatePassengerPickupTimeAsync(entry.Key, entry.Value, transaction);
                    }

                    transaction.Commit();
                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    return false;
                }
            }
        }

        private async Task UpdateAssignmentPickupTimeAsync(int routeDetailId, int passengerId,
            string pickupTime, SQLiteTransaction transaction)
        {
            string query = @"
                UPDATE PassengerAssignments
                SET EstimatedPickupTime = @PickupTime
                WHERE RouteDetailID = @RouteDetailID AND PassengerID = @PassengerID";

            using (var cmd = new SQLiteCommand(query, _connection, transaction))
            {
                cmd.Parameters.AddWithValue("@RouteDetailID", routeDetailId);
                cmd.Parameters.AddWithValue("@PassengerID", passengerId);
                cmd.Parameters.AddWithValue("@PickupTime", pickupTime);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<List<(int RouteId, DateTime GeneratedTime, int VehicleCount, int PassengerCount)>>
            GetRouteHistoryAsync()
        {
            string query = @"
                SELECT r.RouteID, r.SolutionDate, r.GeneratedTime, 
                       COUNT(DISTINCT rd.VehicleID) as VehicleCount,
                       COUNT(pa.PassengerID) as PassengerCount
                FROM Routes r
                LEFT JOIN RouteDetails rd ON r.RouteID = rd.RouteID
                LEFT JOIN PassengerAssignments pa ON rd.RouteDetailID = pa.RouteDetailID
                GROUP BY r.RouteID
                ORDER BY r.SolutionDate DESC, r.GeneratedTime DESC
                LIMIT 50";

            return await _dbManager.ExecuteReaderAsync<(int, DateTime, int, int)>(
                query,
                async reader => (
                    reader.GetInt32(0),
                    DateTime.Parse(reader.GetString(2)),
                    reader.GetInt32(3),
                    reader.GetInt32(4)
                ),
                null
            );
        }

        public async Task ResetAvailabilityAsync()
        {
            string vehiclesQuery = "UPDATE Vehicles SET IsAvailableTomorrow = 1";
            await _dbManager.ExecuteNonQueryAsync(vehiclesQuery, null);

            string passengersQuery = "UPDATE Passengers SET IsAvailableTomorrow = 1";
            await _dbManager.ExecuteNonQueryAsync(passengersQuery, null);
        }

        /// <summary>
        /// Migrates existing routes to include empty path points if needed
        /// </summary>
        public async Task MigrateRoutePathsAsync()
        {
            try
            {
                // Get all route details without path points
                string query = @"
            SELECT rd.RouteDetailID
            FROM RouteDetails rd
            LEFT JOIN (SELECT DISTINCT RouteDetailID FROM RoutePathPoints) rp 
            ON rd.RouteDetailID = rp.RouteDetailID
            WHERE rp.RouteDetailID IS NULL";

                var routeDetailIds = await _dbManager.ExecuteReaderAsync<int>(
                    query,
                    async reader => reader.GetInt32(0),
                    null
                );

                // Create empty path arrays for these routes
                foreach (var routeDetailId in routeDetailIds)
                {
                    await SaveRoutePathAsync(routeDetailId, new List<PointLatLng>(), null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Route migration error: {ex.Message}");
            }
        }


    }

}
