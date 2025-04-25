using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.DatabaseServiceClasses
{
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
}
