using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.DatabaseServiceClasses
{
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

}
