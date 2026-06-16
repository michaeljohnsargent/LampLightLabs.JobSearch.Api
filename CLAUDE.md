# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A personal ASP.NET Core 8 Web API used to practice production-relevant backend patterns: API versioning, multiple auth schemes, idempotent writes, async long-running jobs, and characterization testing. See `README.md` for the full endpoint list, auth details, and the rationale behind each design decision ("Key Design Decisions" section) — it is kept current and is the canonical reference; don't duplicate it here.

## Commands

```
dotnet build LampLightLabs.JobSearch.Api.sln          # build solution
dotnet run --project LampLightLabs.JobSearch.Api      # run API (Swagger at /swagger in Development)
dotnet test                                            # run all tests (75 passing)
dotnet test --filter "FullyQualifiedName~ClassName"           # run one test class
dotnet test --filter "FullyQualifiedName~ClassName.MethodName" # run one test method
```

There is no separate lint/format command configured; rely on the IDE/compiler warnings (`Nullable` and `ImplicitUsings` are enabled in both projects).

## Architecture

**Versioning is structural, not just routing.** `Controllers/V1` and `Controllers/V2`, and `Models/V1` and `Models/V2`, are fully separate folders/types — a V2 change cannot accidentally affect V1. Version comes from the URL segment (`/api/v1/...`, `/api/v2/...`) via `Asp.Versioning.Mvc`, configured in `Program.cs`. V1 returns raw CSV fields; V2 adds computed fields (`DaysInPipeline`, `IsFollowUpToday`, `StatusCategory`) via `StatusCategorizerService`. The OAuth token endpoint (`OAuthController`, `POST /oauth/token`) deliberately sits outside `/api/v{n}/` since auth infrastructure isn't a versioned resource.

**Four auth schemes, each wired differently, on different endpoints** (see README "Authentication" section for the full mapping):
- JWT Bearer — standard `AddJwtBearer`, issued by `TokenService` from `POST /api/v1/auth/token`.
- API Key — `Attributes/ApiKeyAuthAttribute.cs`, an `IAuthorizationFilter` (not a scheme), checked per-request, no token issuance.
- Basic Auth — `Authentication/BasicAuthHandler.cs`, registered as an additional scheme (`"Basic"`) alongside JWT, not replacing it.
- OAuth 2.0 Client Credentials — `OAuthClientService` validates `client_id`/`client_secret` from config, `TokenService` issues a client-scoped JWT (no user context, `client_id` claim instead of `name`).

Swagger doesn't auto-detect `[Authorize]` for the UI padlock; `Filters/BasicAuthOperationFilter.cs` and `Filters/BearerAuthOperationFilter.cs` inspect attributes at doc-generation time to wire the correct scheme per endpoint. Adding a new auth scheme means adding a matching filter.

**Idempotency** (`POST /api/v2/applications`): `IdempotencyService` is a singleton backed by `ConcurrentDictionary`, keyed by `clientId:idempotencyKey` (clientId from the JWT `name` or `client_id` claim — so two different clients reusing the same key don't collide). On first call it stores a SHA-256 hash of the request body alongside the response; on replay with the same key it returns the cached response, and on replay with the same key but a different body hash it returns 422.

**Strategy Pattern for file reading**: `ICsvReaderService` is the contract; `CsvReaderService` (production, handles RFC 4180 multi-line quoted fields via CsvHelper) and `JsonReaderService` (alternative) are interchangeable. Swapping implementations is a one-line change in `Program.cs`'s DI registration — controllers and callers are untouched. `CsvReaderServiceTests` has a proof test asserting both implementations return identical results from equivalent data.

**Async job pattern** (`Controllers/V1/JobsController.cs` + `Services/JobStore.cs`): `POST /api/v1/jobs/start` returns `202 Accepted` with a job ID immediately; work runs in the background and `GET /api/v1/jobs/{jobId}/status` polls for completion. `JobStore` is a singleton `ConcurrentDictionary` for thread-safe state. The background task takes a `CancellationToken`, used in a cancellation-aware `Task.Delay` and checked again before CSV processing starts, so cancellation lands at a safe boundary rather than mid-work.

**Characterization testing**: `StatusCategorizerService` was extracted from a private controller method and pinned with characterization tests (`StatusCategorizerCharacterizationTests`) *before* any refactor — tests freeze current behavior (including at least one known logic gap, documented inline) rather than intended behavior, per Michael Feathers' approach. Don't "fix" frozen behavior as a side effect of unrelated changes; that gap is intentionally deferred to its own PR.

**Config**: `Jwt`, `ApiKey`, `BasicAuth`, and `OAuthClients` sections live in `appsettings.json` (and `appsettings.Development.json`). All services are registered against interfaces in `Program.cs` (`AddScoped`/`AddSingleton`), making Moq-based unit testing straightforward.

## Azure tooling

When a task involves Azure, use the available Azure tools, and call `azmcp_bestpractices_get` first if it's available. If it isn't available, ask the user to enable it before proceeding.
