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

## 🏗️ Architecture

The system utilizes a modern, distributed architecture to achieve its performance metrics:

*   **Identifier Generation**: Uses pre-generated unique IDs (Base62 format: A-Z, a-z, 0-9) to avoid costly collision checks during URL creation.
*   **Sharding Strategy**: Uses a hashed short code strategy to distribute data across multiple PostgreSQL partitions/shards.
*   **Caching Strategy**: 
    *   **Read-Through**: Fast cache lookups on every redirect request.
    *   **Eviction Policy**: LRU (Least Recently Used) to ensure the most frequently accessed ("hot") URLs remain in memory.

## 📈 Success Metrics

*   **Performance**: Sustain 10M requests/day without performance degradation.
*   **Latency**: P99 redirection time under 50ms.
*   **Error Rate**: Maintain < 0.01% error rate under load.

## 💻 Getting Started

*(Instructions for local development, building, and running via Docker to be added here.)*
