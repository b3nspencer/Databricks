using System.Text.Json.Serialization;

namespace DatabricksService.Models;

/// <summary>
/// Response from Databricks Statement Execution API 2.0
/// </summary>
public class StatementExecutionResponse
{
    /// <summary>
    /// Unique statement ID
    /// </summary>
    [JsonPropertyName("statement_id")]
    public string? StatementId { get; set; }

    /// <summary>
    /// Current state of the statement (PENDING, RUNNING, SUCCEEDED, FAILED, CANCELED)
    /// </summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }

    /// <summary>
    /// Result data if query is complete
    /// </summary>
    [JsonPropertyName("result")]
    public StatementResult? Result { get; set; }

    /// <summary>
    /// Error information if query failed
    /// </summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error code if available
    /// </summary>
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; }
}

/// <summary>
/// Result data from a completed statement
/// </summary>
public class StatementResult
{
    /// <summary>
    /// Column metadata
    /// </summary>
    [JsonPropertyName("result_columns")]
    public List<ResultColumn>? ResultColumns { get; set; }

    /// <summary>
    /// Actual data rows
    /// </summary>
    [JsonPropertyName("data_array")]
    public List<List<object?>>? DataArray { get; set; }

    /// <summary>
    /// Total number of rows
    /// </summary>
    [JsonPropertyName("row_count")]
    public long? RowCount { get; set; }

    /// <summary>
    /// For large results, link to external storage (SAS URL)
    /// </summary>
    [JsonPropertyName("external_links")]
    public List<ExternalLink>? ExternalLinks { get; set; }

    /// <summary>
    /// Execution statistics
    /// </summary>
    [JsonPropertyName("statementStats")]
    public StatementStatistics? Statistics { get; set; }
}

/// <summary>
/// Column metadata
/// </summary>
public class ResultColumn
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type_text")]
    public string? TypeText { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }
}

/// <summary>
/// External link for large result sets
/// </summary>
public class ExternalLink
{
    [JsonPropertyName("fileLink")]
    public string? FileLink { get; set; }

    [JsonPropertyName("expirationTime")]
    public string? ExpirationTime { get; set; }

    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

    [JsonPropertyName("fileSize")]
    public long? FileSize { get; set; }
}

/// <summary>
/// Execution statistics for a statement
/// </summary>
public class StatementStatistics
{
    [JsonPropertyName("executionDurationMs")]
    public long? ExecutionDurationMs { get; set; }

    [JsonPropertyName("rowsRead")]
    public long? RowsRead { get; set; }

    [JsonPropertyName("rowsWritten")]
    public long? RowsWritten { get; set; }

    [JsonPropertyName("bytesRead")]
    public long? BytesRead { get; set; }

    [JsonPropertyName("bytesWritten")]
    public long? BytesWritten { get; set; }
}
