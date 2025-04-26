using System;
using System.Data.SQLite;
using System.Threading.Tasks;

namespace RideMatchProject.Services.DatabaseServiceClasses
{
    /// <summary>
    /// Manages database table operations including creation and verification
    /// </summary>
    public class DatabaseTableManager
    {
        private readonly SQLiteConnection _connection;

        public DatabaseTableManager(SQLiteConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <summary>
        /// Checks if a table exists in the database
        /// </summary>
        public bool TableExists(string tableName)
        {
            using (var cmd = new SQLiteCommand(_connection))
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@TableName";
                cmd.Parameters.AddWithValue("@TableName", tableName);
                var result = cmd.ExecuteScalar();
                return result != null;
            }
        }

        /// <summary>
        /// Creates the RoutePathPoints table if it doesn't exist
        /// </summary>
        public void CreateRoutePathPointsTable()
        {
            if (!TableExists("RoutePathPoints"))
            {
                string query = @"
                CREATE TABLE IF NOT EXISTS RoutePathPoints (
                    PointID INTEGER PRIMARY KEY AUTOINCREMENT,
                    RouteDetailID INTEGER NOT NULL,
                    PointOrder INTEGER NOT NULL,
                    Latitude REAL NOT NULL,
                    Longitude REAL NOT NULL,
                    FOREIGN KEY (RouteDetailID) REFERENCES RouteDetails(RouteDetailID)
                )";

                using (var cmd = new SQLiteCommand(query, _connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}