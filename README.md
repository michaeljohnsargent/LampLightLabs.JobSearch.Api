# LampLightLabs.JobSearch.Api

A personal ASP.NET Core Web API project built by **Michael Sargent** to dust off and sharpen API development skills following a career transition in early 2026.

---

## Table of Contents

- [What This Project Does](#what-this-project-does)
- [Endpoints](#endpoints)
- [Authentication](#authentication)
- [CORS and Rate Limiting](#cors-and-rate-limiting)
- [API Versioning](#api-versioning)
- [Async Job Pattern](#async-job-pattern---how-it-works)
- [RAG Pipeline](#rag-pipeline---how-it-works)
- [Frontend](#frontend)
- [Project Structure](#project-structure)
- [Tech Stack](#tech-stack)
- [Running Locally](#running-locally)
- [Key Design Decisions](#key-design-decisions)
- [Author](#author)

---

## What This Project Does

This project serves ten purposes:

**1. Job Search Pipeline Tracker**
The `ApplicationsController` reads a CSV file containing job applications and their current pipeline states, exposing that data via a REST endpoint. It uses CsvHelper to correctly handle quoted multi-line fields - a real-world parsing challenge solved during development.

**2. Async Long-Running Job Pattern (Exercise)**
The `JobsController` demonstrates a production-relevant API pattern: accepting a long-running request, returning a job ID immediately with `202 Accepted`, processing work asynchronously in the background, and exposing a polling endpoint the client can call to check job status. This pattern is common in compliance processing, batch operations, and file export workflows. Job records are persisted via EF Core against Postgres (`EfJobStore`), with the original in-memory implementation (`JobStore`) kept alongside it — both implement `IJobStore`, the same Strategy Pattern used for `ICsvReaderService` below, and swapping which one is registered in `Program.cs` is a one-line change. Moving to a Scoped `DbContext` surfaced a real DI-lifetime bug in the background task: it previously reused the controller's own injected store directly, which only worked because that store was Singleton. `JobsController` now injects `IServiceScopeFactory` and creates a fresh scope inside the background method, so fire-and-forget work gets its own Scoped instances instead of risking a disposed `DbContext` once the originating HTTP request's scope ends.

**3. Authentication Showcase**
Each endpoint in the project demonstrates a different authentication scheme: JWT Bearer, API Key, Basic Auth, and OAuth 2.0 Client Credentials. This mirrors real-world backend APIs where different consumers (users, services, integrations) require different auth patterns on different endpoints.

**4. Idempotency (Exercise)**
`POST /api/v2/applications` demonstrates idempotent write operations using a caller-supplied `Idempotency-Key` header and SHA-256 request fingerprinting. The server caches the response on first call and replays it on retry - no duplicate record is created. Key reuse on a different payload returns 422. This pattern is critical in payment processing, compliance workflows, and any distributed system where a request may fire more than once.

**5. Characterization Testing (Exercise)**
`StatusCategorizerService` was extracted from a private controller method and covered with characterization tests before any refactoring was attempted. Following Michael Feathers' approach from "Working Effectively with Legacy Code" - freeze what the code actually does, not what it should do, then refactor safely inside that safety net.

**6. Threading Concepts (Exercise)**
Two tests in `RaceConditionDemoTests` demonstrate race condition behavior and its fix. The broken version spins up two threads incrementing a shared counter without synchronization - at 1 million iterations the result is reliably short of 2 million, proving the lost update. The fixed version wraps the increment in a `lock` block with a shared lock object and produces exactly 2 million every time. `ProcessApplicationsAsync` in `JobsController` also demonstrates `CancellationToken` wired into a long-running async operation - the delay is cancellation-aware and the token is checked before CSV processing begins so the operation exits cleanly at a safe boundary.

**7. AI Integration (Exercise)**
`POST /api/v2/ai/chat` accepts a JSON body with a `prompt` field and forwards it to the Anthropic Claude API via the official Anthropic .NET SDK, returning Claude's text response. `IClaudeChatService` wraps the SDK call behind an interface, consistent with the interface-based DI pattern used throughout the project, which keeps the controller and its tests free of any direct dependency on the SDK. The API key is configured under `Anthropic:ApiKey` in `appsettings.json` and overridden locally via .NET user secrets - it is never committed.

**8. Semantic Kernel Integration (Exercise)**
`POST /api/v2/sk/chat` accepts a JSON body with a `prompt` field and forwards it to an OpenAI chat completion model through Microsoft Semantic Kernel's OpenAI connector, returning the model's text response. `ISemanticKernelChatService` builds and wraps the Semantic Kernel `Kernel` behind an interface - the same pattern used for `IClaudeChatService` - so the controller and its tests have no direct dependency on Semantic Kernel or OpenAI. This mirrors a real-world scenario where an application swaps or runs multiple LLM orchestration frameworks side by side. The API key is configured under `OpenAI:ApiKey` in `appsettings.json` and overridden locally via .NET user secrets - it is never committed.

**9. CORS and Rate Limiting (Exercise)**
A `ViteDev` CORS policy allows `http://localhost:5173` (the Vite dev server default) to make cross-origin requests, with any method and header permitted so the React frontend can send `Authorization`, `Content-Type`, and `Idempotency-Key` preflight checks without being blocked by the browser. Three named rate limiting policies use ASP.NET Core 8's built-in `AddRateLimiter` (no additional NuGet packages): a fixed window on token issuance endpoints to prevent credential stuffing, a sliding window on general data endpoints for smooth per-user throttling, and a token bucket on LLM inference endpoints to meter expensive AI calls. All three policies partition by authenticated user identity (`name` or `client_id` claim) with a fallback to remote IP for anonymous requests, so each caller gets an independent counter. Rejected requests return HTTP 429 with a `Retry-After` header. Limits are config-driven under `Cors` and `RateLimiting` in `appsettings.json`. The middleware order is intentional: `UseRouting` is called explicitly before `UseRateLimiter` so endpoint metadata (the `[EnableRateLimiting]` attributes) is resolved before the rate limiter runs; `UseCors` comes before `UseAuthentication` so browser preflight OPTIONS requests are not challenged for credentials; `UseRateLimiter` comes after `UseAuthorization` so `HttpContext.User` is populated when the partition key is computed.

**10. RAG Pipeline (Exercise)**
`POST /api/rag/match` accepts the full text of a job description and returns a structured match analysis against the resume: a match score (0–100), a 2–3 sentence narrative, identified strengths, gaps, and the specific resume chunks that informed the analysis. Input sanitization happens in two layers: `NewlineSanitizingMiddleware` runs first and replaces literal `\r\n`, `\r`, and `\n` characters in the raw JSON body with spaces before the JSON deserializer sees them (unescaped newlines inside a JSON string value are illegal per RFC 8259 and would cause a parse error before the request reaches any controller), then the controller strips non-printable control characters, normalizes any remaining line endings, collapses horizontal whitespace runs, and caps consecutive blank lines at two, so the LLM always receives clean input regardless of how the client serialized the text. At startup, `ResumeVectorStoreService` (a `BackgroundService`) splits the resume into sections, embeds each one using OpenAI's `text-embedding-3-small` model, and holds the vectors in memory. On each request, the sanitized job description is embedded, cosine similarity retrieves the top-3 most relevant resume sections, and those sections are injected into a structured prompt sent to Claude via the Anthropic API. Claude returns a raw JSON object; the service deserializes it and attaches `retrievedContext` from the vector store rather than trusting the model to report which chunks it used. `IPromptRepository` owns all prompt construction — system instructions and user message assembly are separated from orchestration logic so prompts can be reviewed, swapped, or tested independently. Requires `OpenAI:ApiKey` (embeddings) and `Anthropic:ApiKey` (generation) set in user secrets.

---

## Endpoints

### Auth
| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/v1/auth/token` | None | Accepts username/password, returns a signed JWT bearer token |
| POST | `/oauth/token` | None | Accepts client_id/client_secret (form), returns a client-scoped JWT (OAuth 2.0 Client Credentials) |

### Applications
| Method | Route | Version | Auth | Description |
|---|---|---|---|---|
| GET | `/api/v1/applications/fromcsv` | v1 | None | Returns raw job application records from the pipeline CSV |
| GET | `/api/v2/applications/fromcsv` | v2 | JWT Bearer | Returns enriched records with calculated pipeline intelligence fields |
| GET | `/api/v2/applications/status` | v2 | API Key | Returns lightweight pipeline status confirming the API is operational |
| GET | `/api/v2/applications/count` | v2 | Basic Auth | Returns the total count of applications in the pipeline CSV |
| GET | `/api/v2/applications/stats` | v2 | JWT Bearer (OAuth) | Returns aggregate pipeline statistics: totals, breakdowns, averages |
| POST | `/api/v2/applications` | v2 | JWT Bearer | Creates a new application record. Requires `Idempotency-Key` header. Replays cached response on retry. Returns 422 if key is reused with a different body. |

### Jobs
| Method | Route | Version | Auth | Description |
|---|---|---|---|---|
| POST | `/api/v1/jobs/start` | v1 | None | Starts a background job, returns job ID immediately |
| GET | `/api/v1/jobs/{jobId}/status` | v1 | None | Polls the status of a running or completed job |

> Job records are persisted via EF Core to Postgres (`EfJobStore`) in production. Requires a local Postgres instance and an applied migration — see **Running Locally**. Every other endpoint in this project works without Postgres running.

### AI
| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/v2/ai/chat` | None | Accepts `{ "prompt": "..." }` and returns Claude's response via the Anthropic .NET SDK |
| POST | `/api/v2/sk/chat` | None | Accepts `{ "prompt": "..." }` and returns an OpenAI model's response via Microsoft Semantic Kernel |

### RAG
| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/rag/match` | None | Accepts a job description and returns a structured resume match analysis: score (0–100), narrative summary, strengths, gaps, and the retrieved resume chunks |

**Request: POST /api/rag/match**
```json
{
  "jobDescription": "We are looking for a Senior .NET Engineer with Azure experience..."
}
```

> Requires `OpenAI:ApiKey` (embeddings via `text-embedding-3-small`) and `Anthropic:ApiKey` (generation via Claude) set in user secrets. See **Running Locally** for setup.

**Response:**
```json
{
  "matchScore": 82,
  "summary": "Strong fit for a senior .NET backend role. Candidate brings 26 years of C# and Azure experience directly aligned with the core requirements. Minor gaps in Kubernetes and container orchestration.",
  "strengths": [
    "26 years C#/.NET experience across enterprise and federal domains",
    "Azure DevOps, Azure Functions, and cloud-native architecture",
    "REST API design, SQL Server, and microservices patterns"
  ],
  "gaps": [
    "No explicit Kubernetes or container orchestration experience",
    "Limited frontend/React experience"
  ],
  "retrievedContext": [
    "RECENT EXPERIENCE section: Principal Engineer at ...",
    "TECHNICAL SKILLS section: C#, .NET 8, Azure, ...",
    "PROFESSIONAL SUMMARY section: ..."
  ]
}
```

---

## Authentication

This project demonstrates four authentication schemes, each protecting a different endpoint to illustrate real-world usage patterns.

### JWT Bearer
Standard user-facing auth. POST credentials to `/api/v1/auth/token`, receive a signed JWT, pass it as `Authorization: Bearer {token}` on protected requests.

- Protected endpoints: `GET /api/v2/applications/fromcsv`, `GET /api/v2/applications/stats`
- Token issued by `TokenService`, validated by ASP.NET Core JWT Bearer middleware
- Key, issuer, audience, and expiry configured in `appsettings.json` under `Jwt`

### API Key
Service-to-service auth where a shared secret is passed in the request header. No token issuance - the key is validated on every request.

- Protected endpoint: `GET /api/v2/applications/status`
- Implemented as `ApiKeyAuthAttribute` (IAuthorizationFilter) - keeps auth logic out of the controller
- Key configured in `appsettings.json` under `ApiKey`
- Header: `Authorization: ApiKey {key}`

### Basic Auth
Legacy/simple auth scheme. Credentials are Base64-encoded and passed in the Authorization header on every request.

- Protected endpoint: `GET /api/v2/applications/count`
- Implemented as `BasicAuthHandler` chained onto the existing JWT scheme without overwriting it
- Credentials configured in `appsettings.json` under `BasicAuth`
- Header: `Authorization: Basic {base64(username:password)}`

### OAuth 2.0 Client Credentials
Machine-to-machine auth. A client application authenticates with its client_id and client_secret to receive a JWT. No user context - the token represents the application, not a person.

- Protected endpoint: `GET /api/v2/applications/stats`
- Token endpoint: `POST /oauth/token` (outside versioning - auth infrastructure does not belong to a version)
- Request format: `application/x-www-form-urlencoded` per the OAuth 2.0 spec
- Clients configured in `appsettings.json` under `OAuthClients`
- Token claims: `sub` (clientId), `client_id`, `scope`, `jti`, `exp`

---

## CORS and Rate Limiting

### CORS

The `ViteDev` policy is registered via `AddCors` and applied globally with `UseCors("ViteDev")`. It permits only `http://localhost:5173` and allows any method and header.

Allowed origins are config-driven under `Cors:AllowedOrigins` in `appsettings.json`, so additional origins (e.g. a staging frontend) can be added without a code change.

### Rate Limiting

Three named policies are registered via `AddRateLimiter` using ASP.NET Core 8's built-in rate limiting — no additional packages required.

| Policy | Algorithm | Default limits | Applied to |
|---|---|---|---|
| `auth-fixed` | Fixed window | 10 req / 60s, no queue | `POST /api/v1/auth/token`, `POST /oauth/token` |
| `api-sliding` | Sliding window | 100 req / 60s, 4 segments, no queue | All Applications and Jobs endpoints |
| `ai-token-bucket` | Token bucket | Burst 5, +2 per 30s, queue 2 | `/api/v2/ai/chat`, `/api/v2/sk/chat`, `/api/rag/match` |

**Partition key:** Each policy partitions by authenticated user identity (`name` claim for user JWTs, `client_id` claim for client-credential JWTs). Anonymous requests fall back to remote IP. This means each caller gets an independent counter — a user hitting the limit does not affect any other user.

**Rejected responses:** HTTP 429 with a plain-text body and a `Retry-After` header set to the remaining window seconds.

**Configuration:** All limits live under `RateLimiting` in `appsettings.json` and are read at startup with in-code defaults as fallback. Tuning a limit is a config change with no rebuild required.

**Middleware order:**
```
UseRouting()        ← explicit; UseRateLimiter needs endpoint metadata resolved first
UseCors()           ← before auth; preflight OPTIONS must not require credentials
UseAuthentication()
UseAuthorization()
UseRateLimiter()    ← after auth; HttpContext.User is populated for partition key
MapControllers()
```

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
```
Client                          Server
|                               |
|-- POST /api/v1/jobs/start --->|  Returns 202 Accepted + jobId immediately
|<-- { jobId, status: Queued } -|  Background work starts (CancellationToken wired in)
|                               |
|-- GET /api/v1/jobs/{id}/status|  Poll #1
|<-- { status: Processing } ----|
|                               |
|-- GET /api/v1/jobs/{id}/status|  Poll #2 (after delay)
|<-- { status: Complete,        |
|      result: "Processed 29    |
|      applications" }          |
```

The background task accepts a `CancellationToken`. The 3-second delay is cancellation-aware (`Task.Delay(3000, cancellationToken)`), and the token is checked again before CSV processing begins. The operation exits cleanly at a safe boundary rather than being interrupted mid-work.

---

## RAG Pipeline - How It Works
```
Startup
  ResumeVectorStoreService (BackgroundService)
  └── Loads resume.txt, splits on ---
  └── Embeds each chunk via OpenAI text-embedding-3-small
  └── Stores (chunk, embedding[]) in memory
  └── Signals ready via TaskCompletionSource

Request: POST /api/rag/match { "jobDescription": "..." }
  │
  ├─ 1. GetRelevantChunksAsync(jobDescription, topK: 3)
  │       Embeds the job description
  │       Cosine similarity against all stored resume chunks
  │       Returns top-3 chunks
  │
  ├─ 2. IPromptRepository.BuildRagMatchUserMessage(jd, chunks)
  │       Assembles: Job Description + Resume Context
  │
  ├─ 3. IClaudeChatService.SendPromptAsync(systemPrompt, userMessage)
  │       System: "Return only raw JSON: matchScore, summary, strengths, gaps"
  │       Returns raw JSON string
  │
  ├─ 4. JsonSerializer.Deserialize → LlmMatchResult
  │
  └─ 5. Return RagMatchResponse
          matchScore, summary, strengths, gaps  ← from Claude
          retrievedContext                       ← from vector store (step 1)
```

---

## Frontend

The `client/` directory contains **Resume Match Analyzer**, a React + TypeScript + Vite single-page app that calls the real `POST /api/rag/match` endpoint documented above — it does not compute or fake anything client-side (demo mode, below, is the one deliberate exception).

### Deployment

The frontend deploys to **Azure Static Web Apps**, with a GitHub Actions workflow (`.github/workflows/azure-static-web-apps-blue-coast-05842a60f.yml`) that builds and deploys on every push to `main`. This is the same push-to-`main` CI/CD pattern the backend uses for its own deployment to Azure Web App (`.github/workflows/main_lamplightlabs-api.yml`) — both halves of this project ship the same way.

### Theme System

Five themes — **Professional**, **Bold**, **Minimal**, **Claude Dark**, and **Claude Light** — switchable via `ThemeSwitcher.tsx`. Selecting a theme sets a `data-theme` attribute on the document root, which drives a set of CSS custom properties (`--bg`, `--surface`, `--surface-raised`, `--border`, `--text`, `--muted`, `--accent`, `--accent-hover`, `--font`, `--radius`, plus semantic score/status colors) defined per-theme in `App.css`.

The Claude Dark/Claude Light pair was added 7/30/2026 as a deliberate homage to Claude's actual design language — the clay/terracotta accent (`#d97757`) is intentional, not a random color choice.

### Demo Mode

Three buttons — "See a strong match," "See a partial match," "See a weak match" — populate the same results UI with realistic hardcoded fixture data instead of calling the API. This is a deliberate cost-conscious design decision, not a limitation: it lets visitors see the app's full functionality, including all three score bands, without spending real API credits on every page view.

### Live URLs

| | URL |
|---|---|
| Frontend | [match.lamplightlabs.com](https://match.lamplightlabs.com) |
| API | [lamplightlabs-api.azurewebsites.net](https://lamplightlabs-api.azurewebsites.net) (Swagger at `/swagger`) |

---

## Project Structure

```
LampLightLabs.JobSearch.Api/
├── Authentication/
│   └── BasicAuthHandler.cs             - Custom auth handler chained onto JWT scheme
├── Attributes/
│   └── ApiKeyAuthAttribute.cs          - IAuthorizationFilter for API key validation
├── Data/
│   └── JobSearchDbContext.cs           - EF Core DbContext for JobRecord (Postgres via Npgsql), registered Scoped
├── Controllers/
│   ├── OAuthController.cs              - POST /oauth/token (outside versioning)
│   ├── RagController.cs                - POST /api/rag/match (outside versioning)
│   ├── V1/
│   │   ├── ApplicationsController.cs   - Returns raw CSV data (v1)
│   │   ├── AuthController.cs           - POST /api/v1/auth/token (JWT issuance)
│   │   └── JobsController.cs           - Async job pattern endpoints (v1)
│   └── V2/
│       ├── AiController.cs             - POST /api/v2/ai/chat (v2)
│       ├── ApplicationsController.cs   - Enriched data, status, count, stats, and idempotent POST endpoints (v2)
│       └── SemanticKernelController.cs - POST /api/v2/sk/chat (v2)
├── Filters/
│   ├── BasicAuthOperationFilter.cs     - Swagger padlock for Basic-protected endpoints
│   └── BearerAuthOperationFilter.cs    - Swagger padlock for Bearer-protected endpoints
├── Middleware/
│   └── NewlineSanitizingMiddleware.cs  - Replaces literal newlines in JSON bodies with spaces before deserialization
├── Models/
│   ├── Ai/
│   │   ├── AiChatRequest.cs            - Request body for POST /api/ai/chat (Prompt)
│   │   └── AiChatResponse.cs           - Response body for POST /api/ai/chat (Response)
│   ├── Auth/
│   │   ├── LoginRequest.cs             - Username/password input
│   │   ├── TokenResponse.cs            - JWT response wrapper
│   │   ├── OAuthTokenRequest.cs        - client_id, client_secret, grant_type, scope
│   │   └── OAuthTokenResponse.cs       - access_token, token_type, expires_in, scope
│   ├── Rag/
│   │   ├── RagMatchRequest.cs          - Request body: JobDescription string
│   │   └── RagMatchResponse.cs         - Response: MatchScore, Summary, Strengths, Gaps, RetrievedContext
│   ├── Sk/
│   │   ├── SkChatRequest.cs            - Request body for POST /api/sk/chat (Prompt)
│   │   └── SkChatResponse.cs           - Response body for POST /api/sk/chat (Response)
│   ├── V1/
│   │   └── ApplicationResponse.cs      - Raw CSV field mapping
│   └── V2/
│       ├── ApplicationRequest.cs       - POST request body for idempotent application creation
│       ├── ApplicationResponse.cs      - Adds DaysInPipeline, IsFollowUpToday, StatusCategory
│       └── ApplicationStatsResponse.cs - Pipeline aggregate statistics
├── ResumeData/
│   └── resume.txt                      - Resume split into sections by --- for RAG chunking
├── Services/
│   ├── IClaudeChatService.cs           - Claude chat interface (single-prompt and system+user overloads)
│   ├── ClaudeChatService.cs            - Anthropic .NET SDK implementation
│   ├── ISemanticKernelChatService.cs   - Semantic Kernel chat interface
│   ├── SemanticKernelChatService.cs    - Builds a Semantic Kernel Kernel with the OpenAI connector
│   ├── IPromptRepository.cs            - Prompt construction interface
│   ├── PromptRepository.cs             - System prompt and user message assembly for the RAG pipeline
│   ├── IResumeVectorStoreService.cs    - Vector store interface
│   ├── ResumeVectorStoreService.cs     - BackgroundService: embeds resume at startup, serves top-K chunks by cosine similarity
│   ├── IRagMatchService.cs             - RAG match orchestration interface
│   ├── RagMatchService.cs              - Retrieves chunks, builds prompt, calls Claude, parses JSON, injects RetrievedContext
│   ├── ICsvReaderService.cs            - Reader service interface (Strategy Pattern contract)
│   ├── CsvReaderService.cs             - CsvHelper implementation (default production reader)
│   ├── JsonReaderService.cs            - JSON implementation (Strategy Pattern alternative)
│   ├── ITokenService.cs                - Token generation interface
│   ├── TokenService.cs                 - JWT generation for users and OAuth clients
│   ├── IOAuthClientService.cs          - Client credential validation interface
│   ├── OAuthClientService.cs           - Validates client_id/secret against config
│   ├── IIdempotencyService.cs          - Idempotency store interface
│   ├── IdempotencyService.cs           - ConcurrentDictionary-backed idempotency store (singleton)
│   ├── IdempotencyCacheEntry.cs        - Cache entry: request hash, status code, response, timestamp
│   ├── IStatusCategorizerService.cs    - Status categorization interface
│   ├── StatusCategorizerService.cs     - Extracted from controller; covered by characterization tests
│   ├── IJobStore.cs                    - Job store interface (Strategy Pattern contract)
│   ├── JobStore.cs                     - Thread-safe in-memory implementation (Singleton-safe, ConcurrentDictionary-backed)
│   └── EfJobStore.cs                   - EF Core/Postgres implementation (Scoped, production default)
├── TestData/
│   └── applications.csv                - Live job search pipeline data
└── Program.cs                          - DI registration and middleware

LampLightLabs.JobSearch.Api.Tests/
├── AiControllerTests.cs                - 4 tests: prompt validation, response shaping, service passthrough (IClaudeChatService mocked)
├── AuthenticationTests.cs              - 13 tests: TokenService, AuthController, JWT integration
├── ApiKeyAuthTests.cs                  - API key auth tests
├── BasicAuthTests.cs                   - Basic auth tests
├── OAuthTests.cs                       - 14 tests: GenerateClientToken, OAuthClientService, OAuthController, stats integration
├── CsvReaderServiceTests.cs            - CSV parsing tests + 5 JsonReaderService tests including Strategy Pattern proof
├── JobStoreTests.cs                    - JobStore concurrency and state tests
├── EfJobStoreTests.cs                  - EfJobStore CRUD tests (EF Core InMemory provider) + Strategy Pattern parity test against JobStore
├── IdempotencyTests.cs                 - 4 integration tests: new key, replay, missing key, key reuse conflict
├── NewlineSanitizingMiddlewareTests.cs - 4 tests: \n, \r\n, \r replacement; non-JSON body passthrough (no ASP.NET hosting)
├── PromptRepositoryTests.cs            - 6 tests: system prompt content, user message assembly (no mocks)
├── RagControllerTests.cs               - 9 tests: validation, input sanitization (control chars, whitespace, newlines), response shaping, service passthrough (IRagMatchService mocked)
├── RagMatchServiceTests.cs             - 7 tests: JSON parsing, RetrievedContext injection, orchestration wiring (all dependencies mocked)
├── StatusCategorizerCharacterizationTests.cs - 13 characterization tests freezing current categorizer behavior
├── CorsTests.cs                        - 3 integration tests: allowed origin returns ACAO header, disallowed origin omits it, OPTIONS preflight returns 204
├── RateLimitingTests.cs                - 4 integration tests: fixed window 429 + Retry-After, sliding window 429, token bucket 429 (fresh factory per test for clean partition state)
├── RaceConditionDemoTests.cs           - 2 threading tests: race condition without lock (broken by design), race condition with lock (fixed)
└── SemanticKernelControllerTests.cs    - 4 tests: prompt validation, response shaping, service passthrough (ISemanticKernelChatService mocked)

Total: 122 tests (121 passing; RaceConditionDemoTests.Counter_WithoutLock_ProducesUnpredictableResults is intentionally broken by design)
```

---

## Tech Stack

- .NET 8.0
- ASP.NET Core Web API
- Asp.Versioning.Mvc 8.1.0
- CsvHelper
- Microsoft.AspNetCore.Authentication.JwtBearer 8.0.22
- Npgsql.EntityFrameworkCore.PostgreSQL 8.0.11 (EF Core / PostgreSQL, Jobs endpoints)
- Microsoft.EntityFrameworkCore.Design 8.0.11 (migration tooling, dev-time only)
- Anthropic .NET SDK (Claude API - chat and RAG generation)
- Microsoft Semantic Kernel 1.77.0 (OpenAI chat and text-embedding-3-small)
- xUnit v3
- Moq
- Swagger / Swashbuckle 6.9.0

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

**Optional - AI Integration:**
To call the AI endpoints or the RAG pipeline, set real API keys via user secrets (run from the `LampLightLabs.JobSearch.Api` directory):
```
dotnet user-secrets init
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
```
The embedding model (`text-embedding-3-small`) and chat model (`gpt-4o-mini`) are configured in `appsettings.json` under `OpenAI:EmbeddingModel` and `OpenAI:Model` — no secret required, change them there if needed. The placeholder API key values in `appsettings.json` are never used for live requests; they exist only to document the expected config shape.

**Optional - EF Core / Postgres (Jobs endpoints):**
`POST /api/v1/jobs/start` and `GET /api/v1/jobs/{jobId}/status` are backed by Postgres via EF Core (`EfJobStore`). Every other endpoint in this project works without it.
1. Have a local Postgres instance running. The dev default in `appsettings.Development.json` targets `Host=localhost;Port=5432;Database=lamplightlabs_jobsearch;Username=postgres;Password=postgres` - override via user secrets or edit the connection string directly for a different local setup.
2. Install the EF Core CLI tool once (global, one-time): `dotnet tool install --global dotnet-ef`
3. Generate the initial migration: `dotnet ef migrations add InitialCreate --project LampLightLabs.JobSearch.Api`
4. Apply it to create the schema: `dotnet ef database update --project LampLightLabs.JobSearch.Api`

**Running Tests:**
- Open Test Explorer (`Ctrl+E, T`)
- Click Run All

---

## Key Design Decisions

**URL segment versioning** - Version is embedded directly in the route path (`/api/v1/...`, `/api/v2/...`). This is the most explicit and widely adopted strategy for public APIs - visible at a glance, easy to test in a browser, and cache-friendly.

**Separate controller and model folders per version** - V1 and V2 controllers live in `Controllers/V1` and `Controllers/V2`. Models follow the same pattern. This mirrors production codebases where versions are isolated from each other - a change to V2 cannot accidentally break V1.

**Calculated fields in V2, not V1** - V1 returns raw data. V2 applies business logic server-side. This is the correct versioning pattern - rather than modifying an existing contract, a new version introduces the enriched shape while V1 remains stable and unchanged.

**OAuth token endpoint outside versioning** - `POST /oauth/token` lives at the root, not under `/api/v1/` or `/api/v2/`. Auth infrastructure is not a versioned resource. Moving a token endpoint under a new version would break all existing clients - there is no good reason to version it. The RAG endpoint (`/api/rag/match`) follows the same principle for the same reason: it is infrastructure-level capability, not a versioned business resource.

**Client Credentials over Authorization Code for the first OAuth exercise** - Client Credentials is the most common OAuth flow in backend contract work. No browser, no redirect, no user session - a service authenticates and gets a token. Authorization Code is the right next step for user-delegated access scenarios.

**BearerAuthOperationFilter mirrors BasicAuthOperationFilter** - Swagger doesn't automatically connect `[Authorize]` to a security scheme in the UI. The operation filters inspect method attributes at doc generation time and wire the correct padlock to the correct endpoints. Adding a new auth scheme means adding a new filter - the pattern is consistent and self-contained.

**CsvHelper over manual parsing** - The job search CSV contains multi-line quoted fields in the Notes column. A hand-rolled `ReadLine()` parser breaks on these. CsvHelper handles RFC 4180 compliant CSV correctly out of the box.

**Strategy Pattern (ICsvReaderService)** - `ICsvReaderService` defines the contract for reading structured data files. `CsvReaderService` and `JsonReaderService` are interchangeable implementations. Swapping one for the other requires changing a single line in `Program.cs` - the controller, job processor, and all callers remain untouched. This is the Strategy Pattern: same interface, swappable behavior, caller never knows the difference. A proof test in `CsvReaderServiceTests` verifies both implementations return identical results from the same data in different formats.

**Strategy Pattern extended to job storage (IJobStore)** - `JobStore` (in-memory, `ConcurrentDictionary`-backed, safe as a Singleton) and `EfJobStore` (EF Core/Postgres, registered Scoped) both implement `IJobStore`. `JobsController` depends only on the interface; `Program.cs` registers `EfJobStore` in production, and swapping back to the in-memory implementation is the same one-line change already established for `ICsvReaderService`. `EfJobStoreTests` includes a parity test against `JobStore`, same shape as the CSV/JSON proof test.

**DbContext registered Scoped, not Singleton or Transient** - `JobSearchDbContext` is not thread-safe, so a Singleton instance shared across every concurrent request would corrupt tracked state or throw. Transient would hand out a fresh instance per injection site even within the same request, defeating change-tracking and opening redundant connections for what should be one unit of work. Scoped - one instance per request - is the only lifetime that matches how a request-scoped unit of work actually behaves.

**IServiceScopeFactory for fire-and-forget background work** - `JobsController.StartJob` kicks off `ProcessApplicationsAsync` via `Task.Run` without awaiting it, so that work can outlive the HTTP request that started it. Reusing the controller's own request-scoped `IJobStore`/`ICsvReaderService` inside that background method would risk touching a `DbContext` already disposed once the request's scope ends. The background method instead creates its own scope via `IServiceScopeFactory` and resolves fresh instances from it - a lifetime tied to the background work itself, not to the request that kicked it off.

**Interface-based DI** - All services are registered against interfaces. This decouples controllers from implementations and makes unit testing with Moq clean and straightforward.

**ConcurrentDictionary for IdempotencyService** - Runs in singleton scope and handles concurrent requests. `ConcurrentDictionary` ensures thread-safe reads and writes without manual locking. Uses a composite key of `clientId:idempotencyKey` so two clients sending the same GUID do not collide.

**Idempotency-Key scoped to client identity** - The idempotency store keys each entry to the authenticated client identity plus the caller-supplied key. A user JWT resolves to the `name` claim. A client credentials JWT resolves to the `client_id` claim. This prevents one client's key from shadowing another client's identical key.

**SHA-256 request fingerprinting** - On first call the server hashes the serialized request body and stores it alongside the cached response. On retry, if the key matches but the hash does not, the server returns 422. This catches a client who reuses a key on a different payload - a bug that would otherwise be silently mishandled.

**Characterization tests before refactoring** - `StatusCategorizerService` was a private static method on `ApplicationsController` before extraction. Characterization tests were written against the extracted service before any logic was changed. One test (`Applied_ReturnsUnknown`) explicitly documents a gap in the current logic and freezes it as-is. The fix belongs in a subsequent PR after the safety net is in place - not during the characterization pass.

**BackgroundService for startup embedding** - `ResumeVectorStoreService` implements both `IResumeVectorStoreService` and `BackgroundService`. The singleton instance is registered once and resolved as both, so the host starts it as a background service while DI injects it as the vector store interface. Resume chunks are embedded once at startup and held in memory — no re-embedding per request. A `TaskCompletionSource` signals when initialization is complete; any request that arrives before startup finishes awaits it rather than racing against an empty store.

**Manual cosine similarity over a vector store library** - With five resume chunks and one query embedding, the lookup is O(n) over a tiny list. A dedicated vector store library would add a dependency with no runtime benefit at this scale. The math is four lines and fully transparent in the codebase. The pattern scales to a library (Azure AI Search, Qdrant, etc.) with a one-interface swap when the dataset grows.

**IPromptRepository separates prompt authoring from orchestration** - `RagMatchService` asks the repository for a system prompt and a user message; it does not construct strings itself. This means prompt content can be reviewed and tested in isolation (`PromptRepositoryTests` has no mocks), swapped without touching the service, or evolved to load from files or a database without changing the caller.

**RetrievedContext injected in the service, not by the model** - The chunks returned by the vector store are attached to the response in `RagMatchService`, not sourced from the LLM output. Claude is asked only for `matchScore`, `summary`, `strengths`, and `gaps`. This prevents hallucination in the `retrievedContext` field and keeps the record of which chunks were retrieved authoritative and deterministic.

**System prompt separated from user message** - `IClaudeChatService` was extended with a `SendPromptAsync(string systemPrompt, string userMessage, CancellationToken ct)` overload that maps to the Anthropic API's separate `system` and `messages` parameters. The existing single-string overload is unchanged and all prior tests continue to pass. Mixing system instructions into the user message works, but the Anthropic API accepts them separately and Claude responds more reliably when the distinction is explicit.

**Config-driven rate limiting limits** - All rate limit values (`PermitLimit`, `WindowSeconds`, `TokenLimit`, etc.) are read from `appsettings.json` at startup with hard-coded fallbacks. Tuning a limit in production is a config change — no rebuild, no redeploy of binaries. This also makes integration tests straightforward: each test overrides the limit to 2 via in-memory configuration so the limit is hit with just 3 requests, without touching production values.

**Partition key falls back to IP for anonymous requests** - The rate limiter partition key checks `HttpContext.User.Identity.Name` (user JWT) and then the `client_id` claim (OAuth client JWT). If neither is present (anonymous request), it falls back to the remote IP address with IPv6-mapped IPv4 normalized for consistent key strings. This gives each authenticated caller their own independent counter while still throttling unauthenticated abuse by IP. The fallback is placed in a local static function (`GetPartitionKey`) so all three policies share the same logic with no duplication.

**UseRouting called explicitly** - Without an explicit `app.UseRouting()` call before `app.UseRateLimiter()`, ASP.NET Core 8 would implicitly insert routing at the `app.MapControllers()` call site — which comes *after* the rate limiter in the pipeline. The rate limiter would then run before endpoint metadata is resolved, making `[EnableRateLimiting]` attributes invisible to it and causing all requests to be treated as unrated. Adding the explicit call makes the ordering unambiguous and matches the ASP.NET Core documentation recommendation.

**Three policies, three endpoint types** - A single global limiter would either be too strict for high-frequency data reads or too lenient for expensive AI calls. Three named policies match three cost profiles: auth endpoints get a tight fixed window to resist credential stuffing; general data endpoints get a sliding window to smooth out burst traffic while allowing sustained use; LLM endpoints get a token bucket that allows a small burst (5 tokens) but replenishes slowly (+2 per 30s), reflecting the actual cost of an inference call. `[EnableRateLimiting]` attributes are placed at the controller level (so all current and future actions inherit the policy) except on the token-issuance actions, where action-level placement leaves room for non-auth actions on those controllers later.

**Two-layer input sanitization** - Sanitizing a job description that a client pastes as raw text requires two separate passes at different points in the pipeline. `NewlineSanitizingMiddleware` handles what the JSON parser would reject before the controller is ever called: literal `\n` and `\r` characters inside a JSON string value are illegal per RFC 8259 — the parser returns a 400 long before model binding or controller logic runs. The middleware reads the raw body, replaces those characters with spaces, and rewrites the stream. `RagController` handles what is legal JSON but still dirty for LLM input: non-printable control characters (`\x00`–`\x1F`, excluding whitespace), excess whitespace runs, and blank line clusters. The sanitized value is re-validated after cleaning so a string composed entirely of control characters still returns 400. This separation is intentional: the middleware fixes a protocol-level violation; the controller fixes application-level hygiene. Mixing them in one place would either put JSON wire-format concerns in a controller or put application semantics in infrastructure.

**Vitest/RTL over a second, heavier test runner** - The `client/` React app had zero frontend test coverage before this. Vitest was chosen because it shares Vite's config and transform pipeline (no separate Babel/webpack setup to maintain) and is a drop-in Jest-API replacement, so React Testing Library patterns transfer directly. `vite.config.ts` adds a `test` block (`jsdom` environment, `globals: true`, `src/setupTests.ts` importing `@testing-library/jest-dom` matchers); `tsconfig.app.json` adds `vitest/globals` and `@testing-library/jest-dom` to `types` so `describe`/`it`/`expect`/`vi` and the custom matchers type-check without per-file imports. `npm test` runs the suite once (`vitest run`) for CI/hook use; `npm run test:watch` is the interactive loop. `Message.test.tsx` covers the first component: rendering via `UserContext.Provider` and the thrown error when rendered outside one.

**claude-code-review GitHub Action reviews every PR against this repo's own conventions** - `.github/workflows/claude-code-review.yml` runs `anthropics/claude-code-action` on every PR open/sync and posts a review comment. The prompt explicitly points it at this repo's `CLAUDE.md` — versioning structure, the auth schemes, the Strategy Pattern used for `ICsvReaderService`/`IJobStore`, the characterization-testing rule for `StatusCategorizerService`, and the async job/DI-scope pattern in `JobsController` — so review feedback is scoped to deviations from patterns already established here, not generic style nits. It's read-only (`Read,Grep,Glob` via `--allowedTools`) and comment-only: it cannot push commits. Requires an `ANTHROPIC_API_KEY` repository secret, added once in GitHub settings.

**Two Claude Code hooks scope what the agent can touch and when a turn is "done"** - `.claude/settings.json` wires up:
- A `PreToolUse` hook (`block-secrets.js`) on `Read`/`Edit`/`Write` that blocks any `appsettings.*.json` override (e.g. `appsettings.Development.json`) or `.env`/`.env.*` file by filename, regardless of directory. The base `appsettings.json` is exempted — it only ever holds placeholder values (see **AI Integration**, **Semantic Kernel Integration**) — so the agent can still read the shape of the config without a path to real secrets, which live only in user secrets or an environment-specific override file.
- A `Stop` hook (`run-tests.js`) that runs both suites — `dotnet test` and `npm test` in `client/` — before a turn is considered finished, so a broken build or a broken test can't silently pass as "done." The backend run passes `--filter "FullyQualifiedName!~Counter_WithoutLock_ProducesUnpredictableResults"` to exclude just that one method: it's the intentionally-broken half of the **Threading Concepts** exercise above, and without the filter the hook would report a failed turn on every single stop regardless of what changed, since that test is designed to never pass.

---

## Author

**Michael Sargent**
Senior Software Engineer - 26 years production experience
C# - .NET - Azure - REST APIs - SQL Server

[linkedin.com/in/michaeljohnsargent](https://linkedin.com/in/michaeljohnsargent)
