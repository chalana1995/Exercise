using Dapper;
using Npgsql;
using StackExchange.Redis;
using System.IO.Hashing;
using System.Text;

namespace UrlShortener.Api.Services;

public class ShardedUrlRepository
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IConfiguration _config;
    private readonly int _shardCount;

    public ShardedUrlRepository(IConnectionMultiplexer redis, IConfiguration config)
    {
        _redis = redis;
        _config = config;
        _shardCount = config.GetValue<int>("Sharding:ShardCount", 3); // Default 3 shards
    }

    public async Task SaveUrlAsync(string shortCode, string originalUrl, DateTime expiresAt)
    {
        // 1. Calculate Shard
        int shardId = GetShardId(shortCode);
        string connectionString = _config.GetConnectionString($"Shard{shardId}") 
            ?? throw new InvalidOperationException($"Missing config for Shard{shardId}");

        // 2. Write to DB
        // We do NOT write to Redis here (Read-Repair / Cache-Aside pattern) to save write ops
        const string sql = @"
            INSERT INTO urls (short_code, original_url, created_at, expires_at)
            VALUES (@ShortCode, @OriginalUrl, @CreatedAt, @ExpiresAt)
            ON CONFLICT (short_code) DO NOTHING;";

        using var conn = new NpgsqlConnection(connectionString);
        await conn.ExecuteAsync(sql, new { 
            ShortCode = shortCode, 
            OriginalUrl = originalUrl, 
            CreatedAt = DateTime.UtcNow, 
            ExpiresAt = expiresAt 
        });
    }

    public async Task<string?> GetUrlAsync(string shortCode)
    {
        var db = _redis.GetDatabase();
        string cacheKey = $"url:{shortCode}";

        // 1. Cache Lookup
        var cachedUrl = await db.StringGetAsync(cacheKey);
        if (cachedUrl.HasValue)
        {
            // Sliding expiration
            await db.KeyExpireAsync(cacheKey, TimeSpan.FromHours(24), CommandFlags.FireAndForget);
            return cachedUrl;
        }

        // 2. DB Lookup (Cache Miss)
        int shardId = GetShardId(shortCode);
        string connectionString = _config.GetConnectionString($"Shard{shardId}")!;

        using var conn = new NpgsqlConnection(connectionString);
        var url = await conn.QuerySingleOrDefaultAsync<string>(
            "SELECT original_url FROM urls WHERE short_code = @Code AND expires_at > NOW()", 
            new { Code = shortCode });

        // 3. Cache Population (Read Repair)
        if (url != null)
        {
            await db.StringSetAsync(cacheKey, url, TimeSpan.FromHours(24));
        }

        return url;
    }

    public async Task IncrementClickCountAsync(string shortCode)
    {
        // Use Redis HyperLogLog or simple Counter
        var db = _redis.GetDatabase();
        await db.StringIncrementAsync($"stats:clicks:{shortCode}");
    }

    private int GetShardId(string key)
    {
        // CRC32 is fast and provides good distribution
        byte[] bytes = Encoding.UTF8.GetBytes(key);
        var hash = Crc32.Hash(bytes);
        uint crc = BitConverter.ToUInt32(hash);
        return (int)(crc % _shardCount);
    }
}
