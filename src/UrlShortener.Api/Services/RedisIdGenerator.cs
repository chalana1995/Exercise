using StackExchange.Redis;

namespace UrlShortener.Api.Services;

public class RedisIdGenerator
{
    private readonly IConnectionMultiplexer _redis;
    private const string GlobalSequenceKey = "global:url_id_sequence";
    
    // We reserve blocks of IDs to minimize Redis calls
    // e.g., each instance grabs 1000 IDs at a time
    private const int BlockSize = 1000;
    
    private long _currentStart = -1;
    private long _currentEnd = -1;
    private long _nextId = -1;
    private readonly object _lock = new();

    public RedisIdGenerator(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<long> GenerateIdAsync()
    {
        // Optimistic locking / simple synchronization for the local block
        // For extremely high scale, we might want a Channel/Buffer approach
        
        lock (_lock)
        {
            if (_nextId != -1 && _nextId <= _currentEnd)
            {
                return _nextId++;
            }
        }

        // Block exhausted, fetch new block
        return await AllocateNewBlockAsync();
    }

    private async Task<long> AllocateNewBlockAsync()
    {
        var db = _redis.GetDatabase();
        
        // Atomically increment global counter by BlockSize
        long endId = await db.StringIncrementAsync(GlobalSequenceKey, BlockSize);
        long startId = endId - BlockSize + 1;

        lock (_lock)
        {
            _currentStart = startId;
            _currentEnd = endId;
            _nextId = startId + 1; // Return first, clean next
            return startId;
        }
    }
}
