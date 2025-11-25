# Quick Start Guide

Get up and running with the Databricks C# Service in 5 minutes.

## Step 1: Set Environment Variables

```bash
# Required
export DATABRICKS_WORKSPACE_URL="https://adb-12345678.90.azuredatabricks.net"
export DATABRICKS_WAREHOUSE_ID="abc123def456"

# For local development with PAT
export DATABRICKS_USE_MANAGED_IDENTITY="false"
export DATABRICKS_PERSONAL_ACCESS_TOKEN="dapi1234567890abcdef"
```

## Step 2: Create a Data Model

```csharp
// Your domain model
public class Customer
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; }
    public string Email { get; set; }
    public DateTime CreatedDate { get; set; }
}
```

## Step 3: Configure DI Container

```csharp
using DatabricksService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());
services.AddDatabricksServicesFromEnvironment();

var provider = services.BuildServiceProvider();
```

## Step 4: Execute Your First Query

```csharp
var dataService = provider.GetRequiredService<IDatabricksDataAccessService>();

var customers = await dataService.ExecuteQueryAsync<Customer>(
    "SELECT customer_id, customer_name, email, created_date FROM customers LIMIT 10"
);

foreach (var customer in customers)
{
    Console.WriteLine($"{customer.CustomerId}: {customer.CustomerName}");
}
```

## Step 5: Add Parameters

```csharp
var parameters = new Dictionary<string, string>
{
    { "min_date", "2024-01-01" },
    { "email_domain", "%@example.com" }
};

var filtered = await dataService.ExecuteQueryAsync<Customer>(
    "SELECT * FROM customers WHERE created_date >= @min_date AND email LIKE @email_domain",
    parameters
);
```

## Complete Example

```csharp
using DatabricksService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

class Program
{
    public class Customer
    {
        public int customer_id { get; set; }
        public string customer_name { get; set; }
        public string email { get; set; }
    }

    static async Task Main()
    {
        // Setup
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddDatabricksServicesFromEnvironment();
        var provider = services.BuildServiceProvider();

        // Execute query
        var dataService = provider.GetRequiredService<IDatabricksDataAccessService>();
        var customers = await dataService.ExecuteQueryAsync<Customer>(
            "SELECT customer_id, customer_name, email FROM customers LIMIT 5"
        );

        // Display results
        Console.WriteLine($"Found {customers.Count} customers:");
        foreach (var c in customers)
        {
            Console.WriteLine($"  - {c.customer_name} ({c.email})");
        }
    }
}
```

## Column Name Mapping

By default, JSON deserialization uses case-insensitive matching. Databricks typically returns lowercase column names:

**Option 1: Lowercase properties** (recommended)
```csharp
public class User
{
    public int user_id { get; set; }
    public string user_name { get; set; }
}
```

**Option 2: Use JsonPropertyName attribute**
```csharp
using System.Text.Json.Serialization;

public class User
{
    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("user_name")]
    public string UserName { get; set; }
}
```

## Streaming Large Results

For queries that return millions of rows:

```csharp
long processedCount = 0;

await dataService.ExecuteQueryAsyncStream<Customer>(
    "SELECT * FROM large_customers_table",
    async (customer) =>
    {
        // Process one row at a time
        await ProcessCustomer(customer);
        processedCount++;

        if (processedCount % 10000 == 0)
            Console.WriteLine($"Processed {processedCount}...");
    }
);

async Task ProcessCustomer(Customer c)
{
    // Example: write to database, file, etc.
    await Task.Delay(10); // simulate work
}
```

## Caching Query Results

```csharp
var cache = provider.GetRequiredService<IQueryResultCache>();

const string cacheKey = "top-100-customers";

// Try to get from cache
var customers = await cache.GetAsync<List<Customer>>(cacheKey);

if (customers == null)
{
    // Not in cache, query Databricks
    customers = await dataService.ExecuteQueryAsync<Customer>(
        "SELECT * FROM customers ORDER BY lifetime_value DESC LIMIT 100"
    );

    // Cache for 10 minutes
    await cache.SetAsync(cacheKey, customers, TimeSpan.FromMinutes(10));
    Console.WriteLine("Results cached");
}
else
{
    Console.WriteLine("Using cached results");
}

// View cache stats
var stats = cache.GetStatistics();
Console.WriteLine($"Cache hit rate: {stats.HitRate:P}");
```

## Next Steps

1. **Check [README.md](README.md)** for detailed documentation
2. **Review [Examples/SampleUsage.cs](Examples/SampleUsage.cs)** for more patterns
3. **Set up Kubernetes** deployment using manifests in `k8s/` folder
4. **Configure authentication** for your environment (Managed Identity, Key Vault, etc.)

## Troubleshooting

### "No valid authentication method available"
- Check environment variables are set correctly
- For Managed Identity: verify pod identity binding in Kubernetes
- For PAT: verify token is valid and has Databricks permissions

### "Column name does not match"
- Check Databricks table schema: `DESC table_name`
- Properties must match column names (case-insensitive)
- Use `JsonPropertyName` attribute for custom naming

### "Query timeout"
- Increase `DATABRICKS_TIMEOUT_SECONDS` if needed
- Optimize the query for Databricks
- Check SQL warehouse is running

## Common Patterns

### Pagination

```csharp
int pageSize = 100;
int page = 0;

while (true)
{
    var offset = page * pageSize;
    var customers = await dataService.ExecuteQueryAsync<Customer>(
        $"SELECT * FROM customers LIMIT {pageSize} OFFSET {offset}"
    );

    if (customers.Count == 0) break;

    // Process page
    foreach (var c in customers) { /*...*/ }

    page++;
}
```

### Aggregation

```csharp
public class SalesStats
{
    public string region { get; set; }
    public decimal total_sales { get; set; }
    public int transaction_count { get; set; }
}

var stats = await dataService.ExecuteQueryAsync<SalesStats>(
    "SELECT region, SUM(amount) as total_sales, COUNT(*) as transaction_count " +
    "FROM sales GROUP BY region"
);
```

### Error Handling

```csharp
try
{
    var results = await dataService.ExecuteQueryAsync<Customer>(query);
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Query failed: {ex.Message}");
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"API error: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected error: {ex.Message}");
}
```
