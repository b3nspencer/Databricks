# Databricks C# Service - Complete Project Index

## 📋 Documentation

| Document | Purpose |
|----------|---------|
| [GETTING_STARTED.md](GETTING_STARTED.md) | **Start here** - 5-minute setup guide |
| [README.md](README.md) | Complete feature documentation and API reference |
| [QUICKSTART.md](QUICKSTART.md) | Common patterns and code examples |
| [K8S_DEPLOYMENT.md](K8S_DEPLOYMENT.md) | Kubernetes deployment and Azure setup |

## 🏗️ Core Service Classes

| Class | File | Purpose |
|-------|------|---------|
| `DatabricksConfig` | [DatabricksConfig.cs](DatabricksConfig.cs) | Configuration settings |
| `IDatabricksAuthenticationProvider` | [DatabricksAuthenticationProvider.cs](DatabricksAuthenticationProvider.cs) | Authentication (Managed Identity, Key Vault, PAT) |
| `IDatabricksDataAccessService` | [DatabricksDataAccessService.cs](DatabricksDataAccessService.cs) | Main service for SQL execution |
| `IQueryResultCache` | [Caching/QueryResultCache.cs](Caching/QueryResultCache.cs) | In-memory caching with TTL |
| `DatabricksHealthCheck` | [DatabricksHealthCheck.cs](DatabricksHealthCheck.cs) | Kubernetes health check |
| `ServiceCollectionExtensions` | [ServiceCollectionExtensions.cs](ServiceCollectionExtensions.cs) | Dependency injection setup |

## 📦 Data Models

| Class | File | Purpose |
|-------|------|---------|
| `StatementExecutionRequest` | [Models/StatementExecutionRequest.cs](Models/StatementExecutionRequest.cs) | SQL execution request |
| `StatementParameter` | [Models/StatementExecutionRequest.cs](Models/StatementExecutionRequest.cs) | Query parameter |
| `StatementExecutionResponse` | [Models/StatementExecutionResponse.cs](Models/StatementExecutionResponse.cs) | API response |
| `StatementResult` | [Models/StatementExecutionResponse.cs](Models/StatementExecutionResponse.cs) | Query results |
| `ResultColumn` | [Models/StatementExecutionResponse.cs](Models/StatementExecutionResponse.cs) | Column metadata |

## 📝 Examples & Configuration

| File | Purpose |
|------|---------|
| [Examples/SampleUsage.cs](Examples/SampleUsage.cs) | 5 complete code examples |
| [Program.cs](Program.cs) | Application setup template |

## 🐳 Deployment

| File | Purpose |
|------|---------|
| [Dockerfile](Dockerfile) | Container image (multi-stage build) |
| [k8s/deployment.yaml](k8s/deployment.yaml) | Kubernetes deployment manifest |
| [k8s/configmap.yaml](k8s/configmap.yaml) | Configuration values |
| [k8s/service.yaml](k8s/service.yaml) | Kubernetes service & HPA |
| [k8s/serviceaccount.yaml](k8s/serviceaccount.yaml) | Service account & pod identity |

## 🚀 Quick Reference

### Authentication Priority
1. **Managed Identity** (Kubernetes) - Recommended
2. **Key Vault** (Secure PAT storage)
3. **Personal Access Token** (Development only)

### Key Methods
```csharp
// Execute query
await dataService.ExecuteQueryAsync<T>(query, parameters);

// Stream large results
await dataService.ExecuteQueryAsyncStream<T>(query, processRow);

// Get raw response
await dataService.ExecuteRawQueryAsync(query);

// Caching
var cached = await cache.GetAsync<T>(key);
await cache.SetAsync(key, value, ttl);
```

### Environment Variables
```bash
DATABRICKS_WORKSPACE_URL          # Required: https://adb-xxx.azuredatabricks.net
DATABRICKS_WAREHOUSE_ID           # Required: warehouse ID
DATABRICKS_USE_MANAGED_IDENTITY   # Optional: true (default) or false
DATABRICKS_KEYVAULT_URL           # Optional: Key Vault URL
DATABRICKS_TOKEN_SECRET_NAME      # Optional: Secret name (default: databricks-pat)
DATABRICKS_PERSONAL_ACCESS_TOKEN  # Optional: Direct PAT token
DATABRICKS_TIMEOUT_SECONDS        # Optional: Query timeout (default: 600)
DATABRICKS_WAIT_TIMEOUT_SECONDS   # Optional: Poll interval (default: 10)
```

## 📚 Learning Path

1. **5 minutes**: Read [GETTING_STARTED.md](GETTING_STARTED.md)
2. **10 minutes**: Try [QUICKSTART.md](QUICKSTART.md) examples
3. **30 minutes**: Review [Examples/SampleUsage.cs](Examples/SampleUsage.cs)
4. **1 hour**: Read [README.md](README.md) for full API
5. **2 hours**: Follow [K8S_DEPLOYMENT.md](K8S_DEPLOYMENT.md) for production deployment

## ✅ Build & Test

```bash
# Build
dotnet build

# Build for production
dotnet build -c Release

# Publish
dotnet publish -c Release -o ./publish

# Build Docker image
docker build -t databricks-service:latest .
```

## 📊 Architecture

```
User Code
    ↓
IDatabricksDataAccessService
    ↓
DatabricksAuthenticationProvider
    ↓
Azure Databricks SQL API
```

## 🔐 Security Features

- ✅ Managed Identity support
- ✅ Azure Key Vault integration
- ✅ Parameterized SQL queries
- ✅ Credential rotation support
- ✅ No hardcoded secrets
- ✅ Kubernetes RBAC ready
- ✅ TLS for all API calls
- ✅ Audit logging support

## 🎯 Use Cases

- ✅ Simple SQL queries
- ✅ Parameterized queries
- ✅ Large dataset streaming
- ✅ Result caching
- ✅ Batch processing
- ✅ Microservices integration
- ✅ Kubernetes deployments

## 📞 Support

- **Getting Help**: See [README.md](README.md) troubleshooting section
- **Examples**: Check [Examples/SampleUsage.cs](Examples/SampleUsage.cs)
- **Docs**: https://docs.databricks.com/api/workspace/introduction
- **Azure**: https://learn.microsoft.com/en-us/azure/databricks/

## 🎓 Next Steps

1. ➡️ **Start Here**: [GETTING_STARTED.md](GETTING_STARTED.md)
2. ➡️ **Learn By Example**: [QUICKSTART.md](QUICKSTART.md)
3. ➡️ **Go Deep**: [README.md](README.md)
4. ➡️ **Deploy**: [K8S_DEPLOYMENT.md](K8S_DEPLOYMENT.md)

---

**Version**: 1.0.0  
**Framework**: .NET 6.0  
**API**: Databricks SQL Statement Execution API 2.0  
**License**: MIT
