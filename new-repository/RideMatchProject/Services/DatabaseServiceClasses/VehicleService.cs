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
    /// Service for vehicle-related database operations
    /// </summary>
    public class VehicleService
    {
        private readonly DatabaseManager _dbManager;
        private readonly SQLiteConnection _connection;

        public VehicleService(DatabaseManager dbManager)
        {
            _dbManager = dbManager ?? throw new ArgumentNullException(nameof(dbManager));
            _connection = dbManager.GetConnection();
        }

        public async Task<int> AddVehicleAsync(int userId, int capacity,
            double startLatitude, double startLongitude, string startAddress = "")
        {
            var parameters = new Dictionary<string, object>
            {
                { "@UserID", userId },
                { "@Capacity", capacity },
                { "@StartLatitude", startLatitude },
                { "@StartLongitude", startLongitude },
                { "@StartAddress", startAddress ?? "" }
            };

            string query = @"
                INSERT INTO Vehicles (UserID, Capacity, StartLatitude, StartLongitude, StartAddress)
                VALUES (@UserID, @Capacity, @StartLatitude, @StartLongitude, @StartAddress);
                SELECT last_insert_rowid();";

            var vehicleId = await _dbManager.ExecuteScalarAsync<int>(query, parameters);
            return vehicleId > 0 ? vehicleId : -1;
        }

        public async Task<bool> UpdateVehicleAsync(int vehicleId, int capacity,
            double startLatitude, double startLongitude, string startAddress = "")
        {
            var parameters = new Dictionary<string, object>
            {
                { "@VehicleID", vehicleId },
                { "@Capacity", capacity },
                { "@StartLatitude", startLatitude },
                { "@StartLongitude", startLongitude },
                { "@StartAddress", startAddress ?? "" }
            };

            string query = @"
                UPDATE Vehicles
                SET Capacity = @Capacity, StartLatitude = @StartLatitude, 
                    StartLongitude = @StartLongitude, StartAddress = @StartAddress
                WHERE VehicleID = @VehicleID";

            int rowsAffected = await _dbManager.ExecuteNonQueryAsync(query, parameters);
            return rowsAffected > 0;
        }

        public async Task<bool> UpdateVehicleAvailabilityAsync(int vehicleId, bool isAvailable)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@VehicleID", vehicleId },
                { "@IsAvailable", isAvailable ? 1 : 0 }
            };

            string query = "UPDATE Vehicles SET IsAvailableTomorrow = @IsAvailable WHERE VehicleID = @VehicleID";

            int rowsAffected = await _dbManager.ExecuteNonQueryAsync(query, parameters);
            return rowsAffected > 0;
        }

        public async Task<List<Vehicle>> GetAllVehiclesAsync()
        {
            string query = @"
                SELECT v.VehicleID, v.UserID, v.Capacity, v.StartLatitude, v.StartLongitude, 
                       v.StartAddress, v.IsAvailableTomorrow, v.DepartureTime, u.Name
                FROM Vehicles v
                LEFT JOIN Users u ON v.UserID = u.UserID";

            return await _dbManager.ExecuteReaderAsync<Vehicle>(
                query,
                async reader => new Vehicle
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Capacity = reader.GetInt32(2),
                    StartLatitude = reader.GetDouble(3),
                    StartLongitude = reader.GetDouble(4),
                    StartAddress = reader.IsDBNull(5) ? null : reader.GetString(5),
                    IsAvailableTomorrow = reader.GetInt32(6) == 1,
                    DepartureTime = reader.IsDBNull(7) ? null : reader.GetString(7),
                    DriverName = reader.IsDBNull(8) ? null : reader.GetString(8),
                    AssignedPassengers = new List<Passenger>()
                },
                null
            );
        }

        public async Task<List<Vehicle>> GetAvailableVehiclesAsync()
        {
            string query = @"
                SELECT v.VehicleID, v.UserID, v.Capacity, v.StartLatitude, v.StartLongitude, 
                       v.StartAddress, v.DepartureTime, u.Name
                FROM Vehicles v
                JOIN Users u ON v.UserID = u.UserID
                WHERE v.IsAvailableTomorrow = 1";

            return await _dbManager.ExecuteReaderAsync<Vehicle>(
                query,
                async reader => new Vehicle
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Capacity = reader.GetInt32(2),
                    StartLatitude = reader.GetDouble(3),
                    StartLongitude = reader.GetDouble(4),
                    StartAddress = reader.IsDBNull(5) ? null : reader.GetString(5),
                    DepartureTime = reader.IsDBNull(6) ? null : reader.GetString(6),
                    DriverName = reader.GetString(7),
                    AssignedPassengers = new List<Passenger>(),
                    IsAvailableTomorrow = true
                },
                null
            );
        }

        public async Task<Vehicle> GetVehicleByUserIdAsync(int userId)
        {
            var parameters = new Dictionary<string, object> { { "@UserID", userId } };

            string query = @"
                SELECT VehicleID, Capacity, StartLatitude, StartLongitude, StartAddress, 
                       IsAvailableTomorrow, DepartureTime
                FROM Vehicles
                WHERE UserID = @UserID";

            return await _dbManager.ExecuteReaderSingleAsync<Vehicle>(
                query,
                async reader => new Vehicle
                {
                    Id = reader.GetInt32(0),
                    UserId = userId,
                    Capacity = reader.GetInt32(1),
                    StartLatitude = reader.GetDouble(2),
                    StartLongitude = reader.GetDouble(3),
                    StartAddress = reader.IsDBNull(4) ? null : reader.GetString(4),
                    IsAvailableTomorrow = reader.GetInt32(5) == 1,
                    DepartureTime = reader.IsDBNull(6) ? null : reader.GetString(6),
                    AssignedPassengers = new List<Passenger>()
                },
                parameters
            );
        }

        public async Task<Vehicle> GetVehicleByIdAsync(int vehicleId)
        {
            var parameters = new Dictionary<string, object> { { "@VehicleID", vehicleId } };

            string query = @"
                SELECT v.VehicleID, v.UserID, v.Capacity, v.StartLatitude, v.StartLongitude, 
                       v.StartAddress, v.IsAvailableTomorrow, v.DepartureTime, u.Name
                FROM Vehicles v
                LEFT JOIN Users u ON v.UserID = u.UserID
                WHERE v.VehicleID = @VehicleID";

            return await _dbManager.ExecuteReaderSingleAsync<Vehicle>(
                query,
                async reader => new Vehicle
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Capacity = reader.GetInt32(2),
                    StartLatitude = reader.GetDouble(3),
                    StartLongitude = reader.GetDouble(4),
                    StartAddress = reader.IsDBNull(5) ? null : reader.GetString(5),
                    IsAvailableTomorrow = reader.GetInt32(6) == 1,
                    DepartureTime = reader.IsDBNull(7) ? null : reader.GetString(7),
                    DriverName = reader.IsDBNull(8) ? null : reader.GetString(8),
                    AssignedPassengers = new List<Passenger>()
                },
                parameters
            );
        }

        public async Task<int> SaveDriverVehicleAsync(int userId, int capacity,
            double startLatitude, double startLongitude, string startAddress = "")
        {
            var existingVehicle = await GetVehicleByUserIdAsync(userId);

            if (existingVehicle != null)
            {
                await UpdateVehicleAsync(existingVehicle.Id, capacity, startLatitude,
                    startLongitude, startAddress);
                return existingVehicle.Id;
            }
            else
            {
                return await AddVehicleAsync(userId, capacity, startLatitude, startLongitude, startAddress);
            }
        }

        public async Task<bool> DeleteVehicleAsync(int vehicleId)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    await DeletePassengerAssignmentsForVehicleAsync(vehicleId, transaction);
                    await DeleteRouteDetailsForVehicleAsync(vehicleId, transaction);
                    bool deleted = await DeleteVehicleRecordAsync(vehicleId, transaction);

                    if (deleted)
                    {
                        transaction.Commit();
                        return true;
                    }
                    else
                    {
                        transaction.Rollback();
                        return false;
                    }
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private async Task DeletePassengerAssignmentsForVehicleAsync(int vehicleId, SQLiteTransaction transaction)
        {
            string query = @"
                DELETE FROM PassengerAssignments
                WHERE RouteDetailID IN (
                    SELECT RouteDetailID FROM RouteDetails
                    WHERE VehicleID = @VehicleID
                )";

            using (var cmd = new SQLiteCommand(query, _connection, transaction))
            {
                cmd.Parameters.AddWithValue("@VehicleID", vehicleId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task DeleteRouteDetailsForVehicleAsync(int vehicleId, SQLiteTransaction transaction)
        {
            string query = "DELETE FROM RouteDetails WHERE VehicleID = @VehicleID";

            using (var cmd = new SQLiteCommand(query, _connection, transaction))
            {
                cmd.Parameters.AddWithValue("@VehicleID", vehicleId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task<bool> DeleteVehicleRecordAsync(int vehicleId, SQLiteTransaction transaction)
        {
            string query = "DELETE FROM Vehicles WHERE VehicleID = @VehicleID";

            using (var cmd = new SQLiteCommand(query, _connection, transaction))
            {
                cmd.Parameters.AddWithValue("@VehicleID", vehicleId);
                int result = await cmd.ExecuteNonQueryAsync();
                return result > 0;
            }
        }
    }

}
