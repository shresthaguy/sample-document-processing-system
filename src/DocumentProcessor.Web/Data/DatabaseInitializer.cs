using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Microsoft.Data.SqlClient;

namespace DocumentProcessor.Web.Data
{
    /// <summary>
    /// Creates the database and the Documents table on first run. Kept out of Entity
    /// Framework on purpose: a plain script is what the .NET 10 build of this application
    /// has to match, and it lets both builds point at the same database.
    /// </summary>
    public static class DatabaseInitializer
    {
        public static void EnsureSchema(string connectionString, IWebHostEnvironment environment)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            var builder = new SqlConnectionStringBuilder(connectionString);
            var databaseName = builder.InitialCatalog;
            CreateDatabaseIfMissing(builder, databaseName);
            RunSchemaScript(connectionString, environment);
        }

        private static void CreateDatabaseIfMissing(SqlConnectionStringBuilder builder, string databaseName)
        {
            if (string.IsNullOrEmpty(databaseName))
            {
                return;
            }

            // Reconnect to master; you cannot CREATE DATABASE from inside the database
            // you are trying to create.
            var master = new SqlConnectionStringBuilder(builder.ConnectionString)
            {
                InitialCatalog = "master"
            };
            const string sql = "IF DB_ID(@name) IS NULL " + "BEGIN " + "  DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@name); " + "  EXEC sp_executesql @sql; " + "END";
            using (var connection = new SqlConnection(master.ConnectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@name", databaseName);
                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        private static void RunSchemaScript(string connectionString, IWebHostEnvironment environment)
        {
            // App_Data lives under the content root (not wwwroot), matching the csproj Content item.
            var scriptPath = Path.Combine(environment.ContentRootPath, "App_Data", "Schema.sql");
            var script = File.ReadAllText(scriptPath);
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(script, connection))
            {
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }
}