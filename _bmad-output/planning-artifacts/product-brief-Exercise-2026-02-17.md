---
stepsCompleted: [1]
inputDocuments: []
date: 2026-02-17
author: User
---

# Product Brief: Exercise - High-Scale URL Shortener

<!-- Content appended based on user requirements -->

## 1. Project Overview & Vision
A high-performance URL shortening service designed to handle massive scale (10M+ requests/day) with low latency and high availability. The system will leverage modern .NET 8 features, distributed caching with Redis, and horizontal scaling via PostgreSQL sharding.

## 2. Key Objectives
- **High Throughput**: Support 10M+ daily requests (~115 req/sec avg, >1000 req/sec peak).
- **Low Latency**: <50ms response time for redirection (99th percentile).
- **Scalability**: Horizontal sharding for database writes/reads.
- **Reliability**: Resilient to failures with Redis fallback and replication.

## 3. Technical Requirements
- **Runtime**: .NET 8 (C#)
- **Database**: PostgreSQL (Sharded Architecture)
- **Caching**: Redis (Distributed Cache)
- **Testing**: k6 for Load/Stress testing
- **Containerization**: Docker support for easy deployment

## 4. Architectural Decisions
- **Sharding Strategy**:
    - **Key**: Hash of the Short Code (e.g., first 2 chars or consistent hashing ring).
    - **Mechanism**: Application-side sharding or Postgres partitioning (to be decided in implementation).
- **Identifier Generation**:
    - **Approach**: Pre-generated unique IDs (e.g., Hi-Lo algorithm, Snowflake, or distinct DB sequences) to avoid collision checks.
    - **Format**: Base62 (A-Z, a-z, 0-9) for short URLs.
- **Caching Strategy**:
    - **Read-Through**: Cache lookups on redirect.
    - **Write-Through/Behind**: Update cache on creation (optional, read-repair preferred).
    - **Eviction**: LRU to keep hot URLs in memory.

## 5. Success Metrics
- **Performance**: 10M req/day handled without degradation.
- **Latency**: P99 < 50ms for redirection.
- **Error Rate**: < 0.01% under load.
