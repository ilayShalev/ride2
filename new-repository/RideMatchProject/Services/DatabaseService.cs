using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using RideMatchProject.Models;

namespace RideMatchProject.Services
{
    /// <summary>
    /// Base database manager class that handles connection and shared functionality
    /// </summary>
    public class DatabaseManager : IDisposable
    {
        protected readonly string _connectionString;
        protected SQLiteConnection _connection;
        private bool _disposed = false;

        public DatabaseManager(string dbFilePath = "ridematch.db")
        {
            bool createNew = !File.Exists(dbFilePath);
            _connectionString = $"Data Source={dbFilePath};Version=3;";

            InitializeConnection();

            if (createNew)
            {
                CreateDatabaseSchema();
            }
            else
            {
                UpdateDatabaseSchema();
            }
        }

        private void InitializeConnection()
        {
            _connection = new SQLiteConnection(_connectionString);
            _connection.Open();
        }

        public SQLiteConnection GetConnection()
        {
            return _connection;
        }

        private void CreateDatabaseSchema()
        {
            var schemaCreator = new DatabaseSchemaCreator(_connection);
            schemaCreator.CreateSchema();

            var defaultDataInserter = new DefaultDataInserter(_connection);
            defaultDataInserter.InsertDefaultData();
        }

        private void UpdateDatabaseSchema()
        {
            var schemaUpdater = new DatabaseSchemaUpdater(_connection);
            schemaUpdater.UpdateSchema();
        }

        public async Task<T> ExecuteScalarAsync<T>(string commandText,
            Dictionary<string, object> parameters = null)
        {
            using (var cmd = CreateCommand(commandText, parameters))
            {
                var result = await cmd.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value)
                {
                    return default;
                }
                return (T)Convert.ChangeType(result, typeof(T));
            }
        }

        public async Task<int> ExecuteNonQueryAsync(string commandText,
            Dictionary<string, object> parameters = null)
        {
            using (var cmd = CreateCommand(commandText, parameters))
            {
                return await cmd.ExecuteNonQueryAsync();
            }
        }

        protected SQLiteCommand CreateCommand(string commandText,
            Dictionary<string, object> parameters = null)
        {
            var cmd = new SQLiteCommand(commandText, _connection);

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                }
            }

            return cmd;
        }

        public async Task<List<T>> ExecuteReaderAsync<T>(string commandText,
            Func<DbDataReader, Task<T>> rowMapper, Dictionary<string, object> parameters = null)
        {
            var results = new List<T>();

            using (var cmd = CreateCommand(commandText, parameters))
            {
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var item = await rowMapper(reader);
                        results.Add(item);
                    }
                }
            }

            return results;
        }

        public async Task<T> ExecuteReaderSingleAsync<T>(string commandText,
            Func<DbDataReader, Task<T>> rowMapper, Dictionary<string, object> parameters = null)
        {
            using (var cmd = CreateCommand(commandText, parameters))
            {
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return await rowMapper(reader);
                    }
                }
            }

            return default;
        }

        protected async Task<bool> TableExistsAsync(string tableName)
        {
            string query = "SELECT name FROM sqlite_master WHERE type='table' AND name=@TableName";
            var parameters = new Dictionary<string, object> { { "@TableName", tableName } };

            var result = await ExecuteScalarAsync<string>(query, parameters);
            return !string.IsNullOrEmpty(result);
        }

        protected async Task<bool> ColumnExistsAsync(string tableName, string columnName)
        {
            string query = $"PRAGMA table_info({tableName})";

            var columnExists = await ExecuteReaderAsync<bool>(
                query,
                async reader => reader.GetString(1) == columnName,
                null
            );

            return columnExists.Any(exists => exists);
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
                    _connection?.Close();
                    _connection?.Dispose();
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Creates database schema for new databases
    /// </summary>
    public class DatabaseSchemaCreator
    {
        private readonly SQLiteConnection _connection;

        public DatabaseSchemaCreator(SQLiteConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public void CreateSchema()
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    CreateUserTables();
                    CreateVehicleTable();
                    CreatePassengerTable();
                    CreateDestinationTable();
                    CreateRouteAndAssignmentTables();
                    CreateSettingsAndLogTables();

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private void CreateUserTables()
        {
            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS Users (
                    UserID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE,
                    Password TEXT NOT NULL,
                    UserType TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Email TEXT,
                    Phone TEXT,
                    CreatedDate TEXT DEFAULT CURRENT_TIMESTAMP
                )");
        }

        private void CreateVehicleTable()
        {
            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS Vehicles (
                    VehicleID INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserID INTEGER NOT NULL UNIQUE,
                    Capacity INTEGER NOT NULL DEFAULT 4,
                    StartLatitude REAL NOT NULL DEFAULT 0,
                    StartLongitude REAL NOT NULL DEFAULT 0,
                    StartAddress TEXT,
                    IsAvailableTomorrow INTEGER DEFAULT 1,
                    DepartureTime TEXT,
                    FOREIGN KEY (UserID) REFERENCES Users(UserID)
                )");
        }

        private void CreatePassengerTable()
        {
            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS Passengers (
                    PassengerID INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserID INTEGER,
                    Name TEXT NOT NULL,
                    Latitude REAL NOT NULL,
                    Longitude REAL NOT NULL,
                    Address TEXT,
                    IsAvailableTomorrow INTEGER DEFAULT 1,
                    EstimatedPickupTime TEXT,
                    FOREIGN KEY (UserID) REFERENCES Users(UserID)
                )");
        }

        private void CreateDestinationTable()
        {
            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS Destination (
                    DestinationID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Latitude REAL NOT NULL,
                    Longitude REAL NOT NULL,
                    Address TEXT,
                    TargetArrivalTime TEXT NOT NULL
                )");
        }

        private void CreateRouteAndAssignmentTables()
        {
            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS Routes (
                    RouteID INTEGER PRIMARY KEY AUTOINCREMENT,
                    SolutionDate TEXT NOT NULL,
                    GeneratedTime TEXT DEFAULT CURRENT_TIMESTAMP
                )");

            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS RouteDetails (
                    RouteDetailID INTEGER PRIMARY KEY AUTOINCREMENT,
                    RouteID INTEGER NOT NULL,
                    VehicleID INTEGER NOT NULL,
                    TotalDistance REAL NOT NULL,
                    TotalTime REAL NOT NULL,
                    DepartureTime TEXT,
                    FOREIGN KEY (RouteID) REFERENCES Routes(RouteID),
                    FOREIGN KEY (VehicleID) REFERENCES Vehicles(VehicleID)
                )");

            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS PassengerAssignments (
                    AssignmentID INTEGER PRIMARY KEY AUTOINCREMENT,
                    RouteDetailID INTEGER NOT NULL,
                    PassengerID INTEGER NOT NULL,
                    StopOrder INTEGER NOT NULL,
                    EstimatedPickupTime TEXT,
                    FOREIGN KEY (RouteDetailID) REFERENCES RouteDetails(RouteDetailID),
                    FOREIGN KEY (PassengerID) REFERENCES Passengers(PassengerID)
                )");
        }

        private void CreateSettingsAndLogTables()
        {
            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS Settings (
                    SettingID INTEGER PRIMARY KEY AUTOINCREMENT,
                    SettingName TEXT NOT NULL UNIQUE,
                    SettingValue TEXT NOT NULL
                )");

            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS SchedulingLog (
                    LogID INTEGER PRIMARY KEY AUTOINCREMENT,
                    RunTime TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    RoutesGenerated INTEGER,
                    PassengersAssigned INTEGER,
                    ErrorMessage TEXT
                )");
        }

        private void ExecuteNonQuery(string commandText)
        {
            using (var cmd = new SQLiteCommand(commandText, _connection))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    /// Inserts default data into the database
    /// </summary>
    public class DefaultDataInserter
    {
        private readonly SQLiteConnection _connection;
        private readonly SecurityHelper _securityHelper;

        public DefaultDataInserter(SQLiteConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _securityHelper = new SecurityHelper();
        }

        public void InsertDefaultData()
        {
            InsertDefaultAdmin();
            InsertDefaultDestination();
        }

        private void InsertDefaultAdmin()
        {
            using (var cmd = new SQLiteCommand(_connection))
            {
                cmd.CommandText = @"
                    INSERT INTO Users (Username, Password, UserType, Name)
                    VALUES (@Username, @Password, @UserType, @Name)";
                cmd.Parameters.AddWithValue("@Username", "admin");
                cmd.Parameters.AddWithValue("@Password", _securityHelper.HashPassword("admin"));
                cmd.Parameters.AddWithValue("@UserType", "Admin");
                cmd.Parameters.AddWithValue("@Name", "Administrator");
                cmd.ExecuteNonQuery();
            }
        }

        private void InsertDefaultDestination()
        {
            using (var cmd = new SQLiteCommand(_connection))
            {
                cmd.CommandText = @"
                    INSERT INTO Destination (Name, Latitude, Longitude, TargetArrivalTime)
                    VALUES (@Name, @Latitude, @Longitude, @TargetArrivalTime)";
                cmd.Parameters.AddWithValue("@Name", "School");
                cmd.Parameters.AddWithValue("@Latitude", 32.0741);
                cmd.Parameters.AddWithValue("@Longitude", 34.7922);
                cmd.Parameters.AddWithValue("@TargetArrivalTime", "08:00:00");
                cmd.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    /// Updates database schema for existing databases
    /// </summary>
    public class DatabaseSchemaUpdater
    {
        private readonly SQLiteConnection _connection;

        public DatabaseSchemaUpdater(SQLiteConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public void UpdateSchema()
        {
            EnsureRouteTablesExist();
            EnsureSettingsTablesExist();
            AddMissingColumns();
        }

        private void EnsureRouteTablesExist()
        {
            if (!TableExists("Routes"))
            {
                CreateRouteTables();
            }
        }

        private void EnsureSettingsTablesExist()
        {
            if (!TableExists("Settings"))
            {
                CreateSettingsTables();
            }
        }

        private void AddMissingColumns()
        {
            AddColumnIfNotExists("RouteDetails", "DepartureTime", "TEXT");
            AddColumnIfNotExists("Vehicles", "DepartureTime", "TEXT");
            AddColumnIfNotExists("Passengers", "EstimatedPickupTime", "TEXT");
        }

        private bool TableExists(string tableName)
        {
            using (var cmd = new SQLiteCommand(_connection))
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@TableName";
                cmd.Parameters.AddWithValue("@TableName", tableName);
                var result = cmd.ExecuteScalar();
                return result != null;
            }
        }

        private void CreateRouteTables()
        {
            using (var cmd = new SQLiteCommand(_connection))
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Routes (
                        RouteID INTEGER PRIMARY KEY AUTOINCREMENT,
                        SolutionDate TEXT NOT NULL,
                        GeneratedTime TEXT DEFAULT CURRENT_TIMESTAMP
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS RouteDetails (
                        RouteDetailID INTEGER PRIMARY KEY AUTOINCREMENT,
                        RouteID INTEGER NOT NULL,
                        VehicleID INTEGER NOT NULL,
                        TotalDistance REAL NOT NULL,
                        TotalTime REAL NOT NULL,
                        DepartureTime TEXT,
                        FOREIGN KEY (RouteID) REFERENCES Routes(RouteID),
                        FOREIGN KEY (VehicleID) REFERENCES Vehicles(VehicleID)
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS PassengerAssignments (
                        AssignmentID INTEGER PRIMARY KEY AUTOINCREMENT,
                        RouteDetailID INTEGER NOT NULL,
                        PassengerID INTEGER NOT NULL,
                        StopOrder INTEGER NOT NULL,
                        EstimatedPickupTime TEXT,
                        FOREIGN KEY (RouteDetailID) REFERENCES RouteDetails(RouteDetailID),
                        FOREIGN KEY (PassengerID) REFERENCES Passengers(PassengerID)
                    )";
                cmd.ExecuteNonQuery();
            }
        }

        private void CreateSettingsTables()
        {
            using (var cmd = new SQLiteCommand(_connection))
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Settings (
                        SettingID INTEGER PRIMARY KEY AUTOINCREMENT,
                        SettingName TEXT NOT NULL UNIQUE,
                        SettingValue TEXT NOT NULL
                    )";
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS SchedulingLog (
                        LogID INTEGER PRIMARY KEY AUTOINCREMENT,
                        RunTime TEXT NOT NULL,
                        Status TEXT NOT NULL,
                        RoutesGenerated INTEGER,
                        PassengersAssigned INTEGER,
                        ErrorMessage TEXT
                    )";
                cmd.ExecuteNonQuery();
            }
        }

        private void AddColumnIfNotExists(string tableName, string columnName, string columnType)
        {
            if (ColumnExists(tableName, columnName))
            {
                return;
            }

            using (var cmd = new SQLiteCommand(_connection))
            {
                cmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType}";
                cmd.ExecuteNonQuery();
            }
        }

        private bool ColumnExists(string tableName, string columnName)
        {
            using (var cmd = new SQLiteCommand(_connection))
            {
                cmd.CommandText = $"PRAGMA table_info({tableName})";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader["name"].ToString() == columnName)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Security helper for password hashing
    /// </summary>
    public class SecurityHelper
    {
        public string HashPassword(string password)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }
    }

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

    /// <summary>
    /// Service for destination-related database operations
    /// </summary>
    public class DestinationService
    {
        private readonly DatabaseManager _dbManager;

        public DestinationService(DatabaseManager dbManager)
        {
            _dbManager = dbManager ?? throw new ArgumentNullException(nameof(dbManager));
        }

        public async Task<(int Id, string Name, double Latitude, double Longitude, string Address, string TargetTime)>
            GetDestinationAsync()
        {
            string query = @"
                SELECT DestinationID, Name, Latitude, Longitude, Address, TargetArrivalTime
                FROM Destination
                ORDER BY DestinationID LIMIT 1";

            var destination = await _dbManager.ExecuteReaderSingleAsync<(int, string, double, double, string, string)>(
                query,
                async reader => (
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetDouble(2),
                    reader.GetDouble(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetString(5)
                ),
                null
            );

            // Return default values if no destination is found
            return destination.Item1 != 0 ? destination : (0, "Default", 32.0741, 34.7922, null, "08:00:00");
        }

        public async Task<bool> UpdateDestinationAsync(string name, double latitude,
            double longitude, string targetTime, string address = "")
        {
            var parameters = new Dictionary<string, object>
            {
                { "@Name", name },
                { "@Latitude", latitude },
                { "@Longitude", longitude },
                { "@Address", address ?? "" },
                { "@TargetArrivalTime", targetTime }
            };

            string query = @"
                UPDATE Destination
                SET Name = @Name, Latitude = @Latitude, Longitude = @Longitude, 
                    Address = @Address, TargetArrivalTime = @TargetArrivalTime
                WHERE DestinationID = (SELECT DestinationID FROM Destination ORDER BY DestinationID LIMIT 1)";

            int rowsAffected = await _dbManager.ExecuteNonQueryAsync(query, parameters);
            return rowsAffected > 0;
        }
    }

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

        private async Task<int> SaveVehicleRouteAsync(int routeId, Vehicle vehicle, SQLiteTransaction transaction)
        {
            int routeDetailId = await InsertRouteDetailAsync(routeId, vehicle, transaction);

            if (routeDetailId <= 0)
            {
                return -1;
            }

            await UpdateVehicleDepartureTimeAsync(vehicle.Id, vehicle.DepartureTime, transaction);

            for (int i = 0; i < vehicle.AssignedPassengers.Count; i++)
            {
                var passenger = vehicle.AssignedPassengers[i];
                await SavePassengerAssignmentAsync(routeDetailId, passenger, i + 1, transaction);
                await UpdatePassengerPickupTimeAsync(passenger.Id, passenger.EstimatedPickupTime, transaction);
            }

            return routeDetailId;
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
    }

    /// <summary>
    /// Service for settings-related database operations
    /// </summary>
    public class SettingsService
    {
        private readonly DatabaseManager _dbManager;

        public SettingsService(DatabaseManager dbManager)
        {
            _dbManager = dbManager ?? throw new ArgumentNullException(nameof(dbManager));
        }

        public async Task SaveSchedulingSettingsAsync(bool isEnabled, DateTime scheduledTime)
        {
            await EnsureSettingsTableExistsAsync();

            await SaveSettingAsync("SchedulingEnabled", isEnabled ? "1" : "0");
            await SaveSettingAsync("ScheduledTime", scheduledTime.ToString("HH:mm:ss"));
        }

        public async Task<(bool IsEnabled, DateTime ScheduledTime)> GetSchedulingSettingsAsync()
        {
            if (!await TableExistsAsync("Settings"))
            {
                return (false, DateTime.Parse("00:00:00"));
            }

            string enabledValue = await GetSettingAsync("SchedulingEnabled", "0");
            bool isEnabled = enabledValue == "1";

            string timeValue = await GetSettingAsync("ScheduledTime", "00:00:00");
            DateTime scheduledTime = DateTime.Parse(timeValue);

            return (isEnabled, scheduledTime);
        }

        public async Task<bool> SaveSettingAsync(string settingName, string settingValue)
        {
            await EnsureSettingsTableExistsAsync();

            var parameters = new Dictionary<string, object>
            {
                { "@SettingName", settingName },
                { "@SettingValue", settingValue }
            };

            string query = @"
                INSERT OR REPLACE INTO Settings (SettingName, SettingValue)
                VALUES (@SettingName, @SettingValue)";

            int rowsAffected = await _dbManager.ExecuteNonQueryAsync(query, parameters);
            return rowsAffected > 0;
        }

        public async Task<string> GetSettingAsync(string settingName, string defaultValue = "")
        {
            if (!await TableExistsAsync("Settings"))
            {
                return defaultValue;
            }

            var parameters = new Dictionary<string, object> { { "@SettingName", settingName } };

            string query = "SELECT SettingValue FROM Settings WHERE SettingName = @SettingName";

            string value = await _dbManager.ExecuteScalarAsync<string>(query, parameters);
            return !string.IsNullOrEmpty(value) ? value : defaultValue;
        }

        private async Task EnsureSettingsTableExistsAsync()
        {
            string query = @"
                CREATE TABLE IF NOT EXISTS Settings (
                    SettingID INTEGER PRIMARY KEY AUTOINCREMENT,
                    SettingName TEXT NOT NULL UNIQUE,
                    SettingValue TEXT NOT NULL
                )";

            await _dbManager.ExecuteNonQueryAsync(query, null);
        }

        private async Task<bool> TableExistsAsync(string tableName)
        {
            var parameters = new Dictionary<string, object> { { "@TableName", tableName } };

            string query = "SELECT name FROM sqlite_master WHERE type='table' AND name=@TableName";

            string result = await _dbManager.ExecuteScalarAsync<string>(query, parameters);
            return !string.IsNullOrEmpty(result);
        }

        public async Task LogSchedulingRunAsync(DateTime runTime, string status,
            int routesGenerated, int passengersAssigned, string errorMessage = null)
        {
            await EnsureSchedulingLogTableExistsAsync();

            var parameters = new Dictionary<string, object>
            {
                { "@RunTime", runTime.ToString("yyyy-MM-dd HH:mm:ss") },
                { "@Status", status },
                { "@RoutesGenerated", routesGenerated },
                { "@PassengersAssigned", passengersAssigned },
                { "@ErrorMessage", errorMessage }
            };

            string query = @"
                INSERT INTO SchedulingLog (RunTime, Status, RoutesGenerated, PassengersAssigned, ErrorMessage)
                VALUES (@RunTime, @Status, @RoutesGenerated, @PassengersAssigned, @ErrorMessage)";

            await _dbManager.ExecuteNonQueryAsync(query, parameters);
        }

        public async Task<List<(DateTime RunTime, string Status, int RoutesGenerated, int PassengersAssigned)>>
            GetSchedulingLogAsync()
        {
            await EnsureSchedulingLogTableExistsAsync();

            string query = @"
                SELECT RunTime, Status, RoutesGenerated, PassengersAssigned
                FROM SchedulingLog
                ORDER BY RunTime DESC
                LIMIT 50";

            return await _dbManager.ExecuteReaderAsync<(DateTime, string, int, int)>(
                query,
                async reader => (
                    DateTime.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.GetInt32(3)
                ),
                null
            );
        }

        private async Task EnsureSchedulingLogTableExistsAsync()
        {
            string query = @"
                CREATE TABLE IF NOT EXISTS SchedulingLog (
                    LogID INTEGER PRIMARY KEY AUTOINCREMENT,
                    RunTime TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    RoutesGenerated INTEGER,
                    PassengersAssigned INTEGER,
                    ErrorMessage TEXT
                )";

            await _dbManager.ExecuteNonQueryAsync(query, null);
        }
    }

    /// <summary>
    /// Main database service facade that provides access to all domain services
    /// </summary>
    public class DatabaseService : IDisposable
    {
        private readonly DatabaseManager _dbManager;
        private readonly UserService _userService;
        private readonly VehicleService _vehicleService;
        private readonly PassengerService _passengerService;
        private readonly DestinationService _destinationService;
        private readonly RouteService _routeService;
        private readonly SettingsService _settingsService;
        private bool _disposed = false;

        public DatabaseService(string dbFilePath = "ridematch.db")
        {
            _dbManager = new DatabaseManager(dbFilePath);
            _userService = new UserService(_dbManager);
            _vehicleService = new VehicleService(_dbManager);
            _passengerService = new PassengerService(_dbManager);
            _destinationService = new DestinationService(_dbManager);
            _routeService = new RouteService(_dbManager);
            _settingsService = new SettingsService(_dbManager);
        }

        public SQLiteConnection GetConnection()
        {
            return _dbManager.GetConnection();
        }

        #region User Methods

        public Task<(bool Success, string UserType, int UserId)> AuthenticateUserAsync(
            string username, string password)
        {
            return _userService.AuthenticateUserAsync(username, password);
        }

        public Task<int> AddUserAsync(string username, string password,
            string userType, string name, string email = "", string phone = "")
        {
            return _userService.AddUserAsync(username, password, userType, name, email, phone);
        }

        public Task<bool> UpdateUserAsync(int userId, string name, string email, string phone)
        {
            return _userService.UpdateUserAsync(userId, name, email, phone);
        }

        public Task<bool> UpdateUserProfileAsync(int userId, string userType,
            string name, string email, string phone)
        {
            return _userService.UpdateUserProfileAsync(userId, userType, name, email, phone);
        }

        public Task<bool> ChangePasswordAsync(int userId, string newPassword)
        {
            return _userService.ChangePasswordAsync(userId, newPassword);
        }

        public Task<(string Username, string UserType, string Name, string Email, string Phone)>
            GetUserInfoAsync(int userId)
        {
            return _userService.GetUserInfoAsync(userId);
        }

        public Task<List<(int Id, string Username, string UserType, string Name, string Email, string Phone)>>
            GetAllUsersAsync()
        {
            return _userService.GetAllUsersAsync();
        }

        public Task<bool> DeleteUserAsync(int userId)
        {
            return _userService.DeleteUserAsync(userId);
        }

        public Task<List<(int Id, string Username, string Name)>> GetUsersByTypeAsync(string userType)
        {
            return _userService.GetUsersByTypeAsync(userType);
        }

        #endregion

        #region Vehicle Methods

        public Task<int> AddVehicleAsync(int userId, int capacity,
            double startLatitude, double startLongitude, string startAddress = "")
        {
            return _vehicleService.AddVehicleAsync(userId, capacity, startLatitude, startLongitude, startAddress);
        }

        public Task<bool> UpdateVehicleAsync(int vehicleId, int capacity,
            double startLatitude, double startLongitude, string startAddress = "")
        {
            return _vehicleService.UpdateVehicleAsync(vehicleId, capacity, startLatitude, startLongitude, startAddress);
        }

        public Task<bool> UpdateVehicleAvailabilityAsync(int vehicleId, bool isAvailable)
        {
            return _vehicleService.UpdateVehicleAvailabilityAsync(vehicleId, isAvailable);
        }

        public Task<List<Vehicle>> GetAllVehiclesAsync()
        {
            return _vehicleService.GetAllVehiclesAsync();
        }

        public Task<List<Vehicle>> GetAvailableVehiclesAsync()
        {
            return _vehicleService.GetAvailableVehiclesAsync();
        }

        public Task<Vehicle> GetVehicleByUserIdAsync(int userId)
        {
            return _vehicleService.GetVehicleByUserIdAsync(userId);
        }

        public Task<Vehicle> GetVehicleByIdAsync(int vehicleId)
        {
            return _vehicleService.GetVehicleByIdAsync(vehicleId);
        }

        public Task<int> SaveDriverVehicleAsync(int userId, int capacity,
            double startLatitude, double startLongitude, string startAddress = "")
        {
            return _vehicleService.SaveDriverVehicleAsync(userId, capacity, startLatitude, startLongitude, startAddress);
        }

        public Task<bool> UpdateVehicleCapacityAsync(int userId, int capacity)
        {
            return _vehicleService.SaveDriverVehicleAsync(userId, capacity, 0, 0, "").ContinueWith(t => t.Result > 0);
        }

        public Task<bool> UpdateVehicleLocationAsync(int userId, double latitude, double longitude, string address = "")
        {
            return _vehicleService.SaveDriverVehicleAsync(userId, 4, latitude, longitude, address)
                .ContinueWith(t => t.Result > 0);
        }

        public Task<bool> DeleteVehicleAsync(int vehicleId)
        {
            return _vehicleService.DeleteVehicleAsync(vehicleId);
        }

        #endregion

        #region Passenger Methods

        public Task<int> AddPassengerAsync(int userId, string name,
            double latitude, double longitude, string address = "")
        {
            return _passengerService.AddPassengerAsync(userId, name, latitude, longitude, address);
        }

        public Task<bool> UpdatePassengerAsync(int passengerId, string name,
            double latitude, double longitude, string address = "")
        {
            return _passengerService.UpdatePassengerAsync(passengerId, name, latitude, longitude, address);
        }

        public Task<bool> UpdatePassengerAvailabilityAsync(int passengerId, bool isAvailable)
        {
            return _passengerService.UpdatePassengerAvailabilityAsync(passengerId, isAvailable);
        }

        public Task<List<Passenger>> GetAvailablePassengersAsync()
        {
            return _passengerService.GetAvailablePassengersAsync();
        }

        public Task<List<Passenger>> GetAllPassengersAsync()
        {
            return _passengerService.GetAllPassengersAsync();
        }

        public Task<Passenger> GetPassengerByUserIdAsync(int userId)
        {
            return _passengerService.GetPassengerByUserIdAsync(userId);
        }

        public Task<Passenger> GetPassengerByIdAsync(int passengerId)
        {
            return _passengerService.GetPassengerByIdAsync(passengerId);
        }

        public Task<bool> DeletePassengerAsync(int passengerId)
        {
            return _passengerService.DeletePassengerAsync(passengerId);
        }

        #endregion

        #region Destination Methods

        public Task<(int Id, string Name, double Latitude, double Longitude, string Address, string TargetTime)>
            GetDestinationAsync()
        {
            return _destinationService.GetDestinationAsync();
        }

        public Task<bool> UpdateDestinationAsync(string name, double latitude,
            double longitude, string targetTime, string address = "")
        {
            return _destinationService.UpdateDestinationAsync(name, latitude, longitude, targetTime, address);
        }

        #endregion

        #region Route Methods

        public Task<int> SaveSolutionAsync(Solution solution, string date)
        {
            return _routeService.SaveSolutionAsync(solution, date);
        }

        public Task<Solution> GetSolutionForDateAsync(string date)
        {
            return _routeService.GetSolutionForDateAsync(date);
        }

        public Task<(Vehicle Vehicle, List<Passenger> Passengers, DateTime? PickupTime)>
            GetDriverRouteAsync(int userId, string date)
        {
            return _routeService.GetDriverRouteAsync(userId, date);
        }

        public Task<(Vehicle AssignedVehicle, DateTime? PickupTime)> GetPassengerAssignmentAsync(int userId, string date)
        {
            return _routeService.GetPassengerAssignmentAsync(userId, date);
        }

        public Task<bool> UpdatePickupTimesAsync(int routeDetailId, Dictionary<int, string> passengerPickupTimes)
        {
            return _routeService.UpdatePickupTimesAsync(routeDetailId, passengerPickupTimes);
        }

        public Task ResetAvailabilityAsync()
        {
            return _routeService.ResetAvailabilityAsync();
        }

        public Task<List<(int RouteId, DateTime GeneratedTime, int VehicleCount, int PassengerCount)>>
            GetRouteHistoryAsync()
        {
            return _routeService.GetRouteHistoryAsync();
        }

        #endregion

        #region Settings Methods

        public Task SaveSchedulingSettingsAsync(bool isEnabled, DateTime scheduledTime)
        {
            return _settingsService.SaveSchedulingSettingsAsync(isEnabled, scheduledTime);
        }

        public Task<(bool IsEnabled, DateTime ScheduledTime)> GetSchedulingSettingsAsync()
        {
            return _settingsService.GetSchedulingSettingsAsync();
        }

        public Task LogSchedulingRunAsync(DateTime runTime, string status,
            int routesGenerated, int passengersAssigned, string errorMessage = null)
        {
            return _settingsService.LogSchedulingRunAsync(runTime, status,
                routesGenerated, passengersAssigned, errorMessage);
        }

        public Task<List<(DateTime RunTime, string Status, int RoutesGenerated, int PassengersAssigned)>>
            GetSchedulingLogAsync()
        {
            return _settingsService.GetSchedulingLogAsync();
        }

        public Task<bool> SaveSettingAsync(string settingName, string settingValue)
        {
            return _settingsService.SaveSettingAsync(settingName, settingValue);
        }

        public Task<string> GetSettingAsync(string settingName, string defaultValue = "")
        {
            return _settingsService.GetSettingAsync(settingName, defaultValue);
        }

        #endregion

        #region IDisposable Implementation

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
                    _dbManager?.Dispose();
                }
                _disposed = true;
            }
        }

        #endregion
    }
}