using System.Text;

namespace UrlShortener.Api.Services;

public class UrlShortenerService
{
    private readonly RedisIdGenerator _idGenerator;
    private readonly ShardedUrlRepository _repository;
    private readonly ILogger<UrlShortenerService> _logger;
    private const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public UrlShortenerService(RedisIdGenerator idGenerator, ShardedUrlRepository repository, ILogger<UrlShortenerService> logger)
    {
        _idGenerator = idGenerator;
        _repository = repository;
        _logger = logger;
    }

    public async Task<ShortUrlResult> ShortenUrlAsync(string longUrl, int? expiresInDays)
    {
        // 1. Generate Unique ID
        long id = await _idGenerator.GenerateIdAsync();

        // 2. Encode to Base62
        string shortCode = EncodeBase62(id);

        // 3. Define Expiration
        DateTime expiresAt = DateTime.UtcNow.AddDays(expiresInDays ?? 365);

        // 4. Save to Sharded DB
        await _repository.SaveUrlAsync(shortCode, longUrl, expiresAt);

        // 5. Construct Result
        // In real app, domain comes from config
        string shortUrl = $"https://short.ly/{shortCode}"; 

        return new ShortUrlResult(shortCode, shortUrl, expiresAt);
    }

    public async Task<string?> GetOriginalUrlAsync(string code)
    {
        // 1. Check Cache (Read-Through) implemented in Repository for separation of concerns
        return await _repository.GetUrlAsync(code);
    }

    public async Task TrackClickAsync(string code)
    {
        try
        {
            await _repository.IncrementClickCountAsync(code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track click for {Code}", code);
        }
    }

    private static string EncodeBase62(long id)
    {
        if (id == 0) return "0";
        var sb = new StringBuilder();
        while (id > 0)
        {
            sb.Insert(0, Base62Chars[(int)(id % 62)]);
            id /= 62;
        }
        return sb.ToString();
    }
}
