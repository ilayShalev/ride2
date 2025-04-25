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
    /// Service for passenger-related database operations
    /// </summary>
    public class PassengerService
    {
        private readonly DatabaseManager _dbManager;
        private readonly SQLiteConnection _connection;

        public PassengerService(DatabaseManager dbManager)
        {
            _dbManager = dbManager ?? throw new ArgumentNullException(nameof(dbManager));
            _connection = dbManager.GetConnection();
        }

        public async Task<int> AddPassengerAsync(int userId, string name,
            double latitude, double longitude, string address = "")
        {
            var parameters = new Dictionary<string, object>
            {
                { "@UserID", userId },
                { "@Name", name },
                { "@Latitude", latitude },
                { "@Longitude", longitude },
                { "@Address", address ?? "" }
            };

            string query = @"
                INSERT INTO Passengers (UserID, Name, Latitude, Longitude, Address)
                VALUES (@UserID, @Name, @Latitude, @Longitude, @Address);
                SELECT last_insert_rowid();";

            var passengerId = await _dbManager.ExecuteScalarAsync<int>(query, parameters);
            return passengerId > 0 ? passengerId : -1;
        }

        public async Task<bool> UpdatePassengerAsync(int passengerId, string name,
            double latitude, double longitude, string address = "")
        {
            var parameters = new Dictionary<string, object>
            {
                { "@PassengerID", passengerId },
                { "@Name", name },
                { "@Latitude", latitude },
                { "@Longitude", longitude },
                { "@Address", address ?? "" }
            };

            string query = @"
                UPDATE Passengers
                SET Name = @Name, Latitude = @Latitude, Longitude = @Longitude, Address = @Address
                WHERE PassengerID = @PassengerID";

            int rowsAffected = await _dbManager.ExecuteNonQueryAsync(query, parameters);
            return rowsAffected > 0;
        }

        public async Task<bool> UpdatePassengerAvailabilityAsync(int passengerId, bool isAvailable)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@PassengerID", passengerId },
                { "@IsAvailable", isAvailable ? 1 : 0 }
            };

            string query = "UPDATE Passengers SET IsAvailableTomorrow = @IsAvailable WHERE PassengerID = @PassengerID";

            int rowsAffected = await _dbManager.ExecuteNonQueryAsync(query, parameters);
            return rowsAffected > 0;
        }

        public async Task<List<Passenger>> GetAvailablePassengersAsync()
        {
            string query = @"
                SELECT PassengerID, Name, Latitude, Longitude, Address, EstimatedPickupTime
                FROM Passengers
                WHERE IsAvailableTomorrow = 1";

            return await _dbManager.ExecuteReaderAsync<Passenger>(
                query,
                async reader => new Passenger
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Latitude = reader.GetDouble(2),
                    Longitude = reader.GetDouble(3),
                    Address = reader.IsDBNull(4) ? null : reader.GetString(4),
                    EstimatedPickupTime = reader.IsDBNull(5) ? null : reader.GetString(5),
                    IsAvailableTomorrow = true
                },
                null
            );
        }

        public async Task<List<Passenger>> GetAllPassengersAsync()
        {
            string query = @"
                SELECT PassengerID, UserID, Name, Latitude, Longitude, Address, 
                       IsAvailableTomorrow, EstimatedPickupTime
                FROM Passengers";

            return await _dbManager.ExecuteReaderAsync<Passenger>(
                query,
                async reader => new Passenger
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Name = reader.GetString(2),
                    Latitude = reader.GetDouble(3),
                    Longitude = reader.GetDouble(4),
                    Address = reader.IsDBNull(5) ? null : reader.GetString(5),
                    IsAvailableTomorrow = reader.GetInt32(6) == 1,
                    EstimatedPickupTime = reader.IsDBNull(7) ? null : reader.GetString(7)
                },
                null
            );
        }

        public async Task<Passenger> GetPassengerByUserIdAsync(int userId)
        {
            var parameters = new Dictionary<string, object> { { "@UserID", userId } };

            string query = @"
                SELECT PassengerID, Name, Latitude, Longitude, Address, IsAvailableTomorrow, EstimatedPickupTime
                FROM Passengers
                WHERE UserID = @UserID";

            return await _dbManager.ExecuteReaderSingleAsync<Passenger>(
                query,
                async reader => new Passenger
                {
                    Id = reader.GetInt32(0),
                    UserId = userId,
                    Name = reader.GetString(1),
                    Latitude = reader.GetDouble(2),
                    Longitude = reader.GetDouble(3),
                    Address = reader.IsDBNull(4) ? null : reader.GetString(4),
                    IsAvailableTomorrow = reader.GetInt32(5) == 1,
                    EstimatedPickupTime = reader.IsDBNull(6) ? null : reader.GetString(6)
                },
                parameters
            );
        }

        public async Task<Passenger> GetPassengerByIdAsync(int passengerId)
        {
            var parameters = new Dictionary<string, object> { { "@PassengerID", passengerId } };

            string query = @"
                SELECT PassengerID, UserID, Name, Latitude, Longitude, Address, 
                       IsAvailableTomorrow, EstimatedPickupTime
                FROM Passengers
                WHERE PassengerID = @PassengerID";

            return await _dbManager.ExecuteReaderSingleAsync<Passenger>(
                query,
                async reader => new Passenger
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Name = reader.GetString(2),
                    Latitude = reader.GetDouble(3),
                    Longitude = reader.GetDouble(4),
                    Address = reader.IsDBNull(5) ? null : reader.GetString(5),
                    IsAvailableTomorrow = reader.GetInt32(6) == 1,
                    EstimatedPickupTime = reader.IsDBNull(7) ? null : reader.GetString(7)
                },
                parameters
            );
        }

        public async Task<bool> DeletePassengerAsync(int passengerId)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    await DeletePassengerAssignmentsAsync(passengerId, transaction);
                    bool deleted = await DeletePassengerRecordAsync(passengerId, transaction);

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

        private async Task DeletePassengerAssignmentsAsync(int passengerId, SQLiteTransaction transaction)
        {
            string query = "DELETE FROM PassengerAssignments WHERE PassengerID = @PassengerID";

            using (var cmd = new SQLiteCommand(query, _connection, transaction))
            {
                cmd.Parameters.AddWithValue("@PassengerID", passengerId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task<bool> DeletePassengerRecordAsync(int passengerId, SQLiteTransaction transaction)
        {
            string query = "DELETE FROM Passengers WHERE PassengerID = @PassengerID";

            using (var cmd = new SQLiteCommand(query, _connection, transaction))
            {
                cmd.Parameters.AddWithValue("@PassengerID", passengerId);
                int result = await cmd.ExecuteNonQueryAsync();
                return result > 0;
            }
        }
    }
}
