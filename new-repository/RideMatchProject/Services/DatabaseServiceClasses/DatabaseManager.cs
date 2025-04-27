using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace RideMatchProject.Services.DatabaseServiceClasses
{
    /// <summary>
    /// Enhanced database manager with robust SQLite initialization
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

            EnsureSQLiteLoaded();
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

        private void EnsureSQLiteLoaded()
        {
            try
            {
                // Try to load SQLite assembly
                var asm = Assembly.Load("System.Data.SQLite");

                // Check for native library paths
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string x86Path = Path.Combine(baseDir, "x86");
                string x64Path = Path.Combine(baseDir, "x64");

                // Add native library paths to PATH environment variable
                if (Directory.Exists(x86Path) || Directory.Exists(x64Path))
                {
                    string path = Environment.GetEnvironmentVariable("PATH") ?? "";
                    if (Directory.Exists(x86Path)) path = x86Path + ";" + path;
                    if (Directory.Exists(x64Path)) path = x64Path + ";" + path;
                    Environment.SetEnvironmentVariable("PATH", path);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning during SQLite initialization: {ex.Message}");
            }
        }

        private void InitializeConnection()
        {
            try
            {
                _connection = new SQLiteConnection(_connectionString);
                _connection.Open();
            }
            catch (DllNotFoundException ex)
            {
                // More helpful error message
                string message = $"Could not find SQLite native library. Please ensure SQLite is properly installed.\n" +
                                 $"Original error: {ex.Message}\n" +
                                 $"Check that SQLite.Interop.dll exists in the application folder or in x86/x64 subfolders.";
                throw new ApplicationException(message, ex);
            }
            catch (Exception ex)
            {
                // Provide more context on other connection errors
                throw new ApplicationException($"Failed to connect to SQLite database: {ex.Message}", ex);
            }
        }

        public SQLiteConnection GetConnection()
        {
            return _connection;
        }

        private void CreateDatabaseSchema()
        {
            try
            {
                var schemaCreator = new DatabaseSchemaCreator(_connection);
                schemaCreator.CreateSchema();

                var defaultDataInserter = new DefaultDataInserter(_connection);
                defaultDataInserter.InsertDefaultData();
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Failed to create database schema: {ex.Message}", ex);
            }
        }

        private void UpdateDatabaseSchema()
        {
            try
            {
                var schemaUpdater = new DatabaseSchemaUpdater(_connection);
                schemaUpdater.UpdateSchema();
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Failed to update database schema: {ex.Message}", ex);
            }
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