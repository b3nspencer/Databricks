using DatabricksService;
using DatabricksService.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DatabricksService.Examples;

/// <summary>
/// Example usage of the Databricks data access service
/// </summary>
public class SampleUsage
{
    // Sample data class for deserialization
    public class User
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Product
    {
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
    }

    /// <summary>
    /// Example 1: Simple query execution
    /// </summary>
    public static async Task Example1_SimpleQueryAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        // Configure Databricks connection
        services.AddDatabricksServices(config =>
        {
            config.WorkspaceUrl = "https://adb-your-workspace-id.azuredatabricks.net";
            config.WarehouseId = "your-warehouse-id";
            config.UseManagedIdentity = true; // Use Managed Identity in production
            config.TimeoutSeconds = 600;
            config.WaitTimeoutSeconds = 10;
        });

        var provider = services.BuildServiceProvider();
        var dataService = provider.GetRequiredService<IDatabricksDataAccessService>();

        try
        {
            // Execute a simple query
            var users = await dataService.ExecuteQueryAsync<User>(
                "SELECT id, name, email, created_at FROM users LIMIT 10"
            );

            Console.WriteLine($"Retrieved {users.Count} users");
            foreach (var user in users)
            {
                Console.WriteLine($"  {user.Id}: {user.Name} ({user.Email})");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Example 2: Parameterized query
    /// </summary>
    public static async Task Example2_ParameterizedQueryAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddDatabricksServicesFromEnvironment();

        var provider = services.BuildServiceProvider();
        var dataService = provider.GetRequiredService<IDatabricksDataAccessService>();

        try
        {
            // Execute a parameterized query
            var parameters = new Dictionary<string, string>
            {
                { "min_price", "100" },
                { "max_price", "1000" }
            };

            var products = await dataService.ExecuteQueryAsync<Product>(
                "SELECT product_id, product_name, price, stock FROM products " +
                "WHERE price BETWEEN @min_price AND @max_price",
                parameters
            );

            Console.WriteLine($"Retrieved {products.Count} products in price range");
            foreach (var product in products)
            {
                Console.WriteLine($"  {product.ProductName}: ${product.Price} ({product.Stock} in stock)");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Example 3: Streaming large result sets
    /// </summary>
    public static async Task Example3_StreamingResultsAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddDatabricksServicesFromEnvironment();

        var provider = services.BuildServiceProvider();
        var dataService = provider.GetRequiredService<IDatabricksDataAccessService>();

        try
        {
            long processedCount = 0;

            // Stream results instead of loading all into memory
            await dataService.ExecuteQueryAsyncStream<Product>(
                "SELECT product_id, product_name, price, stock FROM products",
                async (product) =>
                {
                    // Process each row as it comes
                    processedCount++;
                    if (processedCount % 1000 == 0)
                    {
                        Console.WriteLine($"Processed {processedCount} products...");
                    }
                    // You could write to a database, file, or aggregate statistics here
                    await Task.CompletedTask;
                }
            );

            Console.WriteLine($"Completed processing {processedCount} products");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Example 4: Query result caching
    /// </summary>
    public static async Task Example4_CachingAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddDatabricksServicesFromEnvironment();

        var provider = services.BuildServiceProvider();
        var dataService = provider.GetRequiredService<IDatabricksDataAccessService>();
        var cache = provider.GetRequiredService<IQueryResultCache>();

        try
        {
            const string cacheKey = "top-products";

            // Check cache first
            var cachedProducts = await cache.GetAsync<List<Product>>(cacheKey);
            if (cachedProducts != null)
            {
                Console.WriteLine("Using cached results");
                foreach (var product in cachedProducts.Take(5))
                {
                    Console.WriteLine($"  {product.ProductName}: ${product.Price}");
                }
            }
            else
            {
                // Execute query and cache results for 5 minutes
                var products = await dataService.ExecuteQueryAsync<Product>(
                    "SELECT product_id, product_name, price, stock FROM products " +
                    "ORDER BY stock DESC LIMIT 100"
                );

                await cache.SetAsync(cacheKey, products, TimeSpan.FromMinutes(5));

                Console.WriteLine($"Cached {products.Count} products");
                foreach (var product in products.Take(5))
                {
                    Console.WriteLine($"  {product.ProductName}: ${product.Price}");
                }
            }

            // Display cache statistics
            var stats = cache.GetStatistics();
            Console.WriteLine($"\nCache Statistics: {stats}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Example 5: Raw query response handling
    /// </summary>
    public static async Task Example5_RawResponseAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddDatabricksServicesFromEnvironment();

        var provider = services.BuildServiceProvider();
        var dataService = provider.GetRequiredService<IDatabricksDataAccessService>();

        try
        {
            var response = await dataService.ExecuteRawQueryAsync(
                "SELECT COUNT(*) as count FROM users"
            );

            Console.WriteLine($"Query State: {response.State}");
            Console.WriteLine($"Row Count: {response.Result?.RowCount}");

            if (response.Result?.Statistics != null)
            {
                Console.WriteLine($"Execution Time: {response.Result.Statistics.ExecutionDurationMs}ms");
                Console.WriteLine($"Bytes Read: {response.Result.Statistics.BytesRead}");
            }

            if (response.Result?.DataArray != null && response.Result.DataArray.Count > 0)
            {
                var firstRow = response.Result.DataArray[0];
                Console.WriteLine($"Total users: {firstRow.FirstOrDefault()}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
        }
    }
}
