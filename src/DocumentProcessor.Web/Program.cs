using Amazon;
using Amazon.BedrockRuntime;
using Amazon.SecretsManager;
using DocumentProcessor.Web;
using DocumentProcessor.Web.Components;
using DocumentProcessor.Web.Configuration;
using DocumentProcessor.Web.Data;
using DocumentProcessor.Web.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

// Application entry point. Replaces Global.asax / Global.asax.cs (Application_Start and
// Application_Error) with the ASP.NET Core minimal host for a Blazor Server app.
var builder = WebApplication.CreateBuilder(args);

// Application_Start relied on Web.config appSettings; the Bedrock.*, Storage.* and Database.*
// settings now come from appsettings.json. The static AppSettings surface is bound first so
// DatabaseConnectionResolver and the services can read it, then the IOptions<T> are registered.
AppSettings.Configure(builder.Configuration);
builder.Services.AddAppSettingsOptions(builder.Configuration);

// [PORT-NOTE] dropped Trace.Listeners/TextWriterTraceListener (App_Data/trace.log): logging goes
// through ILogger, configured by the "Logging" section of appsettings.json.
// [PORT-NOTE] dropped ServicePointManager.SecurityProtocol |= Tls12: .NET negotiates TLS 1.2+
// from the operating system defaults, and the AWS SDK requires nothing more.

// Upload limits. Legacy Web.config allowed 512000 KB per request (httpRuntime maxRequestLength
// and IIS requestLimits); the same budget is one full upload batch:
// Storage.MaxFileSizeMegabytes x Storage.MaxFilesPerUpload.
long maxUploadBytes = AppSettings.Storage.MaxFileSizeMegabytes * 1024L * 1024L * AppSettings.Storage.MaxFilesPerUpload;

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxUploadBytes;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxUploadBytes;
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        // InputFile streams each file over the circuit in segments, so the hub message limit
        // does not need to hold a whole file (raising it to maxUploadBytes would let a single
        // client message reserve hundreds of megabytes). 1 MB lifts the 32 KB default enough
        // for fast segment transfer of the uploads allowed above.
        options.MaximumReceiveMessageSize = 1024 * 1024;
    });

// Database connection: AWS Secrets Manager first (when Database.UseSecretsManager is true),
// falling back to ConnectionStrings:DefaultConnection. Resolved before the container is built
// because AddDbContext needs the connection string. The Secrets Manager client is created from
// the "AWS" configuration section and registered below, so the app shares this one instance.
var awsOptions = builder.Configuration.GetAWSOptions();
var secretsManager = awsOptions.CreateServiceClient<IAmazonSecretsManager>();
var connection = await DatabaseConnectionResolver.ResolveAsync(secretsManager);

// Replaces Application["DatabaseInfo"]; MainLayout reads it for the database banner and footer.
builder.Services.AddSingleton<DatabaseInfo>(connection.Info);

builder.Services.AddDbContext<DocumentDbContext>(options =>
    options.UseSqlServer(connection.ConnectionString));

// AWS clients. The default options come from the "AWS" configuration section (profile, region);
// the Bedrock client keeps the legacy Bedrock.Region setting the summarizer was built with.
builder.Services.AddDefaultAWSOptions(awsOptions);
var bedrockAwsOptions = builder.Configuration.GetAWSOptions();
bedrockAwsOptions.Region = RegionEndpoint.GetBySystemName(AppSettings.Bedrock.Region);
builder.Services.AddAWSService<IAmazonBedrockRuntime>(bedrockAwsOptions);
builder.Services.AddSingleton<IAmazonSecretsManager>(secretsManager);

// Document services used by Components/Pages/Default.razor through IServiceScopeFactory.
// FileSystemDocumentStorage takes IWebHostEnvironment; BedrockDocumentSummarizer takes the
// IAmazonBedrockRuntime registered above; DocumentTextExtractor is parameterless; DocumentPipeline
// is built from DocumentDbContext and the other three.
builder.Services.AddScoped<IDocumentStorage, FileSystemDocumentStorage>();
builder.Services.AddScoped<IDocumentSummarizer, BedrockDocumentSummarizer>();
builder.Services.AddScoped<DocumentTextExtractor>();
builder.Services.AddScoped<DocumentPipeline>();

// Application_Error: log every unhandled exception, then let UseExceptionHandler re-execute
// the error page.
builder.Services.AddExceptionHandler<UnhandledExceptionLogger>();

var app = builder.Build();

if (connection.Warning != null)
{
    app.Logger.LogWarning("{DatabaseConnectionWarning}", connection.Warning);
}

// Application_Start: create the database and the Documents table from App_Data/Schema.sql.
// A failure must not take the application down; the pages report a failure to read documents.
try
{
    DatabaseInitializer.EnsureSchema(connection.ConnectionString, app.Environment);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Could not prepare the database.");
}

// Legacy customErrors: mode=RemoteOnly (Release: On), defaultRedirect=~/ErrorPage.aspx, and
// 404 -> ~/ErrorPage.aspx?code=404. Outside Development unhandled exceptions re-execute the
// ErrorPage component; in Development the developer exception page shows the details.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/ErrorPage", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/ErrorPage", "?code={0}");

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

namespace DocumentProcessor.Web
{
    /// <summary>
    /// Logs unhandled request exceptions (the legacy Application_Error trace) and returns
    /// false so the exception handler middleware still re-executes the error page.
    /// </summary>
    internal sealed class UnhandledExceptionLogger : IExceptionHandler
    {
        private readonly ILogger<UnhandledExceptionLogger> _logger;

        public UnhandledExceptionLogger(ILogger<UnhandledExceptionLogger> logger)
        {
            _logger = logger;
        }

        public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Unhandled application error on {Method} {Path}.", httpContext.Request.Method, httpContext.Request.Path);
            return ValueTask.FromResult(false);
        }
    }
}
