using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.DatabaseServiceClasses
{

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
            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS RoutePathPoints (
                    PointID INTEGER PRIMARY KEY AUTOINCREMENT,
                    RouteDetailID INTEGER NOT NULL,
                    PointOrder INTEGER NOT NULL,
                    Latitude REAL NOT NULL,
                    Longitude REAL NOT NULL,
                    FOREIGN KEY (RouteDetailID) REFERENCES RouteDetails(RouteDetailID)
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

}
