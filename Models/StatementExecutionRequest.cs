using System.Text.Json.Serialization;

namespace DatabricksService.Models;

/// <summary>
/// Request model for Databricks Statement Execution API 2.0
/// </summary>
public class StatementExecutionRequest
{
    /// <summary>
    /// The SQL statement to execute
    /// </summary>
    [JsonPropertyName("statement")]
    public string Statement { get; set; } = string.Empty;

    /// <summary>
    /// The warehouse ID to execute the query against
    /// </summary>
    [JsonPropertyName("warehouse_id")]
    public string WarehouseId { get; set; } = string.Empty;

    /// <summary>
    /// Parameters to bind to the query (optional)
    /// </summary>
    [JsonPropertyName("parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<StatementParameter>? Parameters { get; set; }

    /// <summary>
    /// Maximum time in seconds to wait for query completion
    /// </summary>
    [JsonPropertyName("timeout_seconds")]
    public int TimeoutSeconds { get; set; } = 600;

    /// <summary>
    /// Maximum number of rows to return (0 = no limit)
    /// </summary>
    [JsonPropertyName("row_limit")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RowLimit { get; set; }

    /// <summary>
    /// Whether to return results inline or as external links for large datasets
    /// Options: INLINE, EXTERNAL_LINKS
    /// </summary>
    [JsonPropertyName("disposition")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Disposition { get; set; }
}

/// <summary>
/// Parameter for parameterized SQL queries
/// </summary>
public class StatementParameter
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "STRING";
}
