using System;
using System.Configuration;
using System.IO;

namespace VigilanteMusic.Helpers
{
    /// <summary>
    /// Resolves the database connection string from environment variables,
    /// a local config file or App.config, in that order of priority.
    /// </summary>
    public static class ConnectionStringProvider
    {
        /// <summary>Name of the environment variable holding the connection string.</summary>
        public const string EnvVariableName = "VIGILANTE_DB";

        /// <summary>Fallback file placed next to the executable.</summary>
        public const string LocalFileName = "connection.txt";

        /// <summary>
        /// Returns the first non-empty connection string found, or null.
        /// </summary>
        /// <param name="explicitValue">Value passed directly by the caller, wins over everything.</param>
        public static string Resolve(string explicitValue = null)
        {
            if (!string.IsNullOrWhiteSpace(explicitValue))
                return explicitValue.Trim();

            // 1) process-level variable (set in the current session or inherited)
            string value = Environment.GetEnvironmentVariable(EnvVariableName);
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();

            // 2) user-level variable, read directly from the registry-backed store.
            //    Needed when the process was started before the variable was created.
            value = Environment.GetEnvironmentVariable(EnvVariableName, EnvironmentVariableTarget.User);
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();

            // 3) machine-level variable
            value = Environment.GetEnvironmentVariable(EnvVariableName, EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();

            // 4) plain text file next to the exe - convenient for portable deployment
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LocalFileName);
            if (File.Exists(filePath))
            {
                value = File.ReadAllText(filePath).Trim();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }


            return null;
        }

        /// <summary>
        /// Stores the connection string as a persistent user-level environment variable.
        /// Already running processes will not see the change until restarted.
        /// </summary>
        public static bool SaveToEnvironment(string connectionString, bool machineWide = false)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return false;

            try
            {
                var target = machineWide
                    ? EnvironmentVariableTarget.Machine
                    : EnvironmentVariableTarget.User;

                Environment.SetEnvironmentVariable(EnvVariableName, connectionString, target);

                // make it visible to the current process immediately
                Environment.SetEnvironmentVariable(EnvVariableName, connectionString, EnvironmentVariableTarget.Process);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConnectionStringProvider] Save failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Removes the variable from the user store.</summary>
        public static bool RemoveFromEnvironment()
        {
            try
            {
                Environment.SetEnvironmentVariable(EnvVariableName, null, EnvironmentVariableTarget.User);
                Environment.SetEnvironmentVariable(EnvVariableName, null, EnvironmentVariableTarget.Process);
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Builds a SQL Server connection string from parts.
        /// </summary>
        public static string BuildSqlServer(string server, string database,
                                            bool trustedConnection = true,
                                            string user = null, string password = null,
                                            int timeoutSeconds = 15)
        {
            var builder = new System.Data.SqlClient.SqlConnectionStringBuilder
            {
                DataSource = server,
                InitialCatalog = database,
                ConnectTimeout = timeoutSeconds,
                TrustServerCertificate = true
            };

            if (trustedConnection)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.IntegratedSecurity = false;
                builder.UserID = user;
                builder.Password = password;
            }

            return builder.ConnectionString;
        }

        /// <summary>
        /// Masks the password so the string can be safely written to logs.
        /// </summary>
        public static string Mask(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString)) return "(empty)";

            try
            {
                var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
                if (!string.IsNullOrEmpty(builder.Password)) builder.Password = "********";
                return builder.ConnectionString;
            }
            catch
            {
                return "(invalid connection string)";
            }
        }
    }
}