using Microsoft.Extensions.Logging;

namespace DatabricksService.Caching;

/// <summary>
/// In-memory cache for query results with TTL support
/// </summary>
public interface IQueryResultCache
{
    /// <summary>
    /// Gets a cached result if available and not expired
    /// </summary>
    Task<T?> GetAsync<T>(string cacheKey) where T : class;

    /// <summary>
    /// Sets a cached result with TTL
    /// </summary>
    Task SetAsync<T>(string cacheKey, T value, TimeSpan ttl) where T : class;

    /// <summary>
    /// Removes a cached entry
    /// </summary>
    Task RemoveAsync(string cacheKey);

    /// <summary>
    /// Clears all cached entries
    /// </summary>
    Task ClearAsync();

    /// <summary>
    /// Gets cache statistics
    /// </summary>
    CacheStatistics GetStatistics();
}

public class QueryResultCache : IQueryResultCache
{
    private readonly ILogger<QueryResultCache> _logger;
    private readonly Dictionary<string, CacheEntry> _cache = new();
    private long _hits = 0;
    private long _misses = 0;

    public QueryResultCache(ILogger<QueryResultCache> logger)
    {
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string cacheKey) where T : class
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            throw new ArgumentException("Cache key cannot be empty", nameof(cacheKey));
        }

        lock (_cache)
        {
            if (_cache.TryGetValue(cacheKey, out var entry))
            {
                // Check if entry has expired
                if (DateTime.UtcNow < entry.ExpiryTime)
                {
                    Interlocked.Increment(ref _hits);
                    _logger.LogDebug("Cache hit for key '{CacheKey}'", cacheKey);
                    return Task.FromResult(entry.Value as T);
                }
                else
                {
                    // Entry has expired, remove it
                    _cache.Remove(cacheKey);
                    _logger.LogDebug("Cache entry '{CacheKey}' has expired and was removed", cacheKey);
                }
            }

            Interlocked.Increment(ref _misses);
            _logger.LogDebug("Cache miss for key '{CacheKey}'", cacheKey);
            return Task.FromResult((T?)null);
        }
    }

    public Task SetAsync<T>(string cacheKey, T value, TimeSpan ttl) where T : class
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            throw new ArgumentException("Cache key cannot be empty", nameof(cacheKey));
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentException("TTL must be greater than zero", nameof(ttl));
        }

        lock (_cache)
        {
            var entry = new CacheEntry
            {
                Value = value,
                ExpiryTime = DateTime.UtcNow.Add(ttl),
                CachedAt = DateTime.UtcNow
            };

            _cache[cacheKey] = entry;
            _logger.LogDebug("Cached value for key '{CacheKey}' with TTL {TtlSeconds}s",
                cacheKey, ttl.TotalSeconds);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            throw new ArgumentException("Cache key cannot be empty", nameof(cacheKey));
        }

        lock (_cache)
        {
            if (_cache.Remove(cacheKey))
            {
                _logger.LogDebug("Removed cached value for key '{CacheKey}'", cacheKey);
            }
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        lock (_cache)
        {
            var count = _cache.Count;
            _cache.Clear();
            _logger.LogInformation("Cleared {EntryCount} entries from cache", count);
        }

        return Task.CompletedTask;
    }

    public CacheStatistics GetStatistics()
    {
        lock (_cache)
        {
            var totalRequests = _hits + _misses;
            var hitRate = totalRequests > 0 ? (double)_hits / totalRequests : 0;

            return new CacheStatistics
            {
                TotalEntries = _cache.Count,
                TotalHits = _hits,
                TotalMisses = _misses,
                HitRate = hitRate,
                SnapshotTime = DateTime.UtcNow
            };
        }
    }

    private class CacheEntry
    {
        public object? Value { get; set; }
        public DateTime ExpiryTime { get; set; }
        public DateTime CachedAt { get; set; }
    }
}

public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public long TotalHits { get; set; }
    public long TotalMisses { get; set; }
    public double HitRate { get; set; }
    public DateTime SnapshotTime { get; set; }

    public override string ToString()
    {
        return $"Entries: {TotalEntries}, Hits: {TotalHits}, Misses: {TotalMisses}, " +
               $"HitRate: {HitRate:P2}, Snapshot: {SnapshotTime:O}";
    }
}
