using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using System.Text;

namespace DatabricksService;

/// <summary>
/// Handles authentication for Databricks connections with multiple strategies
/// Priority: Managed Identity → Service Principal → Key Vault PAT → Provided PAT
/// </summary>
public interface IDatabricksAuthenticationProvider
{
    /// <summary>
    /// Gets a valid authentication token for Databricks API calls
    /// </summary>
    Task<string> GetAuthTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full HTTP Authorization header value (e.g., "Bearer token" or "Basic encodedtoken")
    /// </summary>
    Task<string> GetAuthorizationHeaderAsync(CancellationToken cancellationToken = default);
}

public class DatabricksAuthenticationProvider : IDatabricksAuthenticationProvider
{
    private readonly DatabricksConfig _config;
    private readonly ILogger<DatabricksAuthenticationProvider> _logger;
    private string? _cachedToken;
    private DateTime _tokenExpiryTime = DateTime.MinValue;

    public DatabricksAuthenticationProvider(
        DatabricksConfig config,
        ILogger<DatabricksAuthenticationProvider> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ValidateConfiguration();
    }

    public async Task<string> GetAuthTokenAsync(CancellationToken cancellationToken = default)
    {
        // Return cached token if still valid
        if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiryTime)
        {
            return _cachedToken;
        }

        string token = await AcquireTokenAsync(cancellationToken);

        // Cache token for most of its lifetime (Azure tokens are typically 1 hour)
        _cachedToken = token;
        _tokenExpiryTime = DateTime.UtcNow.AddSeconds(3000); // ~50 minutes for 1 hour tokens

        return token;
    }

    public async Task<string> GetAuthorizationHeaderAsync(CancellationToken cancellationToken = default)
    {
        string token = await GetAuthTokenAsync(cancellationToken);
        // Databricks accepts "Bearer" token format for OAuth tokens
        return $"Bearer {token}";
    }

    private async Task<string> AcquireTokenAsync(CancellationToken cancellationToken)
    {
        // Strategy 1: Try Managed Identity (best for Azure deployments)
        if (_config.UseManagedIdentity)
        {
            try
            {
                _logger.LogInformation("Attempting to acquire token using Managed Identity");
                var credential = new DefaultAzureCredential();
                var token = await credential.GetTokenAsync(
                    new TokenRequestContext(new[] { "2ff814a6-3304-4ab8-85cb-cd0e6f879c1d/.default" }),
                    cancellationToken
                );
                _logger.LogInformation("Successfully acquired token using Managed Identity");
                return token.Token;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Managed Identity authentication failed: {ex.Message}. Trying alternative methods...");
            }
        }

        // Strategy 2: Try Key Vault (if configured)
        if (!string.IsNullOrEmpty(_config.KeyVaultUrl) && !string.IsNullOrEmpty(_config.TokenSecretName))
        {
            try
            {
                _logger.LogInformation("Attempting to retrieve token from Key Vault");
                var kvUri = new Uri(_config.KeyVaultUrl);
                var credential = new DefaultAzureCredential();
                var client = new SecretClient(kvUri, credential);
                var secret = await client.GetSecretAsync(_config.TokenSecretName, cancellationToken: cancellationToken);
                _logger.LogInformation("Successfully retrieved token from Key Vault");
                return secret.Value.Value;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Key Vault token retrieval failed: {ex.Message}. Trying alternative methods...");
            }
        }

        // Strategy 3: Use provided Personal Access Token
        if (!string.IsNullOrEmpty(_config.PersonalAccessToken))
        {
            _logger.LogInformation("Using configured Personal Access Token");
            return _config.PersonalAccessToken;
        }

        throw new InvalidOperationException(
            "No valid authentication method available. Configure either: " +
            "1) Managed Identity (UseManagedIdentity=true), " +
            "2) Key Vault (KeyVaultUrl + TokenSecretName), or " +
            "3) Personal Access Token (PersonalAccessToken)");
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_config.WorkspaceUrl))
        {
            throw new InvalidOperationException("DatabricksConfig.WorkspaceUrl is required");
        }

        if (string.IsNullOrWhiteSpace(_config.WarehouseId))
        {
            throw new ArgumentException("DatabricksConfig.WarehouseId is required");
        }

        // Ensure workspace URL is properly formatted
        if (!_config.WorkspaceUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("WorkspaceUrl must start with https://");
        }

        if (_config.WorkspaceUrl.EndsWith("/"))
        {
            _config.WorkspaceUrl = _config.WorkspaceUrl.TrimEnd('/');
        }
    }
}
