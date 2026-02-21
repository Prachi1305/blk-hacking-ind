# BlackRock Auto-Save API – `blk-hacking-ind`

Production-grade REST API that implements an automated retirement micro-savings
system based on expense rounding (multiples of 100) with NPS and Index Fund
investment options.

---

## Table of Contents

1. [Problem Summary](#1-problem-summary)
2. [Approach & Complexity](#2-approach--complexity)
3. [Project Structure](#3-project-structure)
4. [Prerequisites](#4-prerequisites)
5. [Run Locally (dotnet)](#5-run-locally-dotnet)
6. [Run with Docker](#6-run-with-docker)
7. [Run with Docker Compose](#7-run-with-docker-compose)
8. [API Reference](#8-api-reference)
9. [Sample Requests & Expected Outputs](#9-sample-requests--expected-outputs)
10. [Run Tests](#10-run-tests)
11. [Edge Cases Handled](#11-edge-cases-handled)

---

## 1. Problem Summary

Given a list of **expenses**, round each up to the next multiple of 100.
The **remanent** (difference) is the micro-savings amount.

Three types of period-based adjustments are applied in order:

| Step | Rule | Effect |
|------|------|--------|
| q | Fixed-amount override | Replace remanent with `fixed` value for matching dates |
| p | Extra-amount addition | Add `extra` to remanent for matching dates (stacks) |
| k | Evaluation grouping | Group transactions by date range and sum remanents |

Results are fed into either **NPS (7.11% p.a.)** or **Index Fund (14.49% p.a.)**
compound interest, then adjusted for **5.5% inflation** to produce real returns.

---

## 2. Approach & Complexity

### Per-transaction processing

```
For each transaction (O(n)):
  1.  Ceiling   = ceil(amount / 100) × 100    → O(1)
  2.  Remanent  = ceiling – amount             → O(1)
  3.  q-match   = filter q periods by date     → O(q), pick max-start
  4.  p-match   = filter p periods by date     → O(p), sum all extras
  5.  k-group   = for each k period, include if date in range → O(k)
```

| Metric | Value |
|--------|-------|
| Time   | **O(n × (q + p + k))** – linear in inputs |
| Space  | **O(n + k)** – transactions + k-group accumulators |

> For n, q, p, k < 10⁶ the algorithm is efficient enough in practice with
> sorted period lists and binary search (future optimisation).

---

## 3. Project Structure

```
blk-hacking-ind/
├── Controllers/
│   ├── TransactionsController.cs  # parse / validator / filter
│   ├── ReturnsController.cs       # :nps / :index
│   └── PerformanceController.cs   # /performance
├── Models/
│   └── Models.cs                  # all DTOs / records
├── Services/
│   ├── TransactionService.cs      # period-processing logic
│   └── ReturnsService.cs          # compound interest, tax, inflation
├── test/
│   └── ChallengeTests.cs          # xUnit unit + integration tests
├── Program.cs
├── blk-hacking-ind.csproj
├── Dockerfile
├── compose.yaml
└── README.md
```

---

## 4. Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 8.0+ |
| Docker | 24+ (optional) |
| Docker Compose | v2+ (optional) |

---

## 5. Run Locally (dotnet)

```bash
cd blk-hacking-ind
dotnet run
# API available at http://localhost:5477
# Swagger UI at  http://localhost:5477/swagger
```

---

## 6. Run with Docker

```bash
# Build
docker build -t blk-hacking-ind-prachi-gaikwad .

# Run
docker run -d -p 5477:5477 blk-hacking-ind-prachi-gaikwad
```

---

## 7. Run with Docker Compose

```bash
docker compose up --build -d
```

---

## 8. API Reference

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/blackrock/challenge/v1/transactions:parse` | Parse expenses into transactions |
| POST | `/blackrock/challenge/v1/transactions:validator` | Validate transactions |
| POST | `/blackrock/challenge/v1/transactions:filter` | Apply q / p / k period rules |
| POST | `/blackrock/challenge/v1/returns:nps` | Calculate NPS returns |
| POST | `/blackrock/challenge/v1/returns:index` | Calculate NIFTY 50 returns |
| GET  | `/blackrock/challenge/v1/performance` | System performance metrics |

---

## 9. Sample Requests & Expected Outputs

### 9.1 Parse

**Request**
```json
POST /blackrock/challenge/v1/transactions:parse
{
  "expenses": [
    { "timestamp": "2023-10-12 20:15:00", "amount": 250 },
    { "timestamp": "2023-02-28 15:49:00", "amount": 375 },
    { "timestamp": "2023-07-01 21:59:00", "amount": 620 },
    { "timestamp": "2023-12-17 08:09:00", "amount": 480 }
  ]
}
```

**Response**
```json
{
  "transactions": [
    { "date": "2023-10-12 20:15:00", "amount": 250, "ceiling": 300, "remanent": 50 },
    { "date": "2023-02-28 15:49:00", "amount": 375, "ceiling": 400, "remanent": 25 },
    { "date": "2023-07-01 21:59:00", "amount": 620, "ceiling": 700, "remanent": 80 },
    { "date": "2023-12-17 08:09:00", "amount": 480, "ceiling": 500, "remanent": 20 }
  ],
  "totalAmount": 1725,
  "totalCeiling": 1900,
  "totalRemanent": 175
}
```

### 9.2 filter (q + p + k)

**Request**
```json
POST /blackrock/challenge/v1/transactions:filter
{
  "q": [{ "fixed": 0, "start": "2023-07-01 00:00:00", "end": "2023-07-31 23:59:00" }],
  "p": [{ "extra": 25, "start": "2023-10-01 08:00:00", "end": "2023-12-31 19:59:00" }],
  "k": [
    { "start": "2023-03-01 00:00:00", "end": "2023-11-30 23:59:00" },
    { "start": "2023-01-01 00:00:00", "end": "2023-12-31 23:59:00" }
  ],
  "transactions": [
    { "date": "2023-10-12 20:15:00", "amount": 250, "ceiling": 300, "remanent": 50 },
    { "date": "2023-02-28 15:49:00", "amount": 375, "ceiling": 400, "remanent": 25 },
    { "date": "2023-07-01 21:59:00", "amount": 620, "ceiling": 700, "remanent": 80 },
    { "date": "2023-12-17 08:09:00", "amount": 480, "ceiling": 500, "remanent": 20 }
  ]
}
```

**Expected effective remanents:** Oct-12 → 75, Feb-28 → 25, Jul-01 → 0, Dec-17 → 45

### 9.3 returns:index (PDF example)

**Expected output for k2 (full year):**
```json
{ "start": "2023-01-01 00:00:00", "end": "2023-12-31 23:59:00",
  "amount": 145.0, "profits": ~1684, "taxBenefit": 0 }
```

---

## 10. Run Tests

```bash
# Test type    : Unit Tests (xUnit)
# Validation   : TransactionService + ReturnsService logic
# Command:
dotnet test test/blk-hacking-ind.Tests.csproj --logger "console;verbosity=detailed"
```

---

## 11. Edge Cases Handled

| Scenario | Behaviour |
|----------|-----------|
| Amount already a multiple of 100 | Ceiling = amount + 100 |
| Amount = 0 or negative | Ceiling = 100, remanent = 100 - amount |
| Multiple q periods overlap | Latest `start` date wins; first-in-list on tie |
| Multiple p periods overlap | All extras are summed |
| Transaction outside all k periods | Marked invalid with message |
| Age ≥ 60 | Investment years = 5 (minimum) |
| Income in 0% tax slab (≤ 7 L) | Tax benefit = 0 for NPS |
| Duplicate transaction dates | Detected separately in duplicates list |
| Ceiling / remanent mismatch in input | Marked invalid with explanation |
| Empty q / p / k lists | Handled gracefully (no period adjustments) |
