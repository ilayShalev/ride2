using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.DatabaseServiceClasses
{
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

}
