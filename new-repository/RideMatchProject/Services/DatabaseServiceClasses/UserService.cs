using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.DatabaseServiceClasses
{
    /// <summary>
    /// Service for user-related database operations
    /// </summary>
    public class UserService
    {
        private readonly DatabaseManager _dbManager;
        private readonly SQLiteConnection _connection;
        private readonly SecurityHelper _securityHelper;

        public UserService(DatabaseManager dbManager)
        {
            _dbManager = dbManager ?? throw new ArgumentNullException(nameof(dbManager));
            _connection = dbManager.GetConnection();
            _securityHelper = new SecurityHelper();
        }

        public async Task<(bool Success, string UserType, int UserId)> AuthenticateUserAsync(
            string username, string password)
        {
            string hashedPassword = _securityHelper.HashPassword(password);

            var parameters = new Dictionary<string, object>
            {
                { "@Username", username },
                { "@Password", hashedPassword }
            };

            string query = @"
                SELECT UserID, UserType 
                FROM Users 
                WHERE Username = @Username AND Password = @Password";

            var result = await _dbManager.ExecuteReaderSingleAsync<(bool, string, int)>(
                query,
                async reader => (true, reader.GetString(1), reader.GetInt32(0)),
                parameters
            );

            return result.Item1 ? result : (false, "", 0);
        }

        public async Task<int> AddUserAsync(string username, string password,
            string userType, string name, string email = "", string phone = "")
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    int userId = await InsertUserAsync(username, password, userType, name, email, phone, transaction);

                    if (userId > 0 && userType.ToLower() == "driver")
                    {
                        await CreateDefaultVehicleAsync(userId, transaction);
                    }

                    transaction.Commit();
                    return userId;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private async Task<int> InsertUserAsync(string username, string password,
            string userType, string name, string email, string phone, SQLiteTransaction transaction)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@Username", username },
                { "@Password", _securityHelper.HashPassword(password) },
                { "@UserType", userType },
                { "@Name", name },
                { "@Email", email ?? "" },
                { "@Phone", phone ?? "" }
            };

            string query = @"
                INSERT INTO Users (Username, Password, UserType, Name, Email, Phone)
                VALUES (@Username, @Password, @UserType, @Name, @Email, @Phone);
                SELECT last_insert_rowid();";

            using (var cmd = new SQLiteCommand(query, _connection, transaction))
            {
                foreach (var param in parameters)
                {
                    cmd.Parameters.AddWithValue(param.Key, param.Value);
                }

                object result = await cmd.ExecuteScalarAsync();
                if (result != null && int.TryParse(result.ToString(), out int userId))
                {
                    return userId;
                }
            }

            return -1;
        }

        private async Task CreateDefaultVehicleAsync(int userId, SQLiteTransaction transaction)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@UserID", userId },
                { "@Capacity", 4 },
                { "@StartLatitude", 0 },
                { "@StartLongitude", 0 }
            };

            string query = @"
                INSERT INTO Vehicles (UserID, Capacity, StartLatitude, StartLongitude, IsAvailableTomorrow)
                VALUES (@UserID, @Capacity, @StartLatitude, @StartLongitude, 1)";

            using (var cmd = new SQLiteCommand(query, _connection, transaction))
            {
                foreach (var param in parameters)
                {
                    cmd.Parameters.AddWithValue(param.Key, param.Value);
                }

                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<bool> UpdateUserAsync(int userId, string name, string email, string phone)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@UserID", userId },
                { "@Name", name },
                { "@Email", email ?? "" },
                { "@Phone", phone ?? "" }
            };

            string query = @"
                UPDATE Users
                SET Name = @Name, Email = @Email, Phone = @Phone
                WHERE UserID = @UserID";

            int rowsAffected = await _dbManager.ExecuteNonQueryAsync(query, parameters);
            return rowsAffected > 0;
        }

        public async Task<bool> UpdateUserProfileAsync(int userId, string userType,
            string name, string email, string phone)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@UserID", userId },
                { "@UserType", userType },
                { "@Name", name },
                { "@Email", email ?? "" },
                { "@Phone", phone ?? "" }
            };

            string query = @"
                UPDATE Users 
                SET UserType = @UserType, Name = @Name, Email = @Email, Phone = @Phone
                WHERE UserID = @UserID";

            int rowsAffected = await _dbManager.ExecuteNonQueryAsync(query, parameters);
            return rowsAffected > 0;
        }

        public async Task<bool> ChangePasswordAsync(int userId, string newPassword)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@UserID", userId },
                { "@Password", _securityHelper.HashPassword(newPassword) }
            };

            string query = "UPDATE Users SET Password = @Password WHERE UserID = @UserID";

            int rowsAffected = await _dbManager.ExecuteNonQueryAsync(query, parameters);
            return rowsAffected > 0;
        }

        public async Task<(string Username, string UserType, string Name, string Email, string Phone)>
            GetUserInfoAsync(int userId)
        {
            var parameters = new Dictionary<string, object> { { "@UserID", userId } };

            string query = @"
                SELECT Username, UserType, Name, Email, Phone 
                FROM Users 
                WHERE UserID = @UserID";

            return await _dbManager.ExecuteReaderSingleAsync<(string, string, string, string, string)>(
                query,
                async reader => (
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? "" : reader.GetString(3),
                    reader.IsDBNull(4) ? "" : reader.GetString(4)
                ),
                parameters
            );
        }

        public async Task<List<(int Id, string Username, string UserType, string Name, string Email, string Phone)>>
            GetAllUsersAsync()
        {
            string query = @"
                SELECT UserID, Username, UserType, Name, Email, Phone 
                FROM Users 
                ORDER BY UserType, Username";

            return await _dbManager.ExecuteReaderAsync<(int, string, string, string, string, string)>(
                query,
                async reader => (
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? "" : reader.GetString(4),
                    reader.IsDBNull(5) ? "" : reader.GetString(5)
                ),
                null
            );
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    await DeleteVehicleForUserAsync(userId, transaction);
                    await DeletePassengerForUserAsync(userId, transaction);
                    bool deleted = await DeleteUserRecordAsync(userId, transaction);

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

        private async Task DeleteVehicleForUserAsync(int userId, SQLiteTransaction transaction)
        {
            var parameters = new Dictionary<string, object> { { "@UserID", userId } };
            string query = "DELETE FROM Vehicles WHERE UserID = @UserID";

            using (var cmd = new SQLiteCommand(query, _connection, transaction))
            {
                cmd.Parameters.AddWithValue("@UserID", userId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task DeletePassengerForUserAsync(int userId, SQLiteTransaction transaction)
        {
            var parameters = new Dictionary<string, object> { { "@UserID", userId } };
            string query = "DELETE FROM Passengers WHERE UserID = @UserID";

            using (var cmd = new SQLiteCommand(query, _connection, transaction))
            {
                cmd.Parameters.AddWithValue("@UserID", userId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task<bool> DeleteUserRecordAsync(int userId, SQLiteTransaction transaction)
        {
            var parameters = new Dictionary<string, object> { { "@UserID", userId } };
            string query = "DELETE FROM Users WHERE UserID = @UserID";

            using (var cmd = new SQLiteCommand(query, _connection, transaction))
            {
                cmd.Parameters.AddWithValue("@UserID", userId);
                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        public async Task<List<(int Id, string Username, string Name)>> GetUsersByTypeAsync(string userType)
        {
            var parameters = new Dictionary<string, object> { { "@UserType", userType } };

            string query = @"
                SELECT UserID, Username, Name
                FROM Users
                WHERE UserType = @UserType
                ORDER BY Username";

            return await _dbManager.ExecuteReaderAsync<(int, string, string)>(
                query,
                async reader => (
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2)
                ),
                parameters
            );
        }
    }
}
