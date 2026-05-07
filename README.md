# LampLightLabs.JobSearch.Api

A personal ASP.NET Core Web API project built by **Michael Sargent** to dust off and sharpen API development skills following a career transition in early 2026.

---

## What This Project Does

This project serves two purposes:

**1. Job Search Pipeline Tracker**
The `ApplicationsController` reads a CSV file containing job applications and their current pipeline states, exposing that data via a REST endpoint. It uses CsvHelper to correctly handle quoted multi-line fields - a real-world parsing challenge solved during development.

**2. Async Long-Running Job Pattern (Exercise)**
The `JobsController` demonstrates a production-relevant API pattern: accepting a long-running request, returning a job ID immediately with `202 Accepted`, processing work asynchronously in the background, and exposing a polling endpoint the client can call to check job status. This pattern is common in compliance processing, batch operations, and file export workflows.

---

## Endpoints

All endpoints are versioned using URL segment versioning (`/api/v{version}/...`).

### Applications
| Method | Route | Version | Description |
|---|---|---|---|
| GET | `/api/v1/applications/fromcsv` | v1 | Returns raw job application records from the pipeline CSV |
| GET | `/api/v2/applications/fromcsv` | v2 | Returns enriched records with calculated pipeline intelligence fields |

### Jobs
| Method | Route | Version | Description |
|---|---|---|---|
| POST | `/api/v1/jobs/start` | v1 | Starts a background job, returns job ID immediately |
| GET | `/api/v1/jobs/{jobId}/status` | v1 | Polls the status of a running or completed job |

---

## API Versioning

This project uses URL segment versioning via the `Asp.Versioning.Mvc` library.

- Version is embedded in the URL path: `/api/v1/...`, `/api/v2/...`
- Unversioned requests default to v1
- Supported versions are reported in the `api-supported-versions` response header
- Swagger UI reflects both version definitions in the dropdown

### What Changed Between V1 and V2

**V1** returns raw CSV data as-is. No transformation, no calculation.

**V2** returns the same base fields plus three calculated fields:

| Field | Type | Description |
|---|---|---|
| `daysInPipeline` | int | Number of days since the application was submitted |
| `isFollowUpToday` | bool | True if the follow-up date matches today |
| `statusCategory` | string | Derived grouping: Active, OnHold, or Closed |

This mirrors a real-world versioning scenario - V1 consumers continue working unchanged while V2 consumers get enriched data with business logic applied server-side.

---

## Async Job Pattern - How It Works
Client                          Server
|                               |
|-- POST /api/v1/jobs/start --->|  Returns 202 Accepted + jobId immediately
|<-- { jobId, status: Queued } -|  Background work starts
|                               |
|-- GET /api/v1/jobs/{jobId}/status|  Poll #1
|<-- { status: Processing } ----|
|                               |
|-- GET /api/v1/jobs/{jobId}/status|  Poll #2 (after delay)
|<-- { status: Complete,        |
|      result: "Processed 29    |
|      applications" }          |

---

## Project Structure
LampLightLabs.JobSearch.Api/
├── Controllers/
│   ├── V1/
│   │   ├── ApplicationsController.cs   - Returns raw CSV data (v1)
│   │   └── JobsController.cs           - Async job pattern endpoints (v1)
│   └── V2/
│       └── ApplicationsController.cs   - Returns enriched data with calculated fields (v2)
├── Models/
│   ├── V1/
│   │   └── ApplicationResponse.cs      - Raw CSV field mapping
│   ├── V2/
│   │   └── ApplicationResponse.cs      - Adds DaysInPipeline, IsFollowUpToday, StatusCategory
│   └── JobRecord.cs                    - Job state model and JobStatus enum
├── Services/
│   ├── ICsvReaderService.cs            - CSV reader interface
│   ├── CsvReaderService.cs             - CsvHelper implementation
│   └── JobStore.cs                     - Thread-safe in-memory job store
├── TestData/
│   └── applications.csv                - Live job search pipeline data
└── Program.cs                          - DI registration and middleware
LampLightLabs.JobSearch.Api.Tests/
├── CsvReaderServiceTests.cs            - 4 unit tests
└── JobStoreTests.cs                    - 5 unit tests

---

## Tech Stack

- .NET 8.0
- ASP.NET Core Web API
- Asp.Versioning.Mvc 8.1.0
- CsvHelper
- xUnit v3
- Moq
- Swagger / Swashbuckle

---

## Running Locally

**Prerequisites:**
- Visual Studio 2022
- .NET 8.0 SDK

**Steps:**
1. Clone the repository
2. Open `LampLightLabs.JobSearch.Api.sln` in Visual Studio 2022
3. Set `LampLightLabs.JobSearch.Api` as the startup project
4. Press `F5` to run
5. Swagger UI will open automatically at `https://localhost:{port}/swagger`
6. Use the dropdown in the top right to switch between v1 and v2 definitions

**Running Tests:**
- Open Test Explorer (`Ctrl+E, T`)
- Click Run All

---

## Key Design Decisions

**URL segment versioning** - Version is embedded directly in the route path (`/api/v1/...`, `/api/v2/...`). This is the most explicit and widely adopted strategy for public APIs - visible at a glance, easy to test in a browser, and cache-friendly.

**Separate controller and model folders per version** - V1 and V2 controllers live in `Controllers/V1` and `Controllers/V2`. Models follow the same pattern. This mirrors production codebases where versions are isolated from each other - a change to V2 cannot accidentally break V1.

**Calculated fields in V2, not V1** - V1 returns raw data. V2 applies business logic server-side. This is the correct versioning pattern - rather than modifying an existing contract, a new version introduces the enriched shape while V1 remains stable and unchanged.

**CsvHelper over manual parsing** - The job search CSV contains multi-line quoted fields in the Notes column. A hand-rolled `ReadLine()` parser breaks on these. CsvHelper handles RFC 4180 compliant CSV correctly out of the box.

**Interface-based DI** - `CsvReaderService` is registered against `ICsvReaderService`. This decouples the controller from the implementation - swapping in a Google Sheets reader or a mock for testing requires no controller changes.

**ConcurrentDictionary for JobStore** - Background jobs run on a separate thread. `ConcurrentDictionary` ensures thread-safe reads and writes without manual locking.

---

## Author

**Michael Sargent**
Senior Software Engineer - 26 years production experience
C# - .NET - Azure - REST APIs - SQL Server

[linkedin.com/in/michaeljohnsargent](https://linkedin.com/in/michaeljohnsargent)