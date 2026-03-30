# High-Scale URL Shortener

A high-performance URL shortening service designed to handle massive scale (10M+ requests/day) with low latency and high availability.

## 🚀 Key Features

*   **High Throughput**: Capable of supporting 10M+ daily requests (~115 req/sec avg, >1000 req/sec peak).
*   **Low Latency**: Lightning-fast redirection with < 50ms response time (99th percentile).
*   **Scalability**: Architected for horizontal scaling with database sharding for both reads and writes.
*   **Reliability**: Built to be resilient with Redis fallback and replication support.

## 🛠️ Technology Stack

*   **Runtime**: .NET 8 (C#)
*   **Database**: PostgreSQL (Sharded Architecture)
*   **Caching**: Redis (Distributed Cache)
*   **Testing**: k6 (for Load/Stress testing)
*   **Infrastructure**: Docker (Containerization for easy deployment)


