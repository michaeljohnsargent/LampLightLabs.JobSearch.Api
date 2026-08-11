# ADR 0004: Async Job Pattern

## Strategy Pattern extended to job storage (IJobStore)

`JobStore` (in-memory, `ConcurrentDictionary`-backed, safe as a Singleton) and `EfJobStore` (EF Core/Postgres, registered Scoped) both implement `IJobStore`. `JobsController` depends only on the interface; `Program.cs` registers `EfJobStore` in production, and swapping back to the in-memory implementation is the same one-line change already established for `ICsvReaderService`. `EfJobStoreTests` includes a parity test against `JobStore`, same shape as the CSV/JSON proof test.

## DbContext registered Scoped, not Singleton or Transient

`JobSearchDbContext` is not thread-safe, so a Singleton instance shared across every concurrent request would corrupt tracked state or throw. Transient would hand out a fresh instance per injection site even within the same request, defeating change-tracking and opening redundant connections for what should be one unit of work. Scoped - one instance per request - is the only lifetime that matches how a request-scoped unit of work actually behaves.

## IServiceScopeFactory for fire-and-forget background work

`JobsController.StartJob` kicks off `ProcessApplicationsAsync` via `Task.Run` without awaiting it, so that work can outlive the HTTP request that started it. Reusing the controller's own request-scoped `IJobStore`/`ICsvReaderService` inside that background method would risk touching a `DbContext` already disposed once the request's scope ends. The background method instead creates its own scope via `IServiceScopeFactory` and resolves fresh instances from it - a lifetime tied to the background work itself, not to the request that kicked it off.

## Interface-based DI

All services are registered against interfaces. This decouples controllers from implementations and makes unit testing with Moq clean and straightforward.
