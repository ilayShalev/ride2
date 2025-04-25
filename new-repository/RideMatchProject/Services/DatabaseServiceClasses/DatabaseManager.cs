using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.DatabaseServiceClasses
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
}
