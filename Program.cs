using DatabricksService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Build configuration from multiple sources (in order of precedence):
// 1. User Secrets (dotnet user-secrets set "key" "value")
// 2. Environment variables
// 3. appsettings.json
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true)
    .Build();

// Build dependency injection container
var services = new ServiceCollection();

// Add configuration to DI container
services.AddSingleton<IConfiguration>(config);

// Add logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// Add Databricks services from configuration
// Supports configuration from multiple sources (in order of precedence):
// 1. User Secrets: dotnet user-secrets set "Databricks:WorkspaceUrl" "https://..."
// 2. Environment variables: DATABRICKS_WORKSPACE_URL or Databricks__WorkspaceUrl
// 3. appsettings.json: { "Databricks": { "WorkspaceUrl": "..." } }
// Required:
// - Databricks:WorkspaceUrl: https://adb-xxx.azuredatabricks.net
// - Databricks:WarehouseId: your-warehouse-id
// Optional:
// - Databricks:UseManagedIdentity: true (default)
// - Databricks:KeyVaultUrl
// - Databricks:TokenSecretName: databricks-pat (default)
// - Databricks:PersonalAccessToken
// - Databricks:TimeoutSeconds: 600 (default)
// - Databricks:WaitTimeoutSeconds: 10 (default)
try
{
    services.AddDatabricksServicesFromConfiguration(config);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Configuration error: {ex.Message}");
    Console.Error.WriteLine("Please configure Databricks settings via:");
    Console.Error.WriteLine("  1. User Secrets: dotnet user-secrets set \"Databricks:WorkspaceUrl\" \"https://...\"");
    Console.Error.WriteLine("  2. Environment variables: DATABRICKS_WORKSPACE_URL");
    Console.Error.WriteLine("  3. appsettings.json");
    Environment.Exit(1);
}

var serviceProvider = services.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Databricks Service initialized successfully");

// Example: Execute a simple test query
try
{
    var dataService = serviceProvider.GetRequiredService<IDatabricksDataAccessService>();
    logger.LogInformation("Executing test query...");

    // This is a simple test query - modify as needed
    var result = await dataService.ExecuteRawQueryAsync("SELECT 1 as test_value");
    logger.LogInformation("Test query successful. State: {State}", result.State);
}
catch (Exception ex)
{
    logger.LogError(ex, "Error executing test query: {Message}", ex.Message);
}
