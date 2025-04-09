using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using claudpro.Models;

namespace claudpro.Services
{
    public class DatabaseService : IDisposable
    {
        private readonly string connectionString;
        private SQLiteConnection connection;
        private bool disposed = false;

        /// <summary>
        /// Creates a new database service with the given database file
        /// </summary>
        public DatabaseService(string dbFilePath = "ridematch.db")
        {
            bool createNew = !File.Exists(dbFilePath);
            connectionString = $"Data Source={dbFilePath};Version=3;";

            // Create and open the connection
            connection = new SQLiteConnection(connectionString);
            connection.Open();

            if (createNew)
            {
                CreateDatabase();
            }
        }

        /// <summary>
        /// Gets the SQLite connection for direct queries
        /// </summary>
        public SQLiteConnection GetConnection()
        {
            return connection;
        }

        /// <summary>
        /// Creates the database schema if it doesn't exist
        /// </summary>
        private void CreateDatabase()
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                // Create Users table (for authentication)
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        UserID INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username TEXT NOT NULL UNIQUE,
                        Password TEXT NOT NULL,
                        UserType TEXT NOT NULL,
                        Name TEXT NOT NULL,
                        Email TEXT,
                        Phone TEXT,
                        CreatedDate TEXT DEFAULT CURRENT_TIMESTAMP
                    )";
                cmd.ExecuteNonQuery();

                // Create Vehicles table with UserId foreign key
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Vehicles (
                        VehicleID INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserID INTEGER NOT NULL UNIQUE,  -- UNIQUE constraint ensures one-to-one relationship
                        Capacity INTEGER NOT NULL DEFAULT 4,
                        StartLatitude REAL NOT NULL DEFAULT 0,
                        StartLongitude REAL NOT NULL DEFAULT 0,
                        StartAddress TEXT,
                        IsAvailableTomorrow INTEGER DEFAULT 1,
                        FOREIGN KEY (UserID) REFERENCES Users(UserID)
                    )";
                cmd.ExecuteNonQuery();

                // Create Passengers table
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Passengers (
                        PassengerID INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserID INTEGER,
                        Name TEXT NOT NULL,
                        Latitude REAL NOT NULL,
                        Longitude REAL NOT NULL,
                        Address TEXT,
                        IsAvailableTomorrow INTEGER DEFAULT 1,
                        FOREIGN KEY (UserID) REFERENCES Users(UserID)
                    )";
                cmd.ExecuteNonQuery();

                // Create Destination table
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Destination (
                        DestinationID INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Latitude REAL NOT NULL,
                        Longitude REAL NOT NULL,
                        Address TEXT,
                        TargetArrivalTime TEXT NOT NULL
                    )";
                cmd.ExecuteNonQuery();

                // Create Routes table for storing generated solutions
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Routes (
                        RouteID INTEGER PRIMARY KEY AUTOINCREMENT,
                        SolutionDate TEXT NOT NULL,
                        GeneratedTime TEXT DEFAULT CURRENT_TIMESTAMP
                    )";
                cmd.ExecuteNonQuery();

                // Create RouteDetails table for storing vehicle assignments
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS RouteDetails (
                        RouteDetailID INTEGER PRIMARY KEY AUTOINCREMENT,
                        RouteID INTEGER NOT NULL,
                        VehicleID INTEGER NOT NULL,
                        TotalDistance REAL NOT NULL,
                        TotalTime REAL NOT NULL,
                        FOREIGN KEY (RouteID) REFERENCES Routes(RouteID),
                        FOREIGN KEY (VehicleID) REFERENCES Vehicles(VehicleID)
                    )";
                cmd.ExecuteNonQuery();

                // Create PassengerAssignments table
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

                // Create Settings table
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Settings (
                        SettingID INTEGER PRIMARY KEY AUTOINCREMENT,
                        SettingName TEXT NOT NULL UNIQUE,
                        SettingValue TEXT NOT NULL
                    )";
                cmd.ExecuteNonQuery();

                // Create SchedulingLog table
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

                // Create an admin user by default
                cmd.CommandText = @"
                    INSERT INTO Users (Username, Password, UserType, Name)
                    VALUES (@Username, @Password, @UserType, @Name)";
                cmd.Parameters.AddWithValue("@Username", "admin");
                cmd.Parameters.AddWithValue("@Password", HashPassword("admin")); // In production, use proper password hashing
                cmd.Parameters.AddWithValue("@UserType", "Admin");
                cmd.Parameters.AddWithValue("@Name", "Administrator");
                cmd.ExecuteNonQuery();

                // Insert default destination
                cmd.Parameters.Clear();
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

        #region User Methods

        /// <summary>
        /// Authenticates a user with the given username and password
        /// </summary>
        public async Task<(bool Success, string UserType, int UserId)> AuthenticateUserAsync(string username, string password)
        {
            string hashedPassword = HashPassword(password);

            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = "SELECT UserID, UserType FROM Users WHERE Username = @Username AND Password = @Password";
                cmd.Parameters.AddWithValue("@Username", username);
                cmd.Parameters.AddWithValue("@Password", hashedPassword);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        int userId = reader.GetInt32(0);
                        string userType = reader.GetString(1);
                        return (true, userType, userId);
                    }
                }
            }

            return (false, "", 0);
        }

        /// <summary>
        /// Creates a new user in the database
        /// </summary>
        public async Task<int> AddUserAsync(string username, string password, string userType, string name, string email = "", string phone = "")
        {
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    int userId;
                    using (var cmd = new SQLiteCommand(connection))
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                    INSERT INTO Users (Username, Password, UserType, Name, Email, Phone)
                    VALUES (@Username, @Password, @UserType, @Name, @Email, @Phone);
                    SELECT last_insert_rowid();";
                        cmd.Parameters.AddWithValue("@Username", username);
                        cmd.Parameters.AddWithValue("@Password", HashPassword(password));
                        cmd.Parameters.AddWithValue("@UserType", userType);
                        cmd.Parameters.AddWithValue("@Name", name);
                        cmd.Parameters.AddWithValue("@Email", email ?? "");
                        cmd.Parameters.AddWithValue("@Phone", phone ?? "");

                        object result = await cmd.ExecuteScalarAsync();
                        if (result == null || !int.TryParse(result.ToString(), out userId))
                        {
                            transaction.Rollback();
                            return -1;
                        }
                    }

                    // If user is a driver, automatically create a vehicle for them
                    if (userType.ToLower() == "driver" && userId > 0)
                    {
                        using (var vehicleCmd = new SQLiteCommand(connection))
                        {
                            vehicleCmd.Transaction = transaction;
                            vehicleCmd.CommandText = @"
                        INSERT INTO Vehicles (UserID, Capacity, StartLatitude, StartLongitude, IsAvailableTomorrow)
                        VALUES (@UserID, @Capacity, @StartLatitude, @StartLongitude, 1)";
                            vehicleCmd.Parameters.AddWithValue("@UserID", userId);
                            vehicleCmd.Parameters.AddWithValue("@Capacity", 4); // Default capacity
                            vehicleCmd.Parameters.AddWithValue("@StartLatitude", 0);
                            vehicleCmd.Parameters.AddWithValue("@StartLongitude", 0);
                            await vehicleCmd.ExecuteNonQueryAsync();
                        }
                    }

                    transaction.Commit();
                    return userId;
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        /// <summary>
        /// Updates user information
        /// </summary>
        public async Task<bool> UpdateUserAsync(int userId, string name, string email, string phone)
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    UPDATE Users
                    SET Name = @Name, Email = @Email, Phone = @Phone
                    WHERE UserID = @UserID";
                cmd.Parameters.AddWithValue("@UserID", userId);
                cmd.Parameters.AddWithValue("@Name", name);
                cmd.Parameters.AddWithValue("@Email", email ?? "");
                cmd.Parameters.AddWithValue("@Phone", phone ?? "");

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        /// <summary>
        /// Updates a user's profile information including user type
        /// </summary>
        public async Task<bool> UpdateUserProfileAsync(int userId, string userType, string name, string email, string phone)
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    UPDATE Users 
                    SET UserType = @UserType, Name = @Name, Email = @Email, Phone = @Phone
                    WHERE UserID = @UserID";
                cmd.Parameters.AddWithValue("@UserID", userId);
                cmd.Parameters.AddWithValue("@UserType", userType);
                cmd.Parameters.AddWithValue("@Name", name);
                cmd.Parameters.AddWithValue("@Email", email ?? "");
                cmd.Parameters.AddWithValue("@Phone", phone ?? "");

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        /// <summary>
        /// Changes a user's password
        /// </summary>
        public async Task<bool> ChangePasswordAsync(int userId, string newPassword)
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = "UPDATE Users SET Password = @Password WHERE UserID = @UserID";
                cmd.Parameters.AddWithValue("@UserID", userId);
                cmd.Parameters.AddWithValue("@Password", HashPassword(newPassword));

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        /// <summary>
        /// Gets user information by ID
        /// </summary>
        public async Task<(string Username, string UserType, string Name, string Email, string Phone)> GetUserInfoAsync(int userId)
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = "SELECT Username, UserType, Name, Email, Phone FROM Users WHERE UserID = @UserID";
                cmd.Parameters.AddWithValue("@UserID", userId);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return (
                            reader.GetString(0),
                            reader.GetString(1),
                            reader.GetString(2),
                            reader.IsDBNull(3) ? "" : reader.GetString(3),
                            reader.IsDBNull(4) ? "" : reader.GetString(4)
                        );
                    }
                }
            }

            return (null, null, null, null, null);
        }

        /// <summary>
        /// Gets all users from the database
        /// </summary>
        public async Task<List<(int Id, string Username, string UserType, string Name, string Email, string Phone)>> GetAllUsersAsync()
        {
            var users = new List<(int Id, string Username, string UserType, string Name, string Email, string Phone)>();

            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    SELECT UserID, Username, UserType, Name, Email, Phone 
                    FROM Users 
                    ORDER BY UserType, Username";

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        users.Add((
                            reader.GetInt32(0),
                            reader.GetString(1),
                            reader.GetString(2),
                            reader.GetString(3),
                            reader.IsDBNull(4) ? "" : reader.GetString(4),
                            reader.IsDBNull(5) ? "" : reader.GetString(5)
                        ));
                    }
                }
            }

            return users;
        }

        /// <summary>
        /// Deletes a user by ID
        /// </summary>
        public async Task<bool> DeleteUserAsync(int userId)
        {
            // Begin transaction to handle cascading deletes
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // Delete associated vehicle if exists
                    using (var cmd = new SQLiteCommand(connection))
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "DELETE FROM Vehicles WHERE UserID = @UserID";
                        cmd.Parameters.AddWithValue("@UserID", userId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Delete associated passenger if exists
                    using (var cmd = new SQLiteCommand(connection))
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "DELETE FROM Passengers WHERE UserID = @UserID";
                        cmd.Parameters.AddWithValue("@UserID", userId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Delete the user
                    using (var cmd = new SQLiteCommand(connection))
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "DELETE FROM Users WHERE UserID = @UserID";
                        cmd.Parameters.AddWithValue("@UserID", userId);
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected == 0)
                        {
                            // No user was deleted
                            transaction.Rollback();
                            return false;
                        }
                    }

                    // Commit the transaction
                    transaction.Commit();
                    return true;
                }
                catch (Exception)
                {
                    // Rollback on error
                    transaction.Rollback();
                    throw;
                }
            }
        }

        /// <summary>
        /// Gets all users with a specific user type
        /// </summary>
        public async Task<List<(int Id, string Username, string Name)>> GetUsersByTypeAsync(string userType)
        {
            var users = new List<(int Id, string Username, string Name)>();

            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    SELECT UserID, Username, Name
                    FROM Users
                    WHERE UserType = @UserType
                    ORDER BY Username";
                cmd.Parameters.AddWithValue("@UserType", userType);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        users.Add((
                            reader.GetInt32(0),
                            reader.GetString(1),
                            reader.GetString(2)
                        ));
                    }
                }
            }

            return users;
        }

        #endregion

        #region Vehicle Methods

        /// <summary>
        /// Adds a new vehicle to the database
        /// </summary>
        public async Task<int> AddVehicleAsync(int userId, int capacity, double startLatitude, double startLongitude, string startAddress = "")
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    INSERT INTO Vehicles (UserID, Capacity, StartLatitude, StartLongitude, StartAddress)
                    VALUES (@UserID, @Capacity, @StartLatitude, @StartLongitude, @StartAddress);
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@UserID", userId);
                cmd.Parameters.AddWithValue("@Capacity", capacity);
                cmd.Parameters.AddWithValue("@StartLatitude", startLatitude);
                cmd.Parameters.AddWithValue("@StartLongitude", startLongitude);
                cmd.Parameters.AddWithValue("@StartAddress", startAddress ?? "");

                object result = await cmd.ExecuteScalarAsync();
                if (result != null && int.TryParse(result.ToString(), out int vehicleId))
                {
                    return vehicleId;
                }
            }

            return -1;
        }

        /// <summary>
        /// Updates a vehicle's information
        /// </summary>
        public async Task<bool> UpdateVehicleAsync(int vehicleId, int capacity, double startLatitude, double startLongitude, string startAddress = "")
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    UPDATE Vehicles
                    SET Capacity = @Capacity, StartLatitude = @StartLatitude, 
                        StartLongitude = @StartLongitude, StartAddress = @StartAddress
                    WHERE VehicleID = @VehicleID";
                cmd.Parameters.AddWithValue("@VehicleID", vehicleId);
                cmd.Parameters.AddWithValue("@Capacity", capacity);
                cmd.Parameters.AddWithValue("@StartLatitude", startLatitude);
                cmd.Parameters.AddWithValue("@StartLongitude", startLongitude);
                cmd.Parameters.AddWithValue("@StartAddress", startAddress ?? "");

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        /// <summary>
        /// Updates a vehicle's availability for tomorrow
        /// </summary>
        public async Task<bool> UpdateVehicleAvailabilityAsync(int vehicleId, bool isAvailable)
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = "UPDATE Vehicles SET IsAvailableTomorrow = @IsAvailable WHERE VehicleID = @VehicleID";
                cmd.Parameters.AddWithValue("@VehicleID", vehicleId);
                cmd.Parameters.AddWithValue("@IsAvailable", isAvailable ? 1 : 0);

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        /// <summary>
        /// Gets all vehicles in the database, not just those available for tomorrow
        /// </summary>
        public async Task<List<Vehicle>> GetAllVehiclesAsync()
        {
            var vehicles = new List<Vehicle>();

            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    SELECT v.VehicleID, v.UserID, v.Capacity, v.StartLatitude, v.StartLongitude, 
                           v.StartAddress, v.IsAvailableTomorrow, u.Name
                    FROM Vehicles v
                    LEFT JOIN Users u ON v.UserID = u.UserID";

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        vehicles.Add(new Vehicle
                        {
                            Id = reader.GetInt32(0),
                            UserId = reader.GetInt32(1),
                            Capacity = reader.GetInt32(2),
                            StartLatitude = reader.GetDouble(3),
                            StartLongitude = reader.GetDouble(4),
                            StartAddress = reader.IsDBNull(5) ? null : reader.GetString(5),
                            IsAvailableTomorrow = reader.GetInt32(6) == 1,
                            DriverName = reader.IsDBNull(7) ? null : reader.GetString(7),
                            AssignedPassengers = new List<Passenger>()
                        });
                    }
                }
            }

            return vehicles;
        }

        /// <summary>
        /// Gets all vehicles that are available for tomorrow
        /// </summary>
        public async Task<List<Vehicle>> GetAvailableVehiclesAsync()
        {
            var vehicles = new List<Vehicle>();

            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    SELECT v.VehicleID, v.UserID, v.Capacity, v.StartLatitude, v.StartLongitude, v.StartAddress, u.Name
                    FROM Vehicles v
                    JOIN Users u ON v.UserID = u.UserID
                    WHERE v.IsAvailableTomorrow = 1";

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        vehicles.Add(new Vehicle
                        {
                            Id = reader.GetInt32(0),
                            UserId = reader.GetInt32(1),
                            Capacity = reader.GetInt32(2),
                            StartLatitude = reader.GetDouble(3),
                            StartLongitude = reader.GetDouble(4),
                            StartAddress = reader.IsDBNull(5) ? null : reader.GetString(5),
                            DriverName = reader.GetString(6),
                            AssignedPassengers = new List<Passenger>()
                        });
                    }
                }
            }

            return vehicles;
        }

        /// <summary>
        /// Gets a vehicle by user ID
        /// </summary>
        public async Task<Vehicle> GetVehicleByUserIdAsync(int userId)
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    SELECT VehicleID, Capacity, StartLatitude, StartLongitude, StartAddress, IsAvailableTomorrow
                    FROM Vehicles
                    WHERE UserID = @UserID";
                cmd.Parameters.AddWithValue("@UserID", userId);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new Vehicle
                        {
                            Id = reader.GetInt32(0),
                            UserId = userId,
                            Capacity = reader.GetInt32(1),
                            StartLatitude = reader.GetDouble(2),
                            StartLongitude = reader.GetDouble(3),
                            StartAddress = reader.IsDBNull(4) ? null : reader.GetString(4),
                            IsAvailableTomorrow = reader.GetInt32(5) == 1,
                            AssignedPassengers = new List<Passenger>()
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a vehicle by its ID
        /// </summary>
        public async Task<Vehicle> GetVehicleByIdAsync(int vehicleId)
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    SELECT v.VehicleID, v.UserID, v.Capacity, v.StartLatitude, v.StartLongitude, 
                           v.StartAddress, v.IsAvailableTomorrow, u.Name
                    FROM Vehicles v
                    LEFT JOIN Users u ON v.UserID = u.UserID
                    WHERE v.VehicleID = @VehicleID";
                cmd.Parameters.AddWithValue("@VehicleID", vehicleId);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new Vehicle
                        {
                            Id = reader.GetInt32(0),
                            UserId = reader.GetInt32(1),
                            Capacity = reader.GetInt32(2),
                            StartLatitude = reader.GetDouble(3),
                            StartLongitude = reader.GetDouble(4),
                            StartAddress = reader.IsDBNull(5) ? null : reader.GetString(5),
                            IsAvailableTomorrow = reader.GetInt32(6) == 1,
                            DriverName = reader.IsDBNull(7) ? null : reader.GetString(7),
                            AssignedPassengers = new List<Passenger>()
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the assigned vehicle for a passenger by passengerId
        /// </summary>
        public async Task<Vehicle> GetAssignedVehicleForPassengerAsync(int passengerId)
        {
            // Get today's date to find current assignments
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            // Get the routeId for today
            int routeId;
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = "SELECT RouteID FROM Routes WHERE SolutionDate = @SolutionDate ORDER BY GeneratedTime DESC LIMIT 1";
                cmd.Parameters.AddWithValue("@SolutionDate", today);

                object result = await cmd.ExecuteScalarAsync();
                if (result == null || !int.TryParse(result.ToString(), out routeId))
                {
                    return null; // No route found for today
                }
            }

            // Find the vehicle assigned to this passenger
            int vehicleId = 0;
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    SELECT rd.VehicleID
                    FROM PassengerAssignments pa
                    JOIN RouteDetails rd ON pa.RouteDetailID = rd.RouteDetailID
                    WHERE rd.RouteID = @RouteID AND pa.PassengerID = @PassengerID";
                cmd.Parameters.AddWithValue("@RouteID", routeId);
                cmd.Parameters.AddWithValue("@PassengerID", passengerId);

                object result = await cmd.ExecuteScalarAsync();
                if (result == null || !int.TryParse(result.ToString(), out vehicleId))
                {
                    return null; // Passenger not assigned to any vehicle
                }
            }

            // Get vehicle details
            Vehicle vehicle = null;
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    SELECT v.VehicleID, v.Capacity, v.StartLatitude, v.StartLongitude, v.StartAddress, 
                           u.Name, v.IsAvailableTomorrow
                    FROM Vehicles v
                    LEFT JOIN Users u ON v.UserID = u.UserID
                    WHERE v.VehicleID = @VehicleID";
                cmd.Parameters.AddWithValue("@VehicleID", vehicleId);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        vehicle = new Vehicle
                        {
                            Id = reader.GetInt32(0),
                            Capacity = reader.GetInt32(1),
                            StartLatitude = reader.GetDouble(2),
                            StartLongitude = reader.GetDouble(3),
                            StartAddress = reader.IsDBNull(4) ? null : reader.GetString(4),
                            DriverName = reader.IsDBNull(5) ? $"Driver #{vehicleId}" : reader.GetString(5),
                            IsAvailableTomorrow = reader.GetInt32(6) == 1,
                            // Set default values for UI display properties
                            Model = "Standard Vehicle",
                            Color = "White",
                            LicensePlate = $"V-{vehicleId:D4}"
                        };
                    }
                }
            }

            // If vehicle was found, get its assigned passengers
            if (vehicle != null)
            {
                using (var cmd = new SQLiteCommand(connection))
                {
                    cmd.CommandText = @"
                        SELECT p.PassengerID, p.Name, p.Latitude, p.Longitude, p.Address
                        FROM PassengerAssignments pa
                        JOIN RouteDetails rd ON pa.RouteDetailID = rd.RouteDetailID
                        JOIN Passengers p ON pa.PassengerID = p.PassengerID
                        WHERE rd.RouteID = @RouteID AND rd.VehicleID = @VehicleID
                        ORDER BY pa.StopOrder";
                    cmd.Parameters.AddWithValue("@RouteID", routeId);
                    cmd.Parameters.AddWithValue("@VehicleID", vehicleId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            vehicle.AssignedPassengers.Add(new Passenger
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Latitude = reader.GetDouble(2),
                                Longitude = reader.GetDouble(3),
                                Address = reader.IsDBNull(4) ? null : reader.GetString(4)
                            });
                        }
                    }
                }
            }

            return vehicle;
        }

        /// <summary>
        /// Creates a new vehicle for a user or updates an existing one
        /// </summary>
        public async Task<int> SaveVehicleAsync(int userId, int vehicleId, int capacity, double startLatitude, double startLongitude, string startAddress = "")
        {
            if (vehicleId > 0)
            {
                // Update existing vehicle
                using (var cmd = new SQLiteCommand(connection))
                {
                    cmd.CommandText = @"
                        UPDATE Vehicles
                        SET Capacity = @Capacity, StartLatitude = @StartLatitude, StartLongitude = @StartLongitude, StartAddress = @StartAddress
                        WHERE VehicleID = @VehicleID";
                    cmd.Parameters.AddWithValue("@VehicleID", vehicleId);
                    cmd.Parameters.AddWithValue("@Capacity", capacity);
                    cmd.Parameters.AddWithValue("@StartLatitude", startLatitude);
                    cmd.Parameters.AddWithValue("@StartLongitude", startLongitude);
                    cmd.Parameters.AddWithValue("@StartAddress", startAddress ?? "");

                    await cmd.ExecuteNonQueryAsync();
                    return vehicleId;
                }
            }
            else
            {
                // Create a new vehicle
                using (var cmd = new SQLiteCommand(connection))
                {
                    cmd.CommandText = @"
                        INSERT INTO Vehicles (UserID, Capacity, StartLatitude, StartLongitude, StartAddress, IsAvailableTomorrow)
                        VALUES (@UserID, @Capacity, @StartLatitude, @StartLongitude, @StartAddress, 1);
                        SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("@UserID", userId);
                    cmd.Parameters.AddWithValue("@Capacity", capacity);
                    cmd.Parameters.AddWithValue("@StartLatitude", startLatitude);
                    cmd.Parameters.AddWithValue("@StartLongitude", startLongitude);
                    cmd.Parameters.AddWithValue("@StartAddress", startAddress ?? "");

                    object result = await cmd.ExecuteScalarAsync();
                    if (result != null && int.TryParse(result.ToString(), out int newVehicleId))
                    {
                        return newVehicleId;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Deletes a vehicle from the database
        /// </summary>
        public async Task<bool> DeleteVehicleAsync(int vehicleId)
        {
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // Delete any route assignments for this vehicle
                    using (var cmd = new SQLiteCommand(connection))
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            DELETE FROM PassengerAssignments
                            WHERE RouteDetailID IN (
                                SELECT RouteDetailID FROM RouteDetails
                                WHERE VehicleID = @VehicleID
                            )";
                        cmd.Parameters.AddWithValue("@VehicleID", vehicleId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Delete route details
                    using (var cmd = new SQLiteCommand(connection))
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "DELETE FROM RouteDetails WHERE VehicleID = @VehicleID";
                        cmd.Parameters.AddWithValue("@VehicleID", vehicleId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Delete the vehicle
                    using (var cmd = new SQLiteCommand(connection))
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "DELETE FROM Vehicles WHERE VehicleID = @VehicleID";
                        cmd.Parameters.AddWithValue("@VehicleID", vehicleId);
                        int result = await cmd.ExecuteNonQueryAsync();

                        if (result == 0)
                        {
                            transaction.Rollback();
                            return false;
                        }
                    }

                    transaction.Commit();
                    return true;
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        /// <summary>
        /// Creates or updates a vehicle for a driver
        /// </summary>
        public async Task<int> SaveDriverVehicleAsync(int userId, int capacity, double startLatitude, double startLongitude, string startAddress = "")
        {
            // First check if the driver already has a vehicle
            var existingVehicle = await GetVehicleByUserIdAsync(userId);

            if (existingVehicle != null)
            {
                // Update existing vehicle
                using (var cmd = new SQLiteCommand(connection))
                {
                    cmd.CommandText = @"
                UPDATE Vehicles
                SET Capacity = @Capacity, StartLatitude = @StartLatitude, 
                    StartLongitude = @StartLongitude, StartAddress = @StartAddress
                WHERE UserID = @UserID";
                    cmd.Parameters.AddWithValue("@UserID", userId);
                    cmd.Parameters.AddWithValue("@Capacity", capacity);
                    cmd.Parameters.AddWithValue("@StartLatitude", startLatitude);
                    cmd.Parameters.AddWithValue("@StartLongitude", startLongitude);
                    cmd.Parameters.AddWithValue("@StartAddress", startAddress ?? "");

                    await cmd.ExecuteNonQueryAsync();
                    return existingVehicle.Id;
                }
            }
            else
            {
                // Create a new vehicle
                using (var cmd = new SQLiteCommand(connection))
                {
                    cmd.CommandText = @"
                INSERT INTO Vehicles (UserID, Capacity, StartLatitude, StartLongitude, StartAddress, IsAvailableTomorrow)
                VALUES (@UserID, @Capacity, @StartLatitude, @StartLongitude, @StartAddress, 1);
                SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("@UserID", userId);
                    cmd.Parameters.AddWithValue("@Capacity", capacity);
                    cmd.Parameters.AddWithValue("@StartLatitude", startLatitude);
                    cmd.Parameters.AddWithValue("@StartLongitude", startLongitude);
                    cmd.Parameters.AddWithValue("@StartAddress", startAddress ?? "");

                    object result = await cmd.ExecuteScalarAsync();
                    if (result != null && int.TryParse(result.ToString(), out int vehicleId))
                    {
                        return vehicleId;
                    }
                }
            }

            return -1;
        }



        #endregion

        #region Passenger Methods

        /// <summary>
        /// Adds a new passenger to the database
        /// </summary>
        public async Task<int> AddPassengerAsync(int userId, string name, double latitude, double longitude, string address = "")
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    INSERT INTO Passengers (UserID, Name, Latitude, Longitude, Address)
                    VALUES (@UserID, @Name, @Latitude, @Longitude, @Address);
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@UserID", userId);
                cmd.Parameters.AddWithValue("@Name", name);
                cmd.Parameters.AddWithValue("@Latitude", latitude);
                cmd.Parameters.AddWithValue("@Longitude", longitude);
                cmd.Parameters.AddWithValue("@Address", address ?? "");

                object result = await cmd.ExecuteScalarAsync();
                if (result != null && int.TryParse(result.ToString(), out int passengerId))
                {
                    return passengerId;
                }
            }

            return -1;
        }

        /// <summary>
        /// Updates a passenger's information
        /// </summary>
        public async Task<bool> UpdatePassengerAsync(int passengerId, string name, double latitude, double longitude, string address = "")
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    UPDATE Passengers
                    SET Name = @Name, Latitude = @Latitude, Longitude = @Longitude, Address = @Address
                    WHERE PassengerID = @PassengerID";
                cmd.Parameters.AddWithValue("@PassengerID", passengerId);
                cmd.Parameters.AddWithValue("@Name", name);
                cmd.Parameters.AddWithValue("@Latitude", latitude);
                cmd.Parameters.AddWithValue("@Longitude", longitude);
                cmd.Parameters.AddWithValue("@Address", address ?? "");

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        /// <summary>
        /// Updates a passenger's availability for tomorrow
        /// </summary>
        public async Task<bool> UpdatePassengerAvailabilityAsync(int passengerId, bool isAvailable)
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = "UPDATE Passengers SET IsAvailableTomorrow = @IsAvailable WHERE PassengerID = @PassengerID";
                cmd.Parameters.AddWithValue("@PassengerID", passengerId);
                cmd.Parameters.AddWithValue("@IsAvailable", isAvailable ? 1 : 0);

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        /// <summary>
        /// Gets all passengers that are available for tomorrow
        /// </summary>
        public async Task<List<Passenger>> GetAvailablePassengersAsync()
        {
            var passengers = new List<Passenger>();

            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    SELECT PassengerID, Name, Latitude, Longitude, Address
                    FROM Passengers
                    WHERE IsAvailableTomorrow = 1";

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        passengers.Add(new Passenger
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Latitude = reader.GetDouble(2),
                            Longitude = reader.GetDouble(3),
                            Address = reader.IsDBNull(4) ? null : reader.GetString(4)
                        });
                    }
                }
            }

            return passengers;
        }

        /// <summary>
        /// Gets all passengers in the database, not just those available for tomorrow
        /// </summary>
        public async Task<List<Passenger>> GetAllPassengersAsync()
        {
            var passengers = new List<Passenger>();

            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    SELECT PassengerID, UserID, Name, Latitude, Longitude, Address, IsAvailableTomorrow
                    FROM Passengers";

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        passengers.Add(new Passenger
                        {
                            Id = reader.GetInt32(0),
                            UserId = reader.GetInt32(1),
                            Name = reader.GetString(2),
                            Latitude = reader.GetDouble(3),
                            Longitude = reader.GetDouble(4),
                            Address = reader.IsDBNull(5) ? null : reader.GetString(5),
                            IsAvailableTomorrow = reader.GetInt32(6) == 1
                        });
                    }
                }
            }

            return passengers;
        }

        /// <summary>
        /// Gets a passenger by user ID
        /// </summary>
        public async Task<Passenger> GetPassengerByUserIdAsync(int userId)
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    SELECT PassengerID, Name, Latitude, Longitude, Address, IsAvailableTomorrow
                    FROM Passengers
                    WHERE UserID = @UserID";
                cmd.Parameters.AddWithValue("@UserID", userId);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new Passenger
                        {
                            Id = reader.GetInt32(0),
                            UserId = userId,
                            Name = reader.GetString(1),
                            Latitude = reader.GetDouble(2),
                            Longitude = reader.GetDouble(3),
                            Address = reader.IsDBNull(4) ? null : reader.GetString(4),
                            IsAvailableTomorrow = reader.GetInt32(5) == 1
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a passenger by its ID
        /// </summary>
        public async Task<Passenger> GetPassengerByIdAsync(int passengerId)
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    SELECT PassengerID, UserID, Name, Latitude, Longitude, Address, IsAvailableTomorrow
                    FROM Passengers
                    WHERE PassengerID = @PassengerID";
                cmd.Parameters.AddWithValue("@PassengerID", passengerId);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new Passenger
                        {
                            Id = reader.GetInt32(0),
                            UserId = reader.GetInt32(1),
                            Name = reader.GetString(2),
                            Latitude = reader.GetDouble(3),
                            Longitude = reader.GetDouble(4),
                            Address = reader.IsDBNull(5) ? null : reader.GetString(5),
                            IsAvailableTomorrow = reader.GetInt32(6) == 1
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a new passenger for a user or updates an existing one
        /// </summary>
        public async Task<int> SavePassengerAsync(int userId, int passengerId, string name, double latitude, double longitude, string address = "")
        {
            if (passengerId > 0)
            {
                // Update existing passenger
                using (var cmd = new SQLiteCommand(connection))
                {
                    cmd.CommandText = @"
                        UPDATE Passengers
                        SET Name = @Name, Latitude = @Latitude, Longitude = @Longitude, Address = @Address
                        WHERE PassengerID = @PassengerID";
                    cmd.Parameters.AddWithValue("@PassengerID", passengerId);
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Latitude", latitude);
                    cmd.Parameters.AddWithValue("@Longitude", longitude);
                    cmd.Parameters.AddWithValue("@Address", address ?? "");

                    await cmd.ExecuteNonQueryAsync();
                    return passengerId;
                }
            }
            else
            {
                // Create a new passenger
                using (var cmd = new SQLiteCommand(connection))
                {
                    cmd.CommandText = @"
                        INSERT INTO Passengers (UserID, Name, Latitude, Longitude, Address, IsAvailableTomorrow)
                        VALUES (@UserID, @Name, @Latitude, @Longitude, @Address, 1);
                        SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("@UserID", userId);
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Latitude", latitude);
                    cmd.Parameters.AddWithValue("@Longitude", longitude);
                    cmd.Parameters.AddWithValue("@Address", address ?? "");

                    object result = await cmd.ExecuteScalarAsync();
                    if (result != null && int.TryParse(result.ToString(), out int newPassengerId))
                    {
                        return newPassengerId;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Deletes a passenger from the database
        /// </summary>
        public async Task<bool> DeletePassengerAsync(int passengerId)
        {
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // Delete any route assignments for this passenger
                    using (var cmd = new SQLiteCommand(connection))
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "DELETE FROM PassengerAssignments WHERE PassengerID = @PassengerID";
                        cmd.Parameters.AddWithValue("@PassengerID", passengerId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Delete the passenger
                    using (var cmd = new SQLiteCommand(connection))
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "DELETE FROM Passengers WHERE PassengerID = @PassengerID";
                        cmd.Parameters.AddWithValue("@PassengerID", passengerId);
                        int result = await cmd.ExecuteNonQueryAsync();

                        if (result == 0)
                        {
                            transaction.Rollback();
                            return false;
                        }
                    }

                    transaction.Commit();
                    return true;
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        /// <summary>
        /// Searches for addresses that match the given query
        /// </summary>
        public async Task<List<(string Address, double Latitude, double Longitude)>> SearchAddressesAsync(string query, MapService mapService)
        {
            if (string.IsNullOrWhiteSpace(query) || mapService == null)
                return new List<(string, double, double)>();

            // Try to geocode the address
            var result = await mapService.GeocodeAddressAsync(query);
            if (!result.HasValue)
                return new List<(string, double, double)>();

            // Get the address from the coordinates
            string address = await mapService.ReverseGeocodeAsync(result.Value.Latitude, result.Value.Longitude);

            var results = new List<(string, double, double)>
            {
                (address, result.Value.Latitude, result.Value.Longitude)
            };

            return results;
        }

        #endregion

        #region Destination Methods

        /// <summary>
        /// Gets the destination information
        /// </summary>
        public async Task<(int Id, string Name, double Latitude, double Longitude, string Address, string TargetTime)> GetDestinationAsync()
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    SELECT DestinationID, Name, Latitude, Longitude, Address, TargetArrivalTime
                    FROM Destination
                    ORDER BY DestinationID LIMIT 1";

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return (
                            reader.GetInt32(0),
                            reader.GetString(1),
                            reader.GetDouble(2),
                            reader.GetDouble(3),
                            reader.IsDBNull(4) ? null : reader.GetString(4),
                            reader.GetString(5)
                        );
                    }
                }
            }

            // Return default values if no destination is found
            return (0, "Default", 32.0741, 34.7922, null, "08:00:00");
        }

        /// <summary>
        /// Updates the destination information
        /// </summary>
        public async Task<bool> UpdateDestinationAsync(string name, double latitude, double longitude, string targetTime, string address = "")
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    UPDATE Destination
                    SET Name = @Name, Latitude = @Latitude, Longitude = @Longitude, 
                        Address = @Address, TargetArrivalTime = @TargetArrivalTime
                    WHERE DestinationID = (SELECT DestinationID FROM Destination ORDER BY DestinationID LIMIT 1)";
                cmd.Parameters.AddWithValue("@Name", name);
                cmd.Parameters.AddWithValue("@Latitude", latitude);
                cmd.Parameters.AddWithValue("@Longitude", longitude);
                cmd.Parameters.AddWithValue("@Address", address ?? "");
                cmd.Parameters.AddWithValue("@TargetArrivalTime", targetTime);

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        #endregion

        #region Route Methods

        // Updated SaveSolutionAsync to include pickup times and departure times
        public async Task<int> SaveSolutionAsync(Solution solution, string date)
        {
            // Begin transaction to ensure all related data is saved correctly
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    int routeId;

                    // Insert route record
                    using (var cmd = new SQLiteCommand(connection))
                    {
                        cmd.CommandText = @"
                    INSERT INTO Routes (SolutionDate)
                    VALUES (@SolutionDate);
                    SELECT last_insert_rowid();";
                        cmd.Parameters.AddWithValue("@SolutionDate", date);
                        cmd.Transaction = transaction;

                        object result = await cmd.ExecuteScalarAsync();
                        if (result == null || !int.TryParse(result.ToString(), out routeId))
                        {
                            transaction.Rollback();
                            return -1;
                        }
                    }

                    // Save each vehicle's route
                    foreach (var vehicle in solution.Vehicles)
                    {
                        if (vehicle.AssignedPassengers.Count == 0)
                            continue;

                        int routeDetailId;

                        // Insert route detail including departure time
                        using (var cmd = new SQLiteCommand(connection))
                        {
                            cmd.CommandText = @"
                        INSERT INTO RouteDetails (RouteID, VehicleID, TotalDistance, TotalTime, DepartureTime)
                        VALUES (@RouteID, @VehicleID, @TotalDistance, @TotalTime, @DepartureTime);
                        SELECT last_insert_rowid();";
                            cmd.Parameters.AddWithValue("@RouteID", routeId);
                            cmd.Parameters.AddWithValue("@VehicleID", vehicle.Id);
                            cmd.Parameters.AddWithValue("@TotalDistance", vehicle.TotalDistance);
                            cmd.Parameters.AddWithValue("@TotalTime", vehicle.TotalTime);
                            cmd.Parameters.AddWithValue("@DepartureTime", vehicle.DepartureTime ?? (object)DBNull.Value);
                            cmd.Transaction = transaction;

                            object result = await cmd.ExecuteScalarAsync();
                            if (result == null || !int.TryParse(result.ToString(), out routeDetailId))
                            {
                                transaction.Rollback();
                                return -1;
                            }
                        }

                        // Also update vehicle with departure time
                        using (var cmd = new SQLiteCommand(connection))
                        {
                            cmd.CommandText = @"
                        UPDATE Vehicles 
                        SET DepartureTime = @DepartureTime
                        WHERE VehicleID = @VehicleID";
                            cmd.Parameters.AddWithValue("@VehicleID", vehicle.Id);
                            cmd.Parameters.AddWithValue("@DepartureTime", vehicle.DepartureTime ?? (object)DBNull.Value);
                            cmd.Transaction = transaction;
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Save passenger assignments with estimated pickup times
                        for (int i = 0; i < vehicle.AssignedPassengers.Count; i++)
                        {
                            var passenger = vehicle.AssignedPassengers[i];

                            using (var cmd = new SQLiteCommand(connection))
                            {
                                cmd.CommandText = @"
                            INSERT INTO PassengerAssignments (RouteDetailID, PassengerID, StopOrder, EstimatedPickupTime)
                            VALUES (@RouteDetailID, @PassengerID, @StopOrder, @EstimatedPickupTime)";
                                cmd.Parameters.AddWithValue("@RouteDetailID", routeDetailId);
                                cmd.Parameters.AddWithValue("@PassengerID", passenger.Id);
                                cmd.Parameters.AddWithValue("@StopOrder", i + 1);
                                cmd.Parameters.AddWithValue("@EstimatedPickupTime",
                                    !string.IsNullOrEmpty(passenger.EstimatedPickupTime) ?
                                    passenger.EstimatedPickupTime : (object)DBNull.Value);
                                cmd.Transaction = transaction;

                                await cmd.ExecuteNonQueryAsync();
                            }

                            // Also update passenger with estimated pickup time
                            using (var cmd = new SQLiteCommand(connection))
                            {
                                cmd.CommandText = @"
                            UPDATE Passengers 
                            SET EstimatedPickupTime = @EstimatedPickupTime
                            WHERE PassengerID = @PassengerID";
                                cmd.Parameters.AddWithValue("@PassengerID", passenger.Id);
                                cmd.Parameters.AddWithValue("@EstimatedPickupTime",
                                    !string.IsNullOrEmpty(passenger.EstimatedPickupTime) ?
                                    passenger.EstimatedPickupTime : (object)DBNull.Value);
                                cmd.Transaction = transaction;
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }
                    }

                    // Commit the transaction
                    transaction.Commit();
                    return routeId;
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        // Updated GetDriverRouteAsync to include departure time
        public async Task<(Vehicle Vehicle, List<Passenger> Passengers, DateTime? PickupTime)> GetDriverRouteAsync(int userId, string date)
        {
            // Get driver's vehicle first
            var vehicle = await GetVehicleByUserIdAsync(userId);
            if (vehicle == null)
            {
                return (null, null, null);
            }

            // Get route for this date
            int routeId;
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = "SELECT RouteID FROM Routes WHERE SolutionDate = @SolutionDate ORDER BY GeneratedTime DESC LIMIT 1";
                cmd.Parameters.AddWithValue("@SolutionDate", date);

                object result = await cmd.ExecuteScalarAsync();
                if (result == null || !int.TryParse(result.ToString(), out routeId))
                {
                    return (vehicle, new List<Passenger>(), null);
                }
            }

            // Get route details for this vehicle
            int routeDetailId;
            string departureTime = null;
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
            SELECT RouteDetailID, TotalDistance, TotalTime, DepartureTime
            FROM RouteDetails 
            WHERE RouteID = @RouteID AND VehicleID = @VehicleID";
                cmd.Parameters.AddWithValue("@RouteID", routeId);
                cmd.Parameters.AddWithValue("@VehicleID", vehicle.Id);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        routeDetailId = reader.GetInt32(0);
                        vehicle.TotalDistance = reader.GetDouble(1);
                        vehicle.TotalTime = reader.GetDouble(2);
                        departureTime = reader.IsDBNull(3) ? null : reader.GetString(3);
                        vehicle.DepartureTime = departureTime;
                    }
                    else
                    {
                        return (vehicle, new List<Passenger>(), null);
                    }
                }
            }

            // Get assigned passengers and pickup times
            var passengers = new List<Passenger>();
            DateTime? firstPickupTime = null;

            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
            SELECT pa.PassengerID, pa.StopOrder, pa.EstimatedPickupTime, 
                   p.Name, p.Latitude, p.Longitude, p.Address
            FROM PassengerAssignments pa
            JOIN Passengers p ON pa.PassengerID = p.PassengerID
            WHERE pa.RouteDetailID = @RouteDetailID
            ORDER BY pa.StopOrder";
                cmd.Parameters.AddWithValue("@RouteDetailID", routeDetailId);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var passenger = new Passenger
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(3),
                            Latitude = reader.GetDouble(4),
                            Longitude = reader.GetDouble(5),
                            Address = reader.IsDBNull(6) ? null : reader.GetString(6),
                            EstimatedPickupTime = reader.IsDBNull(2) ? null : reader.GetString(2)
                        };

                        passengers.Add(passenger);

                        if (passengers.Count == 1 && !reader.IsDBNull(2))
                        {
                            firstPickupTime = DateTime.Parse(reader.GetString(2));
                        }
                        else if (passengers.Count == 1 && string.IsNullOrEmpty(passenger.EstimatedPickupTime) && !string.IsNullOrEmpty(departureTime))
                        {
                            // Estimate pickup time based on departure time if not explicitly set
                            DateTime departure = DateTime.Parse(departureTime);
                            firstPickupTime = departure.AddMinutes(15); // Assume 15 min to first passenger
                        }
                    }
                }
            }

            vehicle.AssignedPassengers = passengers;
            return (vehicle, passengers, firstPickupTime);

            /// <summary>
            /// Gets the solution for a specific date
            /// </summary>
            public async Task<Solution> GetSolutionForDateAsync(string date)
        {
            var solution = new Solution { Vehicles = new List<Vehicle>() };

            // Get the route ID for the specific date
            int routeId;
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = "SELECT RouteID FROM Routes WHERE SolutionDate = @SolutionDate ORDER BY GeneratedTime DESC LIMIT 1";
                cmd.Parameters.AddWithValue("@SolutionDate", date);

                object result = await cmd.ExecuteScalarAsync();
                if (result == null || !int.TryParse(result.ToString(), out routeId))
                {
                    return null;
                }
            }

            // Get all route details for this route
            var routeDetails = new Dictionary<int, (int VehicleId, double TotalDistance, double TotalTime)>();
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = "SELECT RouteDetailID, VehicleID, TotalDistance, TotalTime FROM RouteDetails WHERE RouteID = @RouteID";
                cmd.Parameters.AddWithValue("@RouteID", routeId);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int routeDetailId = reader.GetInt32(0);
                        routeDetails[routeDetailId] = (
                            reader.GetInt32(1),
                            reader.GetDouble(2),
                            reader.GetDouble(3)
                        );
                    }
                }
            }

            if (routeDetails.Count == 0)
            {
                return null;
            }

            // Get all vehicles first
            var vehicles = await GetAllVehiclesAsync();
            var vehicleMap = vehicles.ToDictionary(v => v.Id);

            // Get passenger assignments
            foreach (var detail in routeDetails)
            {
                int vehicleId = detail.Value.VehicleId;

                if (!vehicleMap.TryGetValue(vehicleId, out Vehicle vehicle))
                {
                    continue;
                }

                vehicle.TotalDistance = detail.Value.TotalDistance;
                vehicle.TotalTime = detail.Value.TotalTime;

                // Get assigned passengers for this route detail
                using (var cmd = new SQLiteCommand(connection))
                {
                    cmd.CommandText = @"
                        SELECT pa.PassengerID, pa.StopOrder, pa.EstimatedPickupTime, 
                               p.Name, p.Latitude, p.Longitude, p.Address
                        FROM PassengerAssignments pa
                        JOIN Passengers p ON pa.PassengerID = p.PassengerID
                        WHERE pa.RouteDetailID = @RouteDetailID
                        ORDER BY pa.StopOrder";
                    cmd.Parameters.AddWithValue("@RouteDetailID", detail.Key);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var passenger = new Passenger
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(3),
                                Latitude = reader.GetDouble(4),
                                Longitude = reader.GetDouble(5),
                                Address = reader.IsDBNull(6) ? null : reader.GetString(6),
                                EstimatedPickupTime = reader.IsDBNull(2) ? null : reader.GetString(2)
                            };

                            vehicle.AssignedPassengers.Add(passenger);
                        }
                    }
                }

                // Add vehicle to solution
                solution.Vehicles.Add(vehicle);
            }

            return solution;
        }

       


        /// <summary>
        /// Updates the estimated pickup times for a route
        /// </summary>
        public async Task<bool> UpdatePickupTimesAsync(int routeDetailId, Dictionary<int, string> passengerPickupTimes)
        {
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    using (var cmd = new SQLiteCommand(connection))
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            UPDATE PassengerAssignments
                            SET EstimatedPickupTime = @PickupTime
                            WHERE RouteDetailID = @RouteDetailID AND PassengerID = @PassengerID";

                        foreach (var entry in passengerPickupTimes)
                        {
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@RouteDetailID", routeDetailId);
                            cmd.Parameters.AddWithValue("@PassengerID", entry.Key);
                            cmd.Parameters.AddWithValue("@PickupTime", entry.Value);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    transaction.Commit();
                    return true;
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    return false;
                }
            }
        }

        /// <summary>
        /// Updates the vehicle and passenger availability for tomorrow
        /// </summary>
        public async Task ResetAvailabilityAsync()
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                // Reset all vehicles and passengers to available for tomorrow
                cmd.CommandText = "UPDATE Vehicles SET IsAvailableTomorrow = 1";
                await cmd.ExecuteNonQueryAsync();

                cmd.CommandText = "UPDATE Passengers SET IsAvailableTomorrow = 1";
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Gets all routes for a specific date
        /// </summary>
        public async Task<List<(int RouteId, DateTime GeneratedTime, int VehicleCount, int PassengerCount)>> GetRouteHistoryAsync()
        {
            var result = new List<(int RouteId, DateTime GeneratedTime, int VehicleCount, int PassengerCount)>();

            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    SELECT r.RouteID, r.SolutionDate, r.GeneratedTime, 
                           COUNT(DISTINCT rd.VehicleID) as VehicleCount,
                           COUNT(pa.PassengerID) as PassengerCount
                    FROM Routes r
                    LEFT JOIN RouteDetails rd ON r.RouteID = rd.RouteID
                    LEFT JOIN PassengerAssignments pa ON rd.RouteDetailID = pa.RouteDetailID
                    GROUP BY r.RouteID
                    ORDER BY r.SolutionDate DESC, r.GeneratedTime DESC
                    LIMIT 50"; // Limit to recent routes

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.Add((
                            reader.GetInt32(0),
                            DateTime.Parse(reader.GetString(2)),
                            reader.GetInt32(3),
                            reader.GetInt32(4)
                        ));
                    }
                }
            }

            return result;
        }

        #endregion

        #region Scheduling Methods

        /// <summary>
        /// Saves scheduling settings to the database
        /// </summary>
        public async Task SaveSchedulingSettingsAsync(bool isEnabled, DateTime scheduledTime)
        {
            // First check if a settings table exists and create it if not
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Settings (
                        SettingID INTEGER PRIMARY KEY AUTOINCREMENT,
                        SettingName TEXT NOT NULL UNIQUE,
                        SettingValue TEXT NOT NULL
                    )";
                await cmd.ExecuteNonQueryAsync();
            }

            // Save the settings
            using (var cmd = new SQLiteCommand(connection))
            {
                // Save isEnabled setting
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO Settings (SettingName, SettingValue)
                    VALUES (@SettingName, @SettingValue)";
                cmd.Parameters.AddWithValue("@SettingName", "SchedulingEnabled");
                cmd.Parameters.AddWithValue("@SettingValue", isEnabled ? "1" : "0");
                await cmd.ExecuteNonQueryAsync();

                // Save scheduledTime setting
                cmd.Parameters.Clear();
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO Settings (SettingName, SettingValue)
                    VALUES (@SettingName, @SettingValue)";
                cmd.Parameters.AddWithValue("@SettingName", "ScheduledTime");
                cmd.Parameters.AddWithValue("@SettingValue", scheduledTime.ToString("HH:mm:ss"));
                await cmd.ExecuteNonQueryAsync();
            }
        }

      

        /// <summary>
        /// Gets the scheduling log entries
        /// </summary>
        public async Task<List<(DateTime RunTime, string Status, int RoutesGenerated, int PassengersAssigned)>> GetSchedulingLogAsync()
        {
            // First, check if the scheduling log table exists and create it if not
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS SchedulingLog (
                        LogID INTEGER PRIMARY KEY AUTOINCREMENT,
                        RunTime TEXT NOT NULL,
                        Status TEXT NOT NULL,
                        RoutesGenerated INTEGER,
                        PassengersAssigned INTEGER,
                        ErrorMessage TEXT
                    )";
                await cmd.ExecuteNonQueryAsync();
            }

            // Now fetch the data
            var result = new List<(DateTime RunTime, string Status, int RoutesGenerated, int PassengersAssigned)>();

            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    SELECT RunTime, Status, RoutesGenerated, PassengersAssigned
                    FROM SchedulingLog
                    ORDER BY RunTime DESC
                    LIMIT 50"; // Limit to recent entries

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.Add((
                            DateTime.Parse(reader.GetString(0)),
                            reader.GetString(1),
                            reader.GetInt32(2),
                            reader.GetInt32(3)
                        ));
                    }
                }
            }

            return result;
        }

        











        // Methods for passenger assignment management
        public async Task<(Vehicle AssignedVehicle, DateTime? PickupTime)> GetPassengerAssignmentAsync(int userId, string date)
        {
            // Get passenger first
            var passenger = await GetPassengerByUserIdAsync(userId);
            if (passenger == null)
            {
                return (null, null);
            }

            // Get route for this date
            int routeId;
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = "SELECT RouteID FROM Routes WHERE SolutionDate = @SolutionDate ORDER BY GeneratedTime DESC LIMIT 1";
                cmd.Parameters.AddWithValue("@SolutionDate", date);

                object result = await cmd.ExecuteScalarAsync();
                if (result == null || !int.TryParse(result.ToString(), out routeId))
                {
                    return (null, null);
                }
            }

            // Find passenger assignment
            int vehicleId = 0;
            string pickupTime = null;

            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
            SELECT rd.VehicleID, pa.EstimatedPickupTime
            FROM PassengerAssignments pa
            JOIN RouteDetails rd ON pa.RouteDetailID = rd.RouteDetailID
            WHERE rd.RouteID = @RouteID AND pa.PassengerID = @PassengerID";
                cmd.Parameters.AddWithValue("@RouteID", routeId);
                cmd.Parameters.AddWithValue("@PassengerID", passenger.Id);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        vehicleId = reader.GetInt32(0);
                        pickupTime = reader.IsDBNull(1) ? null : reader.GetString(1);
                    }
                    else
                    {
                        return (null, null);
                    }
                }
            }

            // Get vehicle details
            Vehicle vehicle = null;
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
            SELECT v.VehicleID, v.Capacity, v.StartLatitude, v.StartLongitude, v.StartAddress, u.Name
            FROM Vehicles v
            JOIN Users u ON v.UserID = u.UserID
            WHERE v.VehicleID = @VehicleID";
                cmd.Parameters.AddWithValue("@VehicleID", vehicleId);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        vehicle = new Vehicle
                        {
                            Id = reader.GetInt32(0),
                            Capacity = reader.GetInt32(1),
                            StartLatitude = reader.GetDouble(2),
                            StartLongitude = reader.GetDouble(3),
                            StartAddress = reader.IsDBNull(4) ? null : reader.GetString(4),
                            DriverName = reader.GetString(5)
                        };
                    }
                }
            }

            DateTime? pickupDateTime = null;
            if (pickupTime != null)
            {
                pickupDateTime = DateTime.Parse(pickupTime);
            }
            return (vehicle, pickupDateTime);
        }


        // Method for scheduling settings
        public async Task<(bool IsEnabled, DateTime ScheduledTime)> GetSchedulingSettingsAsync()
        {
            bool isEnabled = false;
            DateTime scheduledTime = DateTime.Parse("00:00:00"); // Default to midnight

            // Check if the settings table exists
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Settings'";
                var result = await cmd.ExecuteScalarAsync();
                if (result == null)
                    return (isEnabled, scheduledTime); // Return defaults if table doesn't exist
            }

            // Get isEnabled setting
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = "SELECT SettingValue FROM Settings WHERE SettingName = 'SchedulingEnabled'";
                var result = await cmd.ExecuteScalarAsync();
                if (result != null)
                    isEnabled = result.ToString() == "1";
            }

            // Get scheduledTime setting
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = "SELECT SettingValue FROM Settings WHERE SettingName = 'ScheduledTime'";
                var result = await cmd.ExecuteScalarAsync();
                if (result != null)
                    scheduledTime = DateTime.Parse(result.ToString());
            }

            return (isEnabled, scheduledTime);
        }

        // Update vehicle capacity method
        public async Task<bool> UpdateVehicleCapacityAsync(int userId, int capacity)
        {
            var vehicle = await GetVehicleByUserIdAsync(userId);

            if (vehicle == null)
            {
                // If the vehicle doesn't exist yet, create it with default location
                await SaveDriverVehicleAsync(userId, capacity, 0, 0, "");
                return true;
            }

            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
            UPDATE Vehicles
            SET Capacity = @Capacity
            WHERE UserID = @UserID";
                cmd.Parameters.AddWithValue("@UserID", userId);
                cmd.Parameters.AddWithValue("@Capacity", capacity);

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        // Method for updating vehicle location
        public async Task<bool> UpdateVehicleLocationAsync(int userId, double latitude, double longitude, string address = "")
        {
            var vehicle = await GetVehicleByUserIdAsync(userId);

            if (vehicle == null)
            {
                // If the vehicle doesn't exist yet, create it with default capacity
                await SaveDriverVehicleAsync(userId, 4, latitude, longitude, address);
                return true;
            }

            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
            UPDATE Vehicles
            SET StartLatitude = @Latitude, StartLongitude = @Longitude, StartAddress = @Address
            WHERE UserID = @UserID";
                cmd.Parameters.AddWithValue("@UserID", userId);
                cmd.Parameters.AddWithValue("@Latitude", latitude);
                cmd.Parameters.AddWithValue("@Longitude", longitude);
                cmd.Parameters.AddWithValue("@Address", address ?? "");

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        // Method for logging scheduling runs
        public async Task LogSchedulingRunAsync(DateTime runTime, string status, int routesGenerated, int passengersAssigned, string errorMessage = null)
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
            INSERT INTO SchedulingLog (RunTime, Status, RoutesGenerated, PassengersAssigned, ErrorMessage)
            VALUES (@RunTime, @Status, @RoutesGenerated, @PassengersAssigned, @ErrorMessage)";
                cmd.Parameters.AddWithValue("@RunTime", runTime.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@Status", status);
                cmd.Parameters.AddWithValue("@RoutesGenerated", routesGenerated);
                cmd.Parameters.AddWithValue("@PassengersAssigned", passengersAssigned);
                cmd.Parameters.AddWithValue("@ErrorMessage", errorMessage ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }
        }
        #endregion

        #region Helper Methods

        /// <summary>
        /// Simple password hashing for demo purposes
        /// In a real app, use a proper password hashing library
        /// </summary>
        private string HashPassword(string password)
        {
            // This is NOT secure - use proper password hashing in production
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(password);
                var hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
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
            if (!disposed)
            {
                if (disposing)
                {
                    connection?.Close();
                    connection?.Dispose();
                }

                disposed = true;
            }
        }

        #endregion
    }
}