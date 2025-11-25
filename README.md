# Azure Databricks C# Service

A production-ready .NET 6.0 service for accessing Azure Databricks using the SQL Statement Execution API 2.0. Built with support for managed identities, advanced features like caching and streaming, and Kubernetes-native deployment.

## Features

- **Multiple Authentication Methods**
  - Azure Managed Identities (recommended for production)
  - Service Principal OAuth
  - Azure Key Vault secret retrieval
  - Personal Access Token fallback

- **Data Access**
  - SQL query execution with parameterization
  - Result deserialization to C# objects
  - Streaming results for large datasets
  - Raw response handling for complex queries

- **Advanced Features**
  - Built-in query result caching with TTL
  - Exponential backoff retry logic
  - Connection pooling and HTTP client optimization
  - Comprehensive error handling

- **Production Hardiness**
  - Health checks for Databricks connectivity
  - Structured logging throughout
  - Security context and RBAC in Kubernetes
  - Horizontal Pod Autoscaling support

## Architecture

```
┌─────────────────────────────────────────┐
│         Your Application                │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│  DatabricksDataAccessService            │
│  (SQL Execution API 2.0)                │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│  IDatabricksAuthenticationProvider      │
│  ┌─────────────────────────────────┐   │
│  │ 1. Managed Identity             │   │
│  │ 2. Service Principal            │   │
│  │ 3. Key Vault PAT                │   │
│  │ 4. Direct PAT                   │   │
│  └─────────────────────────────────┘   │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│  Azure Databricks API                   │
│  (https://workspace.azuredatabricks.net)│
└─────────────────────────────────────────┘
```

## Installation

### Prerequisites

- .NET 6.0 or later
- Azure subscription with Databricks workspace
- SQL warehouse in Databricks

### Setup

1. **Clone or download** the project
2. **Create Databricks workspace and warehouse** if you haven't already
3. **Configure authentication** (see Configuration section)

## Configuration

### Environment Variables

The service uses environment variables for configuration. Required variables:

```bash
# Required
DATABRICKS_WORKSPACE_URL=https://adb-xxx.azuredatabricks.net
DATABRICKS_WAREHOUSE_ID=your-warehouse-id

# Optional (defaults shown)
DATABRICKS_USE_MANAGED_IDENTITY=true
DATABRICKS_TIMEOUT_SECONDS=600
DATABRICKS_WAIT_TIMEOUT_SECONDS=10

# Key Vault configuration (if using Key Vault for PAT)
DATABRICKS_KEYVAULT_URL=https://your-keyvault.vault.azure.net/
DATABRICKS_TOKEN_SECRET_NAME=databricks-pat

# Direct PAT (not recommended for production)
DATABRICKS_PERSONAL_ACCESS_TOKEN=your-token
```

### Authentication Priority

1. **Managed Identity** (recommended for Kubernetes/Azure)
   - No credentials needed
   - Set `DATABRICKS_USE_MANAGED_IDENTITY=true`

2. **Key Vault** (secure credential storage)
   - Set `DATABRICKS_KEYVAULT_URL` and `DATABRICKS_TOKEN_SECRET_NAME`

3. **Personal Access Token** (fallback)
   - Set `DATABRICKS_PERSONAL_ACCESS_TOKEN`

## Usage

### Basic Query Execution

```csharp
using DatabricksService;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddDatabricksServices(config =>
{
    config.WorkspaceUrl = "https://adb-xxx.azuredatabricks.net";
    config.WarehouseId = "your-warehouse-id";
});

var provider = services.BuildServiceProvider();
var dataService = provider.GetRequiredService<IDatabricksDataAccessService>();

// Execute simple query
var users = await dataService.ExecuteQueryAsync<User>(
    "SELECT id, name, email FROM users WHERE id > 100"
);
```

### Parameterized Queries

```csharp
var parameters = new Dictionary<string, string>
{
    { "min_price", "100" },
    { "max_price", "1000" }
};

var products = await dataService.ExecuteQueryAsync<Product>(
    "SELECT * FROM products WHERE price BETWEEN @min_price AND @max_price",
    parameters
);
```

### Streaming Large Results

```csharp
// Process rows one-by-one without loading all into memory
await dataService.ExecuteQueryAsyncStream<Product>(
    "SELECT * FROM products",
    async (product) =>
    {
        // Process each row
        await ProcessProduct(product);
    }
);
```

### Query Result Caching

```csharp
var cache = provider.GetRequiredService<IQueryResultCache>();

const string cacheKey = "top-products";
var products = await cache.GetAsync<List<Product>>(cacheKey);

if (products == null)
{
    products = await dataService.ExecuteQueryAsync<Product>(
        "SELECT * FROM products ORDER BY sales DESC LIMIT 100"
    );
    await cache.SetAsync(cacheKey, products, TimeSpan.FromMinutes(5));
}
```

### Raw Response Handling

```csharp
var response = await dataService.ExecuteRawQueryAsync(
    "SELECT COUNT(*) as total FROM users"
);

Console.WriteLine($"State: {response.State}");
Console.WriteLine($"Rows: {response.Result?.RowCount}");
if (response.Result?.Statistics != null)
{
    Console.WriteLine($"Execution time: {response.Result.Statistics.ExecutionDurationMs}ms");
}
```

## Kubernetes Deployment

### Prerequisites

- AKS cluster with workload identity enabled
- Azure user-assigned managed identity
- Pod identity webhook or Workload Identity Federation

### Deployment Steps

1. **Create managed identity**
```bash
az identity create --resource-group myRG --name databricks-service
```

2. **Grant Databricks API permissions** to the managed identity
```bash
# Authenticate managed identity with Databricks service principal
```

3. **Deploy ConfigMap**
```bash
kubectl apply -f k8s/configmap.yaml
# Edit with your Databricks workspace details
```

4. **Deploy ServiceAccount**
```bash
kubectl apply -f k8s/serviceaccount.yaml
# Update with your managed identity client ID
```

5. **Build and push Docker image**
```bash
docker build -t myregistry.azurecr.io/databricks-service:latest .
docker push myregistry.azurecr.io/databricks-service:latest
```

6. **Deploy application**
```bash
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
```

### Verification

```bash
# Check pod status
kubectl get pods -l app=databricks-service

# View logs
kubectl logs -f -l app=databricks-service

# Test health
kubectl port-forward svc/databricks-service 8080:8080
curl http://localhost:8080/health
```

## Health Checks

The service implements Kubernetes health checks:

- **Liveness probe** (`/health/live`): Pod restart if unhealthy
- **Readiness probe** (`/health/ready`): Remove from load balancer if unhealthy

Both execute a test query against Databricks every 10-30 seconds.

## Performance Considerations

### Connection Pooling
- HttpClient is reused across requests
- Connection pooling is automatic

### Query Timeouts
- Default: 600 seconds
- Configurable via `DATABRICKS_TIMEOUT_SECONDS`

### Large Result Sets
- **< 25 MB**: Returned inline in JSON
- **> 25 MB**: Returned as external SAS links
- **Streaming**: Use `ExecuteQueryAsyncStream` to avoid memory issues

### Caching
- In-memory only (single instance)
- Configurable TTL per entry
- Automatic expiration cleanup

## Security Best Practices

1. **Use Managed Identities** in Kubernetes instead of PATs
2. **Store PATs in Key Vault** if required
3. **Limit query permissions** in Databricks with service principals
4. **Enable audit logging** in Azure and Databricks
5. **Use network policies** to restrict pod-to-pod communication
6. **Enable TLS** for all API calls (automatic with HTTPS)
7. **Rotate credentials** regularly

## Troubleshooting

### Authentication Failures

```
Error: No valid authentication method available
```

**Solutions:**
- Verify managed identity is assigned to the pod
- Check Key Vault permissions if using Key Vault
- Verify PAT token if using direct authentication

### Query Timeouts

```
Error: Request timeout after 600 seconds
```

**Solutions:**
- Increase `DATABRICKS_TIMEOUT_SECONDS`
- Optimize the query
- Check Databricks cluster/warehouse status

### Connection Refused

```
Error: Connection refused to workspace URL
```

**Solutions:**
- Verify `DATABRICKS_WORKSPACE_URL` format
- Check network connectivity to Databricks
- Verify workspace exists and is accessible

## API Reference

### IDatabricksDataAccessService

```csharp
// Execute query returning deserialized objects
Task<List<T>> ExecuteQueryAsync<T>(
    string query,
    Dictionary<string, string>? parameters = null,
    CancellationToken cancellationToken = default) where T : class;

// Execute query returning raw API response
Task<StatementExecutionResponse> ExecuteRawQueryAsync(
    string query,
    Dictionary<string, string>? parameters = null,
    CancellationToken cancellationToken = default);

// Stream results without loading all into memory
Task ExecuteQueryAsyncStream<T>(
    string query,
    Func<T, Task> processRowAsync,
    Dictionary<string, string>? parameters = null,
    CancellationToken cancellationToken = default) where T : class;
```

### IQueryResultCache

```csharp
Task<T?> GetAsync<T>(string cacheKey) where T : class;
Task SetAsync<T>(string cacheKey, T value, TimeSpan ttl) where T : class;
Task RemoveAsync(string cacheKey);
Task ClearAsync();
CacheStatistics GetStatistics();
```

## Monitoring

### Logs

The service uses structured logging. Examples:

```
Information: Executing query: SELECT * FROM users
Debug: Cache hit for key 'user-list'
Debug: Statement {id} is in state RUNNING, polling for result...
Information: Query executed successfully. Returned 1000 rows
```

### Metrics

Cache statistics available via:

```csharp
var cache = provider.GetRequiredService<IQueryResultCache>();
var stats = cache.GetStatistics();
Console.WriteLine(stats); // Entries: 5, Hits: 42, Misses: 8, HitRate: 84%
```

## Contributing

### Building

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Code Style

- Follow C# conventions
- Use nullable reference types
- Add XML documentation comments
- Keep methods focused and testable

## License

MIT License - see LICENSE file for details

## Support

- Documentation: See this README
- Issues: Report via GitHub Issues
- Examples: Check `Examples/SampleUsage.cs`

## Version History

### v1.0.0 (Current)
- Initial release
- Statement Execution API 2.0 support
- Managed identity authentication
- Query caching and streaming
- Kubernetes deployment support
