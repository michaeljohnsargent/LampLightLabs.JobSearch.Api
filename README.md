markdown# LampLightLabs.JobSearch.Api

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
| GET | `/api/v1/applications/fromcsv` | v1 | Returns all job applications read from the pipeline CSV |

### Jobs
| Method | Route | Version | Description |
|---|---|---|---|
| POST | `/api/v1/jobs/start` | v1 | Starts a background job, returns job ID immediately |
| GET | `/api/v1/jobs/{jobId}/status` | v1 | Polls the status of a running or completed job |

---

## API Versioning

This project uses URL segment versioning via the `Asp.Versioning.Mvc` library.

- Version is embedded in the URL path: `/api/v1/...`
- Unversioned requests default to v1
- Supported versions are reported in the `api-supported-versions` response header
- Swagger UI reflects the active version definition

This pattern mirrors production API design where multiple versions coexist without breaking existing consumers.

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
│   ├── ApplicationsController.cs   - CSV pipeline reader (v1)
│   └── JobsController.cs           - Async job pattern endpoints (v1)
├── Models/
│   └── JobRecord.cs                - Job state model and JobStatus enum
├── Services/
│   ├── ICsvReaderService.cs        - CSV reader interface
│   ├── CsvReaderService.cs         - CsvHelper implementation
│   └── JobStore.cs                 - Thread-safe in-memory job store
├── TestData/
│   └── applications.csv            - Live job search pipeline data
└── Program.cs                      - DI registration and middleware
LampLightLabs.JobSearch.Api.Tests/
├── CsvReaderServiceTests.cs        - 4 unit tests
└── JobStoreTests.cs                - 5 unit tests

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

**Running Tests:**
- Open Test Explorer (`Ctrl+E, T`)
- Click Run All

---

## Key Design Decisions

**URL segment versioning** - Version is embedded directly in the route path (`/api/v1/...`). This is the most explicit and widely adopted strategy for public APIs - visible at a glance, easy to test in a browser, and cache-friendly.

**CsvHelper over manual parsing** - The job search CSV contains multi-line quoted fields in the Notes column. A hand-rolled `ReadLine()` parser breaks on these. CsvHelper handles RFC 4180 compliant CSV correctly out of the box.

**Interface-based DI** - `CsvReaderService` is registered against `ICsvReaderService`. This decouples the controller from the implementation - swapping in a Google Sheets reader or a mock for testing requires no controller changes.

**ConcurrentDictionary for JobStore** - Background jobs run on a separate thread. `ConcurrentDictionary` ensures thread-safe reads and writes without manual locking.

---

## Author

**Michael Sargent**
Senior Software Engineer - 26 years production experience
C# - .NET - Azure - REST APIs - SQL Server

[linkedin.com/in/michaeljohnsargent](https://linkedin.com/in/michaeljohnsargent)