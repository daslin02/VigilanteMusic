using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;   
using System.Threading.Tasks;
using VigilanteMusic.Helpers;

namespace VigilanteMusic.DataBase
{
    /// <summary>
    /// Owns the connection string and produces open connections on demand.
    /// Connections are short-lived: pooling is handled by the provider itself.
    /// </summary>
    public class DBConnector : IDisposable
    {
        private readonly string _connectionString;
        private bool _disposed;

        /// <summary>Human readable server name, useful for the UI status bar.</summary>
        public string ServerName { get; }

        /// <summary>Database name taken from the connection string.</summary>
        public string DatabaseName { get; }

        /// <summary>
        /// Creates a connector. When connectionString is null it is resolved
        /// from the environment / local file / App.config.
        /// </summary>
        /// <exception cref="InvalidOperationException">No connection string could be found.</exception>
        public DBConnector(string connectionString = null)
        {
            _connectionString = ConnectionStringProvider.Resolve(connectionString);

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string not found. Set the '{ConnectionStringProvider.EnvVariableName}' " +
                    $"environment variable, place '{ConnectionStringProvider.LocalFileName}' next to the exe, " +
                    "or pass it explicitly.");
            }

            try
            {
                var builder = new SqlConnectionStringBuilder(_connectionString);
                ServerName = builder.DataSource;
                DatabaseName = builder.InitialCatalog;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Malformed connection string.", ex);
            }
        }

        /// <summary>Connection string with the password masked, safe for logging.</summary>
        public string SafeConnectionString => ConnectionStringProvider.Mask(_connectionString);

        /// <summary>
        /// Opens a new connection. The caller must dispose it (use a using block).
        /// </summary>
        public SqlConnection OpenConnection()
        {
            ThrowIfDisposed();

            var connection = new SqlConnection(_connectionString);
            try
            {
                connection.Open();
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        public async Task<SqlConnection> OpenConnectionAsync()
        {
            ThrowIfDisposed();

            var connection = new SqlConnection(_connectionString);
            try
            {
                await connection.OpenAsync().ConfigureAwait(false);
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Attempts to open a connection. Returns false instead of throwing on failure.
        /// </summary>
        public bool TestConnection(out string error)
        {
            error = null;
            try
            {
                using (var connection = OpenConnection())
                {
                    return connection.State == ConnectionState.Open;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>Checks whether the Tracks table exists.</summary>
        public bool TracksTableExists()
        {
            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT OBJECT_ID('dbo.Tracks', 'U');";
                object result = command.ExecuteScalar();
                return result != null && result != DBNull.Value;
            }
        }

        /// <summary>
        /// Runs a query and maps every row through the supplied function.
        /// </summary>
        public List<T> Query<T>(string sql, Func<IDataRecord, T> map, params SqlParameter[] parameters)
        {
            var results = new List<T>();

            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                if (parameters != null) command.Parameters.AddRange(parameters);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        results.Add(map(reader));
                }
            }
            return results;
        }

        /// <summary>Executes a non-query statement and returns the affected row count.</summary>
        public int Execute(string sql, params SqlParameter[] parameters)
        {
            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                if (parameters != null) command.Parameters.AddRange(parameters);
                return command.ExecuteNonQuery();
            }
        }

        /// <summary>Executes a scalar query.</summary>
        public object Scalar(string sql, params SqlParameter[] parameters)
        {
            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                if (parameters != null) command.Parameters.AddRange(parameters);
                return command.ExecuteScalar();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DBConnector));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // SqlConnection instances are disposed by their owners; nothing to release here.
        }
    }
}