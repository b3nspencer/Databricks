using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace DatabricksService;

/// <summary>
/// Health check for Databricks connectivity
/// </summary>
public class DatabricksHealthCheck : IHealthCheck
{
    private readonly IDatabricksDataAccessService _dataAccessService;
    private readonly ILogger<DatabricksHealthCheck> _logger;

    public DatabricksHealthCheck(
        IDatabricksDataAccessService dataAccessService,
        ILogger<DatabricksHealthCheck> logger)
    {
        _dataAccessService = dataAccessService ?? throw new ArgumentNullException(nameof(dataAccessService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing Databricks health check...");

            // Execute a simple test query to verify connectivity
            var response = await _dataAccessService.ExecuteRawQueryAsync(
                "SELECT 1 as health_check",
                cancellationToken: cancellationToken
            );

            if (response.State == "SUCCEEDED")
            {
                _logger.LogDebug("Databricks health check passed");
                return HealthCheckResult.Healthy("Databricks connection is healthy");
            }
            else
            {
                _logger.LogWarning("Databricks health check failed with state: {State}", response.State);
                return HealthCheckResult.Unhealthy(
                    $"Query failed with state: {response.State}. Error: {response.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Databricks health check exception: {Message}", ex.Message);
            return HealthCheckResult.Unhealthy("Databricks connection check failed", ex);
        }
    }
}
