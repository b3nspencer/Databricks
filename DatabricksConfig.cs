namespace DatabricksService;

/// <summary>
/// Configuration settings for Databricks connection
/// </summary>
public class DatabricksConfig
{
    /// <summary>
    /// The base URL of the Databricks workspace (e.g., https://adb-xxx.azuredatabricks.net)
    /// </summary>
    public string WorkspaceUrl { get; set; } = string.Empty;

    /// <summary>
    /// The SQL warehouse ID to use for queries
    /// </summary>
    public string WarehouseId { get; set; } = string.Empty;

    /// <summary>
    /// Maximum timeout in seconds for query execution (default: 600 = 10 minutes)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 600;

    /// <summary>
    /// Wait timeout for polling query results in seconds (5-50 or 0 for immediate)
    /// </summary>
    public int WaitTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Maximum number of rows to return per query (0 = unlimited)
    /// </summary>
    public int MaxRowLimit { get; set; } = 0;

    /// <summary>
    /// Whether to use Azure Managed Identity for authentication
    /// </summary>
    public bool UseManagedIdentity { get; set; } = true;

    /// <summary>
    /// Azure Key Vault URL for storing sensitive credentials
    /// </summary>
    public string? KeyVaultUrl { get; set; }

    /// <summary>
    /// Personal access token (if not using managed identity)
    /// </summary>
    public string? PersonalAccessToken { get; set; }

    /// <summary>
    /// Name of the Key Vault secret containing the personal access token
    /// </summary>
    public string? TokenSecretName { get; set; } = "databricks-pat";
}
