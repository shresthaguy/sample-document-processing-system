using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentProcessor.Web.Configuration
{
    /// <summary>Strongly typed "Database.*" settings.</summary>
    public class DatabaseOptions
    {
        public const string SectionName = "Database";

        public bool UseSecretsManager { get; set; } = false;

        public string SecretDescriptionPrefix { get; set; } = "Password for RDS MSSQL used for MAM417.";
    }

    /// <summary>Strongly typed "Bedrock.*" settings.</summary>
    public class BedrockOptions
    {
        public const string SectionName = "Bedrock";

        public string Region { get; set; } = "us-east-1";

        public string SummarizationModelId { get; set; } = "global.anthropic.claude-sonnet-5";

        public int MaxTokens { get; set; } = 2000;

        /// <summary>Characters of extracted text sent to the model.</summary>
        public int MaxInputCharacters { get; set; } = 10000;

        public int MaxPdfPages { get; set; } = 5;
    }

    /// <summary>Strongly typed "Storage.*" settings.</summary>
    public class StorageOptions
    {
        public const string SectionName = "Storage";

        /// <summary>Virtual path; resolve with HostingEnvironment.MapPath before use.</summary>
        public string RootPath { get; set; } = "~/App_Data/uploads";

        public int MaxFileSizeMegabytes { get; set; } = 50;

        public int MaxFilesPerUpload { get; set; } = 10;

        public string[] AllowedExtensions { get; set; } = new[] { ".pdf", ".txt", ".log" };
    }

    /// <summary>
    /// Settings bound from IConfiguration (appsettings.json) into the strongly typed
    /// <see cref="DatabaseOptions"/>, <see cref="BedrockOptions"/> and <see cref="StorageOptions"/>.
    /// Keys are read in the flat "Section.Key" form used by appsettings.json (carried over from
    /// Web.config appSettings) and also in the nested "Section:Key" form. Values are parsed when
    /// <see cref="Configure"/> runs, so a bad value fails at startup with a FormatException.
    /// The static properties keep the original surface for existing callers.
    /// </summary>
    public static class AppSettings
    {
        private const string ConnectionStringName = "DefaultConnection";

        private static IConfiguration _configuration;
        private static DatabaseOptions _database;
        private static BedrockOptions _bedrock;
        private static StorageOptions _storage;

        /// <summary>Binds all options from configuration. Call once at startup.</summary>
        public static void Configure(IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var database = new DatabaseOptions();
            Bind(configuration, database);
            var bedrock = new BedrockOptions();
            Bind(configuration, bedrock);
            var storage = new StorageOptions();
            Bind(configuration, storage);

            _database = database;
            _bedrock = bedrock;
            _storage = storage;
            _configuration = configuration;
        }

        /// <summary>
        /// Registers the strongly typed options so services can take
        /// IOptions&lt;DatabaseOptions&gt;, IOptions&lt;BedrockOptions&gt; or IOptions&lt;StorageOptions&gt;.
        /// </summary>
        public static IServiceCollection AddAppSettingsOptions(this IServiceCollection services, IConfiguration configuration)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            services.Configure<DatabaseOptions>(o => Bind(configuration, o));
            services.Configure<BedrockOptions>(o => Bind(configuration, o));
            services.Configure<StorageOptions>(o => Bind(configuration, o));
            return services;
        }

        public static void Bind(IConfiguration configuration, DatabaseOptions options)
        {
            options.UseSecretsManager = GetBoolean(configuration, DatabaseOptions.SectionName, "UseSecretsManager", options.UseSecretsManager);
            options.SecretDescriptionPrefix = GetString(configuration, DatabaseOptions.SectionName, "SecretDescriptionPrefix", options.SecretDescriptionPrefix);
        }

        public static void Bind(IConfiguration configuration, BedrockOptions options)
        {
            options.Region = GetString(configuration, BedrockOptions.SectionName, "Region", options.Region);
            options.SummarizationModelId = GetString(configuration, BedrockOptions.SectionName, "SummarizationModelId", options.SummarizationModelId);
            options.MaxTokens = GetInt32(configuration, BedrockOptions.SectionName, "MaxTokens", options.MaxTokens);
            options.MaxInputCharacters = GetInt32(configuration, BedrockOptions.SectionName, "MaxInputCharacters", options.MaxInputCharacters);
            options.MaxPdfPages = GetInt32(configuration, BedrockOptions.SectionName, "MaxPdfPages", options.MaxPdfPages);
        }

        public static void Bind(IConfiguration configuration, StorageOptions options)
        {
            options.RootPath = GetString(configuration, StorageOptions.SectionName, "RootPath", options.RootPath);
            options.MaxFileSizeMegabytes = GetInt32(configuration, StorageOptions.SectionName, "MaxFileSizeMegabytes", options.MaxFileSizeMegabytes);
            options.MaxFilesPerUpload = GetInt32(configuration, StorageOptions.SectionName, "MaxFilesPerUpload", options.MaxFilesPerUpload);
            var raw = GetString(configuration, StorageOptions.SectionName, "AllowedExtensions", null);
            if (raw != null)
            {
                options.AllowedExtensions = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        public static DatabaseOptions Database
        {
            get { return _database ?? throw NotConfigured(); }
        }

        public static BedrockOptions Bedrock
        {
            get { return _bedrock ?? throw NotConfigured(); }
        }

        public static StorageOptions Storage
        {
            get { return _storage ?? throw NotConfigured(); }
        }

        public static string ConnectionString
        {
            get
            {
                var configuration = _configuration ?? throw NotConfigured();
                var connectionString = configuration.GetConnectionString(ConnectionStringName);
                if (connectionString == null)
                {
                    throw new InvalidOperationException(
                        "The 'DefaultConnection' connection string is missing from appsettings.json (ConnectionStrings section).");
                }

                return connectionString;
            }
        }

        public static bool DatabaseUseSecretsManager
        {
            get { return Database.UseSecretsManager; }
        }

        public static string DatabaseSecretDescriptionPrefix
        {
            get { return Database.SecretDescriptionPrefix; }
        }

        public static string BedrockRegion
        {
            get { return Bedrock.Region; }
        }

        public static string BedrockSummarizationModelId
        {
            get { return Bedrock.SummarizationModelId; }
        }

        public static int BedrockMaxTokens
        {
            get { return Bedrock.MaxTokens; }
        }

        /// <summary>Characters of extracted text sent to the model.</summary>
        public static int BedrockMaxInputCharacters
        {
            get { return Bedrock.MaxInputCharacters; }
        }

        public static int BedrockMaxPdfPages
        {
            get { return Bedrock.MaxPdfPages; }
        }

        /// <summary>Virtual path; resolve with HostingEnvironment.MapPath before use.</summary>
        public static string StorageRootPath
        {
            get { return Storage.RootPath; }
        }

        public static int StorageMaxFileSizeMegabytes
        {
            get { return Storage.MaxFileSizeMegabytes; }
        }

        public static long StorageMaxFileSizeBytes
        {
            get { return StorageMaxFileSizeMegabytes * 1024L * 1024L; }
        }

        public static int StorageMaxFilesPerUpload
        {
            get { return Storage.MaxFilesPerUpload; }
        }

        public static string[] StorageAllowedExtensions
        {
            get { return (string[])Storage.AllowedExtensions.Clone(); }
        }

        private static InvalidOperationException NotConfigured()
        {
            return new InvalidOperationException(
                "AppSettings.Configure(IConfiguration) was not called at startup.");
        }

        /// <summary>Reads "Section.Key" (flat, as in appsettings.json) or "Section:Key" (nested).</summary>
        private static string GetRaw(IConfiguration configuration, string section, string key)
        {
            var value = configuration[section + "." + key];
            if (string.IsNullOrEmpty(value))
            {
                value = configuration[section + ":" + key];
            }

            return value;
        }

        private static string GetString(IConfiguration configuration, string section, string key, string fallback)
        {
            var value = GetRaw(configuration, section, key);
            return string.IsNullOrEmpty(value) ? fallback : value.Trim();
        }

        private static int GetInt32(IConfiguration configuration, string section, string key, int fallback)
        {
            var value = GetRaw(configuration, section, key);
            return string.IsNullOrEmpty(value) ? fallback : int.Parse(value.Trim());
        }

        private static bool GetBoolean(IConfiguration configuration, string section, string key, bool fallback)
        {
            var value = GetRaw(configuration, section, key);
            return string.IsNullOrEmpty(value) ? fallback : bool.Parse(value.Trim());
        }
    }
}
