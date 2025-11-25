using DatabricksService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Build dependency injection container
var services = new ServiceCollection();

// Add logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// Add Databricks services from environment variables
// Required environment variables:
// - DATABRICKS_WORKSPACE_URL: https://adb-xxx.azuredatabricks.net
// - DATABRICKS_WAREHOUSE_ID: your-warehouse-id
// Optional environment variables:
// - DATABRICKS_USE_MANAGED_IDENTITY: true (default) or false
// - DATABRICKS_KEYVAULT_URL: https://your-keyvault.vault.azure.net/
// - DATABRICKS_TOKEN_SECRET_NAME: databricks-pat (default)
// - DATABRICKS_PERSONAL_ACCESS_TOKEN: your-pat-token
// - DATABRICKS_TIMEOUT_SECONDS: 600 (default)
// - DATABRICKS_WAIT_TIMEOUT_SECONDS: 10 (default)
try
{
    services.AddDatabricksServicesFromEnvironment();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Configuration error: {ex.Message}");
    Console.Error.WriteLine("Please set required environment variables:");
    Console.Error.WriteLine("  DATABRICKS_WORKSPACE_URL");
    Console.Error.WriteLine("  DATABRICKS_WAREHOUSE_ID");
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
