# Getting Started with Databricks C# Service

This guide helps you get the Databricks C# Service up and running quickly.

## What You've Got

A complete, production-ready .NET 6.0 service for accessing Azure Databricks with:

- ✅ SQL query execution via REST API
- ✅ Multiple authentication methods (Managed Identity, Key Vault, PAT)
- ✅ Query result caching
- ✅ Streaming for large result sets
- ✅ Kubernetes deployment ready
- ✅ Health checks included
- ✅ Full documentation and examples

## Project Structure

```
DatabricksService/
├── DatabricksConfig.cs                 # Configuration class
├── DatabricksAuthenticationProvider.cs # Authentication logic
├── DatabricksDataAccessService.cs      # Main data access service
├── DatabricksHealthCheck.cs            # Kubernetes health check
├── ServiceCollectionExtensions.cs      # DI setup
├── Models/
│   ├── StatementExecutionRequest.cs   # API request models
│   └── StatementExecutionResponse.cs  # API response models
├── Caching/
│   └── QueryResultCache.cs            # Result caching service
├── Examples/
│   └── SampleUsage.cs                 # 5 complete examples
├── k8s/                                # Kubernetes manifests
│   ├── deployment.yaml
│   ├── configmap.yaml
│   ├── service.yaml
│   └── serviceaccount.yaml
├── Dockerfile                          # Container image
├── Program.cs                          # Application entry point
├── README.md                           # Full documentation
├── QUICKSTART.md                       # Quick start guide
├── K8S_DEPLOYMENT.md                   # Kubernetes guide
└── DatabricksService.csproj           # Project file
```

## Quick Start (5 Minutes)

### 1. Set Environment Variables

```bash
export DATABRICKS_WORKSPACE_URL="https://adb-12345678.azuredatabricks.net"
export DATABRICKS_WAREHOUSE_ID="abc123def456"
export DATABRICKS_USE_MANAGED_IDENTITY="false"
export DATABRICKS_PERSONAL_ACCESS_TOKEN="dapi1234567890abcdef"
```

### 2. Build

```bash
cd DatabricksService
dotnet build
```

### 3. Create Your Data Model

```csharp
public class Customer
{
    public int customer_id { get; set; }
    public string customer_name { get; set; }
    public string email { get; set; }
}
```

### 4. Use in Your Code

```csharp
var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole());
services.AddDatabricksServicesFromEnvironment();

var provider = services.BuildServiceProvider();
var dataService = provider.GetRequiredService<IDatabricksDataAccessService>();

var customers = await dataService.ExecuteQueryAsync<Customer>(
    "SELECT customer_id, customer_name, email FROM customers LIMIT 10"
);

foreach (var c in customers)
{
    Console.WriteLine($"{c.customer_name} ({c.email})");
}
```

## Key Files to Review

| File | Purpose |
|------|---------|
| [README.md](README.md) | Complete documentation |
| [QUICKSTART.md](QUICKSTART.md) | Quick start with common patterns |
| [K8S_DEPLOYMENT.md](K8S_DEPLOYMENT.md) | Kubernetes deployment guide |
| [Examples/SampleUsage.cs](Examples/SampleUsage.cs) | 5 complete code examples |
| [Program.cs](Program.cs) | Application setup example |

## Important Classes

### IDatabricksDataAccessService
Main service for querying Databricks:
- `ExecuteQueryAsync<T>()` - Execute query and get results
- `ExecuteRawQueryAsync()` - Get raw API response
- `ExecuteQueryAsyncStream<T>()` - Stream results for large datasets

### IDatabricksAuthenticationProvider
Handles authentication automatically:
- Managed Identity (recommended)
- Service Principal
- Key Vault
- Personal Access Token

### IQueryResultCache
In-memory result caching:
- `GetAsync<T>()` - Get cached result
- `SetAsync<T>()` - Cache result with TTL
- `GetStatistics()` - View cache performance

## Authentication Methods (Priority Order)

### 1. Managed Identity (Recommended for Kubernetes)
```csharp
config.UseManagedIdentity = true;
// No additional setup needed in pod with proper binding
```

### 2. Key Vault (Secure Credential Storage)
```csharp
config.UseManagedIdentity = true;
config.KeyVaultUrl = "https://my-keyvault.vault.azure.net/";
config.TokenSecretName = "databricks-pat";
```

### 3. Personal Access Token (Development)
```csharp
config.UseManagedIdentity = false;
config.PersonalAccessToken = "dapi1234567890abcdef";
```

## Common Tasks

### Execute a Simple Query
See [QUICKSTART.md](QUICKSTART.md) - Example 1

### Use Parameterized Queries
See [QUICKSTART.md](QUICKSTART.md) - Parameterized Queries section

### Stream Large Results
See [QUICKSTART.md](QUICKSTART.md) - Streaming Large Results section

### Cache Results
See [QUICKSTART.md](QUICKSTART.md) - Caching Query Results section

### Deploy to Kubernetes
See [K8S_DEPLOYMENT.md](K8S_DEPLOYMENT.md)

## Troubleshooting

### "No valid authentication method available"
- Verify environment variables are set
- Check managed identity is properly bound in Kubernetes
- Verify PAT token is valid

### "Query timeout"
- Check SQL warehouse is running
- Increase `DATABRICKS_TIMEOUT_SECONDS`
- Optimize the query for Databricks

### "Column name does not match"
- Verify table schema: `DESC table_name`
- Use lowercase property names (Databricks returns lowercase)
- Or use `[JsonPropertyName("column_name")]` attribute

### Build Fails
- Ensure .NET 6.0 or later: `dotnet --version`
- Restore packages: `dotnet restore`
- Clean and rebuild: `dotnet clean && dotnet build`

## Dependencies

**NuGet Packages:**
- Microsoft.Azure.Databricks.Client - Databricks SDK
- Azure.Identity - Azure authentication
- Azure.Security.KeyVault.Secrets - Key Vault access
- Microsoft.Extensions.* - Dependency injection and logging
- Microsoft.Extensions.Diagnostics.HealthChecks - Health checks
- Microsoft.Extensions.Http - HTTP client factory

All dependencies are automatically installed with `dotnet restore`.

## Next Steps

1. **Review Examples**: Open [Examples/SampleUsage.cs](Examples/SampleUsage.cs)
2. **Check Documentation**: Read [README.md](README.md)
3. **Kubernetes Deploy**: Follow [K8S_DEPLOYMENT.md](K8S_DEPLOYMENT.md)
4. **Customize**: Modify for your specific needs

## Architecture Overview

```
┌─────────────────────────────┐
│   Your Application          │
└──────────────┬──────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│  ServiceCollection (Dependency Injection) │
│  - ILogger                              │
│  - IDatabricksDataAccessService         │
│  - IDatabricksAuthenticationProvider    │
│  - IQueryResultCache                   │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│  DatabricksDataAccessService            │
│  - Executes SQL queries                 │
│  - Handles polling                      │
│  - Deserializes results                 │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│  DatabricksAuthenticationProvider       │
│  - Gets authentication token            │
│  - Tries Managed Identity → Key Vault   │
│    → PAT in order                       │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│  Azure Databricks SQL API                │
│  https://workspace.azuredatabricks.net   │
└─────────────────────────────────────────┘
```

## Performance Tips

1. **Reuse Service Instance**: Don't recreate the service repeatedly
2. **Use Caching**: Cache frequently accessed data
3. **Stream Large Results**: Use `ExecuteQueryAsyncStream<T>()` for big datasets
4. **Parameterize Queries**: Prevent SQL injection, improve performance
5. **Connection Pooling**: HttpClient handles this automatically

## Security Best Practices

1. ✅ Use Managed Identity in Kubernetes
2. ✅ Store secrets in Key Vault
3. ✅ Never commit PAT tokens to source control
4. ✅ Use environment variables for configuration
5. ✅ Enable audit logging
6. ✅ Limit query permissions with service principals

## Support & Resources

- **Main Docs**: [README.md](README.md)
- **Quick Ref**: [QUICKSTART.md](QUICKSTART.md)
- **K8s Deployment**: [K8S_DEPLOYMENT.md](K8S_DEPLOYMENT.md)
- **Examples**: [Examples/SampleUsage.cs](Examples/SampleUsage.cs)
- **Databricks API**: https://docs.databricks.com/api/
- **Azure Docs**: https://learn.microsoft.com/en-us/azure/databricks/

---

**Ready to get started?** Check out [QUICKSTART.md](QUICKSTART.md) for a working example in minutes!
