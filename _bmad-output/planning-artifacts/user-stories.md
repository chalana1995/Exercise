# User Stories - High-Scale URL Shortener

## Phase 1: Foundation & Core API

### Story 1.1: Database Schema & Project Setup
**Description**: Initialize the .NET 8 Web API project and set up the PostgreSQL database schema for storing URLs.
**Acceptance Criteria**:
- [ ] .NET 8 Web API project created with Docker support.
- [ ] PostgreSQL container running locally.
- [ ] `urls` table created with columns: `short_code` (PK), `original_url`, `created_at`, `expires_at`.
- [ ] EF Core or Dapper configured for data access.
**Technical Notes**:
- Use `Npgsql`.
- Ensure `short_code` is case-sensitive (collation).

### Story 1.2: URL Shortening Endpoint (Single DB)
**Description**: Implement the `POST /api/v1/shorten` endpoint to create short URLs.
**Acceptance Criteria**:
- [ ] Input: `{ "long_url": "..." }`
- [ ] Generate unique ID/Code (Base62).
- [ ] Store in DB.
- [ ] Return 201 Created with short URL.
- [ ] Handle duplicate code errors (retry logic).
**Technical Notes**:
- Use a simple random generator or database sequence for MVP to get it working, before switching to the distributed ID generator in Phase 2.

### Story 1.3: Redirection Endpoint (No Cache)
**Description**: Implement the `GET /{short_code}` endpoint.
**Acceptance Criteria**:
- [ ] Query DB for `short_code`.
- [ ] If found, return 302 Found with `Location` header.
- [ ] If not found or expired, return 404.
**Technical Notes**:
- Ensure correct HTTP status codes.

## Phase 2: Scale (Sharding & Caching)

### Story 2.1: Implement Distributed ID Generator
**Description**: Replace simple ID generation with a Redis-backed block allocator or robust distributed ID system to ensure no collisions across multiple instances.
**Acceptance Criteria**:
- [ ] Service allocates blocks of IDs (e.g., 1000) from Redis `INCR`.
- [ ] Instance generates codes from its allocated block.
- [ ] No DB unique constraint violations under parallel load.
**Technical Notes**:
- Redis key: `global:url_id_sequence`.

### Story 2.2: Redis Caching Layer (Read-Through)
**Description**: Implement Redis caching to reduce DB load for redirections.
**Acceptance Criteria**:
- [ ] `GET /{code}` checks Redis first.
- [ ] Cache Hit: Return immediately (latency < 10ms).
- [ ] Cache Miss: Query DB, populate Redis (TTL 24h), return.
- [ ] Use `StackExchange.Redis`.
**Technical Notes**:
- Implement Circuit Breaker: If Redis is down, fall back to DB log warning.

### Story 2.3: Database Sharding Mechanism
**Description**: Implement application-side sharding logic to distribute data across multiple virtual/physical databases.
**Acceptance Criteria**:
- [ ] Sharding Key: `short_code`.
- [ ] Algorithm: `CRC32(short_code) % ShardCount`.
- [ ] Repository layer selects correct connection string based on the code.
- [ ] Migration script to create schema on all shards.
**Technical Notes**:
- Configure 3 logical shards (databases) locally for testing.

## Phase 3: Analytics & Ops

### Story 3.1: Asynchronous Click Tracking
**Description**: Decouple click counting from the main redirect path to preserve latency.
**Acceptance Criteria**:
- [ ] Redirection endpoint publishes event to Redis Stream / Channel.
- [ ] Background service consumes events.
- [ ] Update `click_count` in Redis (HyperLogLog or Counter) and periodically flush to DB.
**Technical Notes**:
- Do NOT await the stats write in the HTTP request.

### Story 3.2: Observability Setup (Metrics & Logs)
**Description**: Instrument the application to track key performance indicators.
**Acceptance Criteria**:
- [ ] Prometheus metrics exposed: `request_count`, `request_duration`, `active_connections`, `cache_hit_rate`.
- [ ] Structured Logging (Serilog) configured.
- [/health] endpoint checks DB and Redis connectivity.
**Technical Notes**:
- Use OpenTelemetry .NET SDK.

### Story 3.3: Rate Limiting
**Description**: Protect the write endpoint from abuse.
**Acceptance Criteria**:
- [ ] Limit `POST /shorten` to N requests per minute per IP.
- [ ] Return 429 Too Many Requests when exceeded.
**Technical Notes**:
- Use `AspNetCoreRateLimit` or build simple Redis middleware.

## Phase 4: Verification & Deployment

### Story 4.1: k6 Load Testing Suite
**Description**: Create performance tests to validate the 10M/day requirement.
**Acceptance Criteria**:
- [ ] Script to simulate high read traffic (100:1 read/write).
- [ ] Script to test peak write loads.
- [ ] Verify P95 latency is under 100ms.
**Technical Notes**:
- Use k6 Docker container.

### Story 4.2: CI/CD Pipeline
**Description**: Automate build and test process.
**Acceptance Criteria**:
- [ ] GitHub Actions workflow created.
- [ ] Runs Unit Tests on PR.
- [ ] Builds Docker image on merge to main.
