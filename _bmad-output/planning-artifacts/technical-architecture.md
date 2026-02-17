# Technical Architecture - High-Scale URL Shortener

## 1. High-Level Architecture

The system follows a microservices-style architecture (or modular monolith) optimized for read-heavy workloads (100:1 read/write ratio).

```mermaid
graph TD
    User[User/Client] -->|HTTP req| LB[Load Balancer]
    LB -->|/shorten or /{code}| API[UrlShortener API (.NET 8)]
    
    subgraph Data Layer
        API -->|Read/Write| Cache[Redis Cluster]
        API -->|Sharded Write| DB_Router[Sharding Logic]
        
        DB_Router -->|Shard 1| DB1[(Postgres Shard 1)]
        DB_Router -->|Shard 2| DB2[(Postgres Shard 2)]
        DB_Router -->|Shard N| DBN[(Postgres Shard N)]
    end
    
    subgraph Background Services
        API -.->|Async Click Events| EventQ[Redis Stream / Channel]
        Processor[Stats Processor] -->|Read| EventQ
        Processor -->|Batch Update| RedisStats[Redis Stats/HLL]
    end
```

## 2. Request Flow: Create URL
1.  **Request**: Client sends `POST /api/v1/shorten` with `long_url`.
2.  **ID Generation**: Application requests a unique ID range from a centralized **Key Generation Service** (or Redis `INCR` based block allocator) to ensure uniqueness without DB constraints.
    *   *Decision*: Use Redis `INCR` to allocate blocks of 1000 IDs to each app instance to minimize network calls.
3.  **Encoding**: Convert Unique ID to Base62 `short_code`.
    *   Example: ID `12345678` -> `aB3d`.
4.  **Sharding**: Calculate Shard ID: `CRC32(short_code) % Total_Shards`.
5.  **Persistence**:
    *   Insert `(short_code, long_url)` into the calculated PostgreSQL Shard.
    *   *Tradeoff*: Write-through cache is skipped to reduce latency. Cache is populated on first read (Read-Repair).
6.  **Response**: Return `short_code` to client.

## 3. Request Flow: Redirect
1.  **Request**: Client visits `/{short_code}`.
2.  **Cache Lookup**:
    *   Compute Redis Key: `url:{short_code}`.
    *   Call Redis GET.
    *   **Hit**: Return 302 Redirect immediately.
    *   **Miss**: 
        *   Calculate Shard ID.
        *   Query appropriate Postgres Shard.
        *   If found, write to Redis (with TTL) and return 302.
        *   If not found, return 404.
3.  **Analytics (Async)**:
    *   Publish "Click Event" (`short_code`, timestamp) to an in-memory buffer or Redis Stream.
    *   Background worker aggregates counts and updates Redis `stats:{short_code}` (HyperLogLog or Counter).

## 4. Database Design (PostgreSQL)

### Schema (Per Shard)

```sql
CREATE TABLE urls (
    short_code VARCHAR(10) PRIMARY KEY, -- Shard Key
    original_url TEXT NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    expires_at TIMESTAMP WITH TIME ZONE
);

CREATE INDEX idx_expires_at ON urls(expires_at) WHERE expires_at IS NOT NULL;
```

*Note: `click_count` is NOT stored in the main `urls` table to avoid write locks on high-traffic links. It is stored in Redis/separate stats table.*

## 5. Sharding Strategy

*   **Algorithm**: Hash-based Sharding.
*   **Key**: `short_code`.
*   **Function**: `hash(short_code) % NumberOfShards`.
    *   *Rationale*: Distributes specific implementation details away from the ID generation logic. Allows looking up a URL knowing only the code.
*   **Resharding**: Consistent Hashing (future proofing) or fixed buckets (MVP). For 10M req/day (writes are ~1% = 100k/day), a fixed set of Logical Shards mapping to Physical DBs is sufficient for years.
    *   *Plan*: 3 Physical DB Servers, 30 Logical Shards.

## 6. Caching Strategy (Redis)

*   **Cluster Mode**: Enabled for high availability and spreading memory load.
*   **Eviction Policy**: `allkeys-lru`. We want to keep popular URLs in memory.
*   **TTL**:
    *   **Hot URLs**: 24 hours + Sliding Expiration (reset TTL on hit).
    *   **Cold URLs**: Allowed to expire.
*   **Stampede Prevention**:
    *   Use "Probabilistic Early Expiration" or simple locking for the very first re-fetcher.

## 7. Scaling Strategy

*   **App Layer**: Stateless .NET 8 Containers (Kubernetes/ECS). Auto-scale based on CPU/Request Latency.
*   **Data Layer**:
    *   **Reads**: Read Replicas for Postgres (if Cache Misses are high).
    *   **Writes**: Add more Physical Shards.
*   **Cache**: Add Redis nodes.

## 8. High Availability & Resilience

*   **Redis**: Multi-AZ with Automatic Failover.
*   **Postgres**: Primary-Replica setup with synchronous commit disabled (for performance) but WAL archiving for safety.
*   **Fallback**: If Redis fails, fall back to DB (with heavy rate limiting).

## 9. Bottlenecks & Mitigation

| Bottleneck | Mitigation |
| :--- | :--- |
| **Hot Keys** (Viral Links) | Local In-Memory Cache (MemoryCache) in .NET app for *extremely* hot items (TTL 5s). |
| **ID Generation** | Redis `INCR` is single point of failure. Mitigation: Dual Redis or Database Sequence fallback. |
| **Connection Limits** | TCP Connection Pooling (PgBouncer) between App and DB/Shards. |

## 10. Observability

*   **Metrics (Prometheus/Grafana)**:
    *   `redirect_latency_ms` (p50, p95, p99)
    *   `cache_hit_ratio`
    *   `active_connections`
*   **Tracing (OpenTelemetry/Jaeger)**: Trace request from Ingress -> API -> Redis -> DB.
*   **Logs**: Serilog (Structured) -> Elasticsearch/Seq.

## 11. Cost Considerations (Cloud Neutral)

*   **Compute**: Spot instances for stateless API nodes.
*   **DB**: Managed Postgres is expensive. For cost-optimization, run Postgres on VM with attached SSD, but Managed is recommended for ops.
*   **Redis**: Memory is the biggest cost driver. Short keys + protobuf/msgpack compression can save space.
