# Implementation Notes & Engineering Review

## 1. Code Review: Risks at 10M+ Req/Day

### Critical Bottlenecks
*   **Redis Dependency**: The `RedisIdGenerator` and `ShardedUrlRepository` heavily depend on Redis. 
    *   *Risk*: If Redis halts, the entire write path (creation) stops.
    *   *Mitigation*: Implement a DB-sequence fallback or local implementation of Hi-Lo if Redis is unreachable.
*   **Connection Exhaustion**: Opening a `new NpgsqlConnection` per request is expensive if pooling isn't tuned. 
    *   *Risk*: At 500+ req/sec, ephemeral port exhaustion.
    *   *Fix*: Rely on Npgsql built-in pooling, but ensure `Max Pool Size` in connection string is adequate (e.g., `Pooling=true;Max Pool Size=100;`).
*   **Sync Locking in ID Generator**: `lock (_lock)` in `GenerateIdAsync` is a potential contention point if a single instance handles >50k RPS.
    *   *Fix*: Use `Interlocked.Increment` or a lock-free approach if meaningful contention observes.

### Scalability Gaps
*   **Resharding**: The `GetShardId` uses `Modulo N`. Changing `N` (adding Shards) re-maps keys to different DBs, breaking lookups.
    *   *Production Fix*: Use Consistent Hashing (Hash Ring) OR keep the mapping table OR simple "Logical Shards" where we split a shard but keep the logical ID same.

## 2. Performance Improvements (Optimization Layer)

1.  **Protobuf/MsgPack Serialization**:
    Currently, we store raw strings in Redis. For an "Original URL", text is fine. But for objects, binary serialization saves bandwidth.
2.  **Local MemoryCache L1**:
    Add `IMemoryCache` before Redis for *extremely* hot keys (viral links).
    *   *Benefit*: Saves network rount-trip to Redis.
    *   *Cost*: Memory pressure on API nodes.
3.  **Brotli Compression**:
    Enable response compression for the JSON API endpoints.

## 3. P95 Validation Explanation

**Goal**: P95 < 100ms.
**Meaning**: 95% of requests must complete in under 100 milliseconds.

**Why k6?**
k6 handles high concurrency better than JMeter due to its Go engine. It allows us to script complex probabilistic scenarios (cache hits/misses).

### k6 Script Strategy
1.  **VUs (Virtual Users)**: Start with 10, ramp to 100, then 500.
2.  **Scenarios**: 
    *   `create_url`: 5% of traffic.
    *   `get_url_hit`: 85% of traffic (simulate cache hit).
    *   `get_url_miss`: 10% of traffic (simulate cold/long-tail).
3.  **Thresholds**: Fail build if `http_req_duration{p(95)} > 100`.

## 4. Run Instructions

### Prerequisites
*   Docker & Docker Compose
*   .NET 8 SDK

### Steps
1.  **Start Infra**: `docker run --name redis -p 6379:6379 -d redis`
2.  **Start DBs**: Run Postgres containers on ports 5432, 5433, 5434 (for shards).
3.  **Run API**: `dotnet run --project src/UrlShortener.Api`
4.  **Run Load Test**: `k6 run tests/load/script.js`
