# Product Requirements Document (PRD) - High-Scale URL Shortener

## 1. System Overview
The High-Scale URL Shortener is a specialized service designed to convert long URLs into compact, shareable unique keys. It is architected to handle massive scale (10M+ requests/day), ensuring high availability, low latency redirection, and data consistency through sharded database storage and distributed caching.

## 2. Functional Requirements

### 2.1 Create Short URL
- **Input**: Long URL (e.g., `https://www.example.com/very/long/path?query=param`), Optional: Custom Alias (if supported in future, generic for now), Optional: Expiration Date.
- **Output**: Short URL (e.g., `https://short.ly/aB3d`).
- **Behavior**:
  - Validate Long URL format.
  - Generate a unique, collision-free short code (Base62).
  - Store mapping in database sharded by short code.
  - Return the constructed short URL.
  - Default expiration: 365 days if not specified.

### 2.2 Redirect to Original URL
- **Input**: Short Code (from URL path).
- **Behavior**:
  - Lookup Short Code in Redis Cache (Read-Through).
  - If miss, lookup in Sharded PostgreSQL Database.
  - If found, return HTTP 302 (Found) or 301 (Moved Permanently) based on configuration (302 preferred for analytics).
  - Asynchronously increment click count (Writes to Redis HyperLogLog or specific counter, flushed to DB periodically).
  - If not found or expired, return HTTP 404.

### 2.3 Expiration Support
- **Behavior**:
  - URLs can have an optional `expires_at` timestamp.
  - Redirection requests check this timestamp.
  - Background job periodically cleans up expired entries (or standard TTL in Redis/DB if supported).

### 2.4 Analytics (Basic Click Tracking)
- **Metrics**: Total Clicks.
- **Granularity**: Global count per short URL.
- **Access**: Via separate API endpoint.

## 3. Non-Functional Requirements

### 3.1 Performance & Scalability
- **Throughput**: Support 10,000,000+ redirect requests/day (~116 RPS avg, ~1,200 RPS peak).
- **Latency**: P95 < 100ms for redirection (cache hit), P95 < 200ms (cache miss).
- **Write Scalability**: Horizontal sharding to support growing data volume (billions of URLs).

### 3.2 Reliability & Availability
- **Uptime**: 99.9% Availability.
- **Resilience**: Redis Cluster for caching resilience; Postgres Replication for data durability.
- **Circuit Breakers**: Implemented for DB and Cache connections.

### 3.3 Security & Limits
- **Rate Limiting**: Per-IP or API Key limiting (e.g., 10 creates/minute/IP) to prevent abuse.
- **Validation**: Strict URL validation to prevent malicious links (optional integration with safe browsing APIs in future).

## 4. Data Requirements

### 4.1 Data Model (Conceptual)

**Table: `short_urls`**
| Column | Type | Description |
| :--- | :--- | :--- |
| `short_code` | `VARCHAR(7)` | **Shard Key**. Primary Key. Base62 encoded ID. |
| `original_url` | `TEXT` | The original long URL. |
| `created_at` | `TIMESTAMP` | Creation time. |
| `expires_at` | `TIMESTAMP` | Expiration time (nullable). |
| `click_count` | `BIGINT` | Total clicks (updated asynchronously). |

### 4.2 Caching Schema (Redis)
- **Key**: `url:{short_code}`
- **Value**: `original_url`
- **TTL**: Configurable (e.g., 24 hours), with sliding expiration on access.

## 5. API Contract

### 5.1 Create Short URL
**POST** `/api/v1/shorten`
**Request Body**:
```json
{
  "long_url": "https://www.example.com/some/resource",
  "expires_in_days": 30
}
```
**Response (201 Created)**:
```json
{
  "short_code": "aB3d",
  "short_url": "https://short.ly/aB3d",
  "expires_at": "2026-03-19T12:00:00Z"
}
```

### 5.2 Redirect
**GET** `/{short_code}`
**Response**:
- `302 Found` (Location: `original_url`)
- `404 Not Found` (If code invalid or expired)

### 5.3 Get Stats
**GET** `/api/v1/stats/{short_code}`
**Response (200 OK)**:
```json
{
  "short_code": "aB3d",
  "total_clicks": 1420
}
```

## 6. Edge Cases
- **Collision Handling**: Although pre-generated logic minimizes this, the system must handle duplicate key generation attempts gracefully (retry logic).
- **Cache Stampede**: Use probabilistic early expiration or locking when refreshing cache for hot keys.
- **Database Downtime**: Read-only mode if primary DB is down but replicas/cache are available.

## 7. Out of Scope
- Custom alias selection (e.g., `short.ly/my-brand`).
- Detailed analytics (User Agent, Geo-location, Referrer).
- User accounts/authentication for creation (Public API for MVP).
- Editing destination URL after creation.
