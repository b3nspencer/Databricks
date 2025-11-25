using DatabricksService.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DatabricksService;

/// <summary>
/// Extension methods for registering Databricks services in the dependency injection container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Databricks services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddDatabricksServices(
        this IServiceCollection services,
        Action<DatabricksConfig> configureOptions)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }

        // Create and configure the DatabricksConfig
        var config = new DatabricksConfig();
        configureOptions(config);

        // Register configuration as singleton
        services.AddSingleton(config);

        // Register authentication provider
        services.AddSingleton<IDatabricksAuthenticationProvider, DatabricksAuthenticationProvider>();

        // Register HTTP client with proper configuration
        services.AddHttpClient<IDatabricksDataAccessService, DatabricksDataAccessService>()
            .ConfigureHttpClient((provider, client) =>
            {
                var config = provider.GetRequiredService<DatabricksConfig>();
                client.DefaultRequestHeaders.Add("User-Agent", "DatabricksService/.NET6.0");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds * 2); // Give extra time for HTTP timeout
            })
            .ConfigureHttpMessageHandlerBuilder(builder =>
            {
                // Add any custom message handlers here for logging, etc.
            });

        // Register caching service
        services.AddSingleton<IQueryResultCache, QueryResultCache>();

        return services;
    }

    /// <summary>
    /// Adds Databricks services with environment variable configuration
    /// </summary>
    [Obsolete("Use AddDatabricksServicesFromConfiguration instead, which supports multiple config sources")]
    public static IServiceCollection AddDatabricksServicesFromEnvironment(
        this IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        return services.AddDatabricksServices(config =>
        {
            var workspaceUrl = Environment.GetEnvironmentVariable("DATABRICKS_WORKSPACE_URL");
            var warehouseId = Environment.GetEnvironmentVariable("DATABRICKS_WAREHOUSE_ID");
            var useManagedIdentity = bool.TryParse(
                Environment.GetEnvironmentVariable("DATABRICKS_USE_MANAGED_IDENTITY"),
                out var result) ? result : true;
            var keyVaultUrl = Environment.GetEnvironmentVariable("DATABRICKS_KEYVAULT_URL");
            var tokenSecretName = Environment.GetEnvironmentVariable("DATABRICKS_TOKEN_SECRET_NAME") ?? "databricks-pat";
            var personalAccessToken = Environment.GetEnvironmentVariable("DATABRICKS_PERSONAL_ACCESS_TOKEN");
            var timeoutSeconds = int.TryParse(
                Environment.GetEnvironmentVariable("DATABRICKS_TIMEOUT_SECONDS"),
                out var timeout) ? timeout : 600;
            var waitTimeoutSeconds = int.TryParse(
                Environment.GetEnvironmentVariable("DATABRICKS_WAIT_TIMEOUT_SECONDS"),
                out var waitTimeout) ? waitTimeout : 10;

            if (string.IsNullOrWhiteSpace(workspaceUrl))
            {
                throw new InvalidOperationException(
                    "DATABRICKS_WORKSPACE_URL environment variable is required");
            }

            if (string.IsNullOrWhiteSpace(warehouseId))
            {
                throw new InvalidOperationException(
                    "DATABRICKS_WAREHOUSE_ID environment variable is required");
            }

            config.WorkspaceUrl = workspaceUrl;
            config.WarehouseId = warehouseId;
            config.UseManagedIdentity = useManagedIdentity;
            config.KeyVaultUrl = keyVaultUrl;
            config.TokenSecretName = tokenSecretName;
            config.PersonalAccessToken = personalAccessToken;
            config.TimeoutSeconds = timeoutSeconds;
            config.WaitTimeoutSeconds = waitTimeoutSeconds;
        });
    }

    /// <summary>
    /// Adds Databricks services with configuration from multiple sources
    /// Supports User Secrets, environment variables, and appsettings.json
    /// </summary>
    public static IServiceCollection AddDatabricksServicesFromConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        return services.AddDatabricksServices(config =>
        {
            var databricksConfig = configuration.GetSection("Databricks");

            var workspaceUrl = databricksConfig["WorkspaceUrl"];
            var warehouseId = databricksConfig["WarehouseId"];
            var useManagedIdentity = bool.TryParse(
                databricksConfig["UseManagedIdentity"],
                out var result) ? result : true;
            var keyVaultUrl = databricksConfig["KeyVaultUrl"];
            var tokenSecretName = databricksConfig["TokenSecretName"] ?? "databricks-pat";
            var personalAccessToken = databricksConfig["PersonalAccessToken"];
            var timeoutSeconds = int.TryParse(
                databricksConfig["TimeoutSeconds"],
                out var timeout) ? timeout : 600;
            var waitTimeoutSeconds = int.TryParse(
                databricksConfig["WaitTimeoutSeconds"],
                out var waitTimeout) ? waitTimeout : 10;

            if (string.IsNullOrWhiteSpace(workspaceUrl))
            {
                throw new InvalidOperationException(
                    "Databricks:WorkspaceUrl is required. Set via User Secrets, environment variable (Databricks__WorkspaceUrl), or appsettings.json");
            }

            if (string.IsNullOrWhiteSpace(warehouseId))
            {
                throw new InvalidOperationException(
                    "Databricks:WarehouseId is required. Set via User Secrets, environment variable (Databricks__WarehouseId), or appsettings.json");
            }

            config.WorkspaceUrl = workspaceUrl;
            config.WarehouseId = warehouseId;
            config.UseManagedIdentity = useManagedIdentity;
            config.KeyVaultUrl = keyVaultUrl;
            config.TokenSecretName = tokenSecretName;
            config.PersonalAccessToken = personalAccessToken;
            config.TimeoutSeconds = timeoutSeconds;
            config.WaitTimeoutSeconds = waitTimeoutSeconds;
        });
    }
}
