using System.Text.Json;
using System.Text.Json.Serialization;
using DatabricksService.Models;
using Microsoft.Extensions.Logging;

namespace DatabricksService;

/// <summary>
/// Service for accessing data from Azure Databricks using the SQL Statement Execution API 2.0
/// </summary>
public interface IDatabricksDataAccessService
{
    /// <summary>
    /// Executes a SQL query and returns results as deserialized objects
    /// </summary>
    Task<List<T>> ExecuteQueryAsync<T>(
        string query,
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Executes a SQL query and returns raw results
    /// </summary>
    Task<StatementExecutionResponse> ExecuteRawQueryAsync(
        string query,
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a query and streams results to handle large datasets
    /// </summary>
    Task ExecuteQueryAsyncStream<T>(
        string query,
        Func<T, Task> processRowAsync,
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default) where T : class;
}

public class DatabricksDataAccessService : IDatabricksDataAccessService
{
    private readonly HttpClient _httpClient;
    private readonly IDatabricksAuthenticationProvider _authProvider;
    private readonly DatabricksConfig _config;
    private readonly ILogger<DatabricksDataAccessService> _logger;

    private const string StatementExecutionApiPath = "/api/2.0/sql/statements";
    private const string StatementGetApiPath = "/api/2.0/sql/statements/{0}";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public DatabricksDataAccessService(
        HttpClient httpClient,
        IDatabricksAuthenticationProvider authProvider,
        DatabricksConfig config,
        ILogger<DatabricksDataAccessService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authProvider = authProvider ?? throw new ArgumentNullException(nameof(authProvider));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ConfigureHttpClient();
    }

    public async Task<List<T>> ExecuteQueryAsync<T>(
        string query,
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default) where T : class
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be empty", nameof(query));
        }

        var response = await ExecuteRawQueryAsync(query, parameters, cancellationToken);

        if (response.State != "SUCCEEDED")
        {
            throw new InvalidOperationException(
                $"Query execution failed with state '{response.State}'. Error: {response.ErrorMessage}");
        }

        if (response.Result?.DataArray == null || response.Result.DataArray.Count == 0)
        {
            return new List<T>();
        }

        var results = new List<T>();

        foreach (var row in response.Result.DataArray)
        {
            var jsonElement = JsonSerializer.SerializeToElement(row);
            var item = JsonSerializer.Deserialize<T>(jsonElement, JsonOptions);
            if (item != null)
            {
                results.Add(item);
            }
        }

        return results;
    }

    public async Task<StatementExecutionResponse> ExecuteRawQueryAsync(
        string query,
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be empty", nameof(query));
        }

        _logger.LogInformation("Executing query: {Query}", query);

        var request = BuildStatementRequest(query, parameters);
        var content = new StringContent(
            JsonSerializer.Serialize(request, JsonOptions),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var response = await ExecuteWithRetryAsync(
            () => _httpClient.PostAsync(
                $"{_config.WorkspaceUrl}{StatementExecutionApiPath}",
                content,
                cancellationToken
            ),
            "Execute query",
            cancellationToken
        );

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Query execution failed with status {StatusCode}: {ResponseContent}",
                response.StatusCode, responseContent);
            throw new HttpRequestException(
                $"Databricks API returned {response.StatusCode}: {responseContent}");
        }

        var executionResponse = JsonSerializer.Deserialize<StatementExecutionResponse>(
            responseContent, JsonOptions);

        if (executionResponse == null)
        {
            throw new InvalidOperationException("Failed to parse Databricks response");
        }

        // Poll for result if statement is still executing
        while (executionResponse.State == "PENDING" || executionResponse.State == "RUNNING")
        {
            _logger.LogDebug("Statement {StatementId} is in state {State}, polling for result...",
                executionResponse.StatementId, executionResponse.State);

            await Task.Delay(TimeSpan.FromSeconds(_config.WaitTimeoutSeconds), cancellationToken);

            executionResponse = await PollForResultAsync(
                executionResponse.StatementId!,
                cancellationToken
            );
        }

        if (executionResponse.State == "SUCCEEDED")
        {
            _logger.LogInformation("Query executed successfully. Returned {RowCount} rows",
                executionResponse.Result?.RowCount ?? 0);
        }
        else
        {
            _logger.LogError("Query failed with state {State}. Error: {ErrorMessage}",
                executionResponse.State, executionResponse.ErrorMessage);
        }

        return executionResponse;
    }

    public async Task ExecuteQueryAsyncStream<T>(
        string query,
        Func<T, Task> processRowAsync,
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default) where T : class
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be empty", nameof(query));
        }

        if (processRowAsync == null)
        {
            throw new ArgumentNullException(nameof(processRowAsync));
        }

        var response = await ExecuteRawQueryAsync(query, parameters, cancellationToken);

        if (response.State != "SUCCEEDED")
        {
            throw new InvalidOperationException(
                $"Query execution failed with state '{response.State}'. Error: {response.ErrorMessage}");
        }

        if (response.Result?.DataArray == null || response.Result.DataArray.Count == 0)
        {
            _logger.LogInformation("Query returned no rows");
            return;
        }

        _logger.LogInformation("Processing {RowCount} rows", response.Result.DataArray.Count);

        foreach (var row in response.Result.DataArray)
        {
            var jsonElement = JsonSerializer.SerializeToElement(row);
            var item = JsonSerializer.Deserialize<T>(jsonElement, JsonOptions);
            if (item != null)
            {
                await processRowAsync(item);
            }
        }

        _logger.LogInformation("Completed processing all rows");
    }

    private StatementExecutionRequest BuildStatementRequest(
        string query,
        Dictionary<string, string>? parameters)
    {
        var request = new StatementExecutionRequest
        {
            Statement = query,
            WarehouseId = _config.WarehouseId,
            TimeoutSeconds = _config.TimeoutSeconds,
            Disposition = "INLINE" // Use external links for results > 25MB
        };

        if (_config.MaxRowLimit > 0)
        {
            request.RowLimit = _config.MaxRowLimit;
        }

        if (parameters != null && parameters.Count > 0)
        {
            request.Parameters = parameters
                .Select(kvp => new StatementParameter
                {
                    Name = kvp.Key,
                    Value = kvp.Value,
                    Type = "STRING"
                })
                .ToList();
        }

        return request;
    }

    private async Task<StatementExecutionResponse> PollForResultAsync(
        string statementId,
        CancellationToken cancellationToken)
    {
        var response = await ExecuteWithRetryAsync(
            () => _httpClient.GetAsync(
                $"{_config.WorkspaceUrl}{string.Format(StatementGetApiPath, statementId)}",
                cancellationToken
            ),
            "Poll statement result",
            cancellationToken
        );

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Failed to poll statement result. Status: {response.StatusCode}, Content: {responseContent}");
        }

        var result = JsonSerializer.Deserialize<StatementExecutionResponse>(
            responseContent, JsonOptions);

        return result ?? throw new InvalidOperationException("Failed to parse poll response");
    }

    private async Task<HttpResponseMessage> ExecuteWithRetryAsync(
        Func<Task<HttpResponseMessage>> requestFunc,
        string operationName,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        int retryCount = 0;

        while (true)
        {
            try
            {
                return await requestFunc();
            }
            catch (HttpRequestException ex) when (retryCount < maxRetries)
            {
                retryCount++;
                var delayMs = (int)Math.Pow(2, retryCount) * 1000; // Exponential backoff: 2s, 4s, 8s

                _logger.LogWarning("Operation '{OperationName}' failed (attempt {Attempt}/{MaxRetries}): {Exception}. " +
                    "Retrying in {DelayMs}ms...",
                    operationName, retryCount, maxRetries, ex.Message, delayMs);

                await Task.Delay(delayMs, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError("Operation '{OperationName}' failed: {Exception}", operationName, ex);
                throw;
            }
        }
    }

    private void ConfigureHttpClient()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "DatabricksService/.NET6.0");
        // Headers will be set per-request for authentication
    }

    /// <summary>
    /// Sets the authorization header for each request (called before each request)
    /// </summary>
    private async Task<string> GetAuthorizationHeaderAsync(CancellationToken cancellationToken)
    {
        return await _authProvider.GetAuthorizationHeaderAsync(cancellationToken);
    }
}
