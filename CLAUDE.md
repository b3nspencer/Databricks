# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**DatabricksService** is a production-grade .NET 6.0 library/service for accessing Azure Databricks via the SQL Statement Execution API 2.0. It's designed for both library consumption (NuGet) and standalone Kubernetes deployment with built-in health checks, caching, streaming, and multiple authentication strategies.

**Key characteristics:** Cloud-native, async-first, security-hardened, enterprise-ready.

## Technology Stack

- **.NET 6.0** (C# 10, nullable reference types enabled, implicit usings)
- **Azure Cloud:** Managed Identities, Key Vault, Databricks SQL API 2.0
- **Dependency Injection:** Microsoft.Extensions (DI, Logging, Health Checks, HTTP Client Factory)
- **Authentication:** Azure.Identity, Azure.Security.KeyVault
- **Deployment:** Docker multi-stage build, Kubernetes (YAML manifests), AKS workload identity

## Common Commands

### Build & Compilation

```bash
# Debug build
dotnet build

# Release build (used in Docker)
dotnet build -c Release

# Clean build
dotnet clean
dotnet build
```

### Debugging & Execution

```bash
# Run the application (executes Program.cs)
dotnet run

# Run with specific environment variables
DATABRICKS_WORKSPACE_URL=https://adb-xxx.azuredatabricks.net \
DATABRICKS_WAREHOUSE_ID=your-id \
dotnet run

# Debug in Visual Studio / VSCode
# Set breakpoints and use VS debugging tools
```

### Docker & Kubernetes

```bash
# Build Docker image
docker build -t databricks-service:latest .

# Test Docker image locally
docker run -e DATABRICKS_WORKSPACE_URL=https://adb-xxx.azuredatabricks.net \
           -e DATABRICKS_WAREHOUSE_ID=your-id \
           databricks-service:latest

# Deploy to Kubernetes
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/serviceaccount.yaml
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml

# Verify deployment
kubectl get pods -l app=databricks-service
kubectl logs -f -l app=databricks-service
kubectl port-forward svc/databricks-service 8080:8080
curl http://localhost:8080/health/live
```

## Code Architecture

### High-Level Design

The service uses a **layered architecture** with clear separation of concerns:

```
Application Code
    ↓
IDatabricksDataAccessService (Main interface)
    ├─→ Handles query execution, polling, deserialization
    ├─→ Implements retry logic (exponential backoff)
    └─→ Uses IQueryResultCache (optional caching)
    ↓
IDatabricksAuthenticationProvider
    ├─→ Fallback authentication chain
    ├─→ 1. Managed Identity (DefaultAzureCredential)
    ├─→ 2. Key Vault (SecretClient)
    └─→ 3. Personal Access Token (direct)
    ↓
HttpClient (connection pooling)
    ↓
Azure Databricks SQL Statement API 2.0 (HTTPS)
```

### Key Classes & Their Responsibilities

| Class | Purpose | Key Methods |
|-------|---------|-------------|
| **DatabricksConfig** | Configuration POCO | Holds WorkspaceUrl, WarehouseId, auth settings, timeouts |
| **DatabricksDataAccessService** | Main data access layer | `ExecuteQueryAsync<T>()`, `ExecuteRawQueryAsync()`, `ExecuteQueryAsyncStream<T>()` |
| **DatabricksAuthenticationProvider** | Multi-strategy auth | `GetAuthTokenAsync()` (tries Managed Identity → Key Vault → PAT) |
| **QueryResultCache** | Thread-safe in-memory cache with TTL | `GetAsync<T>()`, `SetAsync<T>()`, `GetStatistics()` |
| **DatabricksHealthCheck** | K8s health probe | `CheckHealthAsync()` (executes SELECT 1 test query) |
| **ServiceCollectionExtensions** | Dependency Injection setup | `AddDatabricksServices()`, `AddDatabricksServicesFromEnvironment()` |

### Authentication Strategy (Priority Order)

1. **Managed Identity** (recommended for production, Kubernetes, Azure services)
   - Uses `DefaultAzureCredential` from Azure.Identity
   - No credentials needed; Azure handles token acquisition
   - Enabled by default (`DATABRICKS_USE_MANAGED_IDENTITY=true`)

2. **Azure Key Vault** (secure credential storage)
   - Retrieves PAT from Key Vault
   - Uses `SecretClient` from Azure.Security.KeyVault
   - Requires `DATABRICKS_KEYVAULT_URL` and `DATABRICKS_TOKEN_SECRET_NAME`

3. **Direct Personal Access Token** (fallback, less secure)
   - Uses `DATABRICKS_PERSONAL_ACCESS_TOKEN` environment variable
   - Only attempted if previous methods fail

Each method is wrapped in try-catch and falls through to the next on failure.

### Query Execution Flow

1. **Build Request:** `StatementExecutionRequest` created with SQL, warehouse ID, parameters
2. **Get Token:** Auth provider acquires token using fallback chain
3. **POST to Databricks:** Send query to `/api/2.0/sql/statements` with Bearer token
4. **Poll for Results:** Databricks API is async; service polls `/api/2.0/sql/statements/{id}` every N seconds
5. **Handle Completion:** When state is SUCCEEDED/FAILED, process results
6. **Deserialize:** Convert `List<List<object>>` API response to strongly-typed `List<T>` via JSON intermediary
7. **Return Results:** Consumer receives deserialized objects or raw response

**Polling interval:** Configurable via `DATABRICKS_WAIT_TIMEOUT_SECONDS` (default 10 seconds)
**Query timeout:** Configurable via `DATABRICKS_TIMEOUT_SECONDS` (default 600 seconds)

### Retry & Resilience

- **Exponential backoff:** 2s → 4s → 8s (3 retries on `HttpRequestException`)
- **Token caching:** Auth tokens cached internally with expiry (avoids repeated identity service calls)
- **Health checks:** Kubernetes liveness/readiness probes execute test queries (SELECT 1) every 10-30 seconds

## Directory Structure

```
DatabricksService/
├── DatabricksConfig.cs                    # Configuration POCO
├── DatabricksAuthenticationProvider.cs    # Multi-strategy auth
├── DatabricksDataAccessService.cs         # Main service (3 execution methods)
├── DatabricksHealthCheck.cs               # K8s health check
├── ServiceCollectionExtensions.cs         # DI registration
├── Program.cs                             # Service entry point
├── Dockerfile                             # Multi-stage Docker build
├── DatabricksService.csproj               # Project & dependencies
├── Models/
│   ├── StatementExecutionRequest.cs       # Databricks API request DTO
│   └── StatementExecutionResponse.cs      # Databricks API response DTO
├── Caching/
│   └── QueryResultCache.cs                # Thread-safe cache with TTL
├── Examples/
│   └── SampleUsage.cs                     # 5 complete working examples
└── k8s/
    ├── deployment.yaml                    # K8s workload
    ├── configmap.yaml                     # Configuration
    ├── service.yaml                       # K8s service
    └── serviceaccount.yaml                # RBAC & workload identity
```

## Configuration & Environment Variables

### Required Variables

```
DATABRICKS_WORKSPACE_URL=https://adb-xxx.azuredatabricks.net
DATABRICKS_WAREHOUSE_ID=your-warehouse-id
```

### Optional Variables (with defaults)

```
DATABRICKS_USE_MANAGED_IDENTITY=true        # Enable managed identity auth
DATABRICKS_TIMEOUT_SECONDS=600              # Query timeout (0 = infinite)
DATABRICKS_WAIT_TIMEOUT_SECONDS=10          # Polling interval between status checks
DATABRICKS_KEYVAULT_URL=                    # Key Vault URL (if using KV auth)
DATABRICKS_TOKEN_SECRET_NAME=databricks-pat # KV secret name for PAT
DATABRICKS_PERSONAL_ACCESS_TOKEN=           # Direct PAT (fallback)
```

### Configuration in Code

Two ways to configure:

```csharp
// Option 1: Manual configuration
services.AddDatabricksServices(config =>
{
    config.WorkspaceUrl = "https://adb-xxx.azuredatabricks.net";
    config.WarehouseId = "your-id";
    config.UseManagedIdentity = true;
    config.TimeoutSeconds = 600;
});

// Option 2: From environment variables (Program.cs uses this)
services.AddDatabricksServicesFromEnvironment();
```

## Important Patterns & Design Decisions

### Async-First Design
- All I/O operations (HTTP, Key Vault) are async with `ConfigureAwait(false)`
- Enables non-blocking concurrency for high throughput
- Support for `CancellationToken` throughout the API

### Polling Pattern (Not Callback-Based)
- Databricks SQL API is asynchronous; queries return immediately with a StatementId
- Service polls `/statements/{id}` at regular intervals until completion
- Polling is preferable to callbacks for simplicity and reliability in cloud environments

### Streaming for Large Results
- `ExecuteQueryAsyncStream<T>()` deserializes rows one-at-a-time via `async` enumeration
- Avoids loading entire result sets into memory for large datasets
- Alternative to `ExecuteQueryAsync<T>()` which returns `List<T>`

### Generic Result Deserialization
- Databricks API returns raw `List<List<object>>` with no column metadata
- Service uses JSON as intermediary (serializes to JSON, deserializes to `T`)
- Supports `[JsonPropertyName]` attributes and case-insensitive matching

### Dependency Injection Container
- All dependencies registered in `ServiceCollectionExtensions`
- DI container manages object lifetimes: Singletons (config, auth, cache), Scoped (logging)
- Enables testability and loosely-coupled design

### Security Hardening
- **No hardcoded secrets:** All config from environment variables
- **HTTPS only:** WorkspaceUrl validation requires https:// prefix
- **Managed Identity preferred:** Avoids credential storage in code
- **Key Vault integration:** Secure PAT storage option
- **Token caching with expiry:** Reduces identity service load (~50 min cache, 1 hour token)
- **Kubernetes security context:** Non-root user, read-only filesystem, dropped capabilities
- **Structured logging:** Templates sanitize sensitive values

### Thread Safety
- `QueryResultCache` uses lock-based synchronization
- Dictionary operations are thread-safe for concurrent reads
- No race conditions in auth token caching (exception-safe lazy initialization)

## Testing & Examples

### Example Code

`SampleUsage.cs` contains 5 complete working examples:

1. **Simple Query:** SELECT with deserialization to C# objects
2. **Parameterized Query:** With parameter binding
3. **Streaming:** Processing large results row-by-row
4. **Caching:** Using `IQueryResultCache` with TTL
5. **Raw Response:** Accessing raw Databricks API response

Run any example by modifying `Program.cs` to call methods from `SampleUsage`.

### Integration Testing

Tests require a real Databricks workspace and warehouse (no mocking setup currently). To test locally:

```bash
# Set environment variables
export DATABRICKS_WORKSPACE_URL=https://your-workspace.azuredatabricks.net
export DATABRICKS_WAREHOUSE_ID=your-warehouse-id
export DATABRICKS_USE_MANAGED_IDENTITY=false
export DATABRICKS_PERSONAL_ACCESS_TOKEN=your-pat

# Run the service
dotnet run
```

## Important Implementation Notes

### When Modifying Query Execution

- All changes to `ExecuteQueryAsync*` methods must preserve the async/await pattern
- Polling interval is configurable but defaults to `DATABRICKS_WAIT_TIMEOUT_SECONDS`
- State machine for query execution: PENDING → RUNNING → SUCCEEDED/FAILED
- Always handle `TimeoutException` when query execution exceeds `TimeoutSeconds`

### When Adding Authentication Methods

- New methods must implement the fallback pattern (try, catch, return null if fail)
- Must be added to `DatabricksAuthenticationProvider.GetAuthTokenAsync()`
- Always log attempts at Debug level and failures at Warning level

### When Extending Configuration

- Add properties to `DatabricksConfig` POCO
- Add environment variable reading to `ServiceCollectionExtensions.AddDatabricksServicesFromEnvironment()`
- Update `Program.cs` and documentation with new variables
- Validate inputs in configuration setters (e.g., HTTPS URL requirement)

### When Modifying Kubernetes Manifests

- Keep `deployment.yaml` with 2 replicas (horizontal scaling)
- Always specify resource requests/limits (250m CPU, 512Mi RAM minimum)
- Health checks: liveness every 10s, readiness every 30s
- Service account must reference correct managed identity client ID
- ConfigMap holds non-secret configuration; secrets stay in environment variables

### Logging Standards

- **Information:** Successful operations (auth, query completion)
- **Debug:** Internal flow (cache hits, polling cycles, token caching)
- **Warning:** Retriable failures (auth attempt failures, retries)
- **Error:** Terminal failures (query execution failed, API errors)
- Use structured logging templates: `logger.LogInformation("Message with {Parameter}", value)`

## Performance Considerations

### Connection Pooling
- HttpClient is instantiated once (singleton) → automatic connection pooling
- Connection reuse across requests significantly reduces latency

### Query Timeouts & Polling
- Default query timeout: 600 seconds (10 minutes)
- Default polling interval: 10 seconds
- Tune `DATABRICKS_WAIT_TIMEOUT_SECONDS` based on expected query duration

### Large Result Sets
- Databricks API returns results inline if < 25 MB, else as external SAS links
- Use `ExecuteQueryAsyncStream<T>()` for results > 100K rows
- In-memory cache is single-instance (not distributed across pods)

### Caching Strategy
- Cache is in-memory only (scoped to single pod instance)
- Use distributed cache (Redis) for multi-pod deployments
- TTL prevents stale data; fine-tune based on update frequency

## Security Checklist for Changes

When making changes, ensure:

- [ ] No hardcoded secrets, credentials, or API keys
- [ ] All external inputs validated (URLs, query parameters)
- [ ] HTTPS enforced for API calls
- [ ] Sensitive data not logged (use template parameters)
- [ ] Async/await properly used to prevent thread pool starvation
- [ ] Exception handling doesn't expose stack traces to clients
- [ ] Managed Identity preferred over static credentials

## References

- [Azure Databricks SQL Statement Execution API 2.0](https://docs.databricks.com/api/workspace/statementexecution)
- [Azure.Identity Documentation](https://learn.microsoft.com/dotnet/api/azure.identity)
- [Microsoft.Extensions.DependencyInjection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection)
- [Kubernetes Workload Identity](https://learn.microsoft.com/azure/aks/workload-identity-overview)
- [Example Usage](Examples/SampleUsage.cs)
- [README](README.md) - Complete feature and API documentation
- [Kubernetes Deployment Guide](K8S_DEPLOYMENT.md) - K8s setup instructions
