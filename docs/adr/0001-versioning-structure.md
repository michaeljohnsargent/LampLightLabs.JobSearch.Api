# ADR 0001: Versioning Structure

## URL segment versioning

Version is embedded directly in the route path (`/api/v1/...`, `/api/v2/...`). This is the most explicit and widely adopted strategy for public APIs - visible at a glance, easy to test in a browser, and cache-friendly.

## Separate controller and model folders per version

V1 and V2 controllers live in `Controllers/V1` and `Controllers/V2`. Models follow the same pattern. This mirrors production codebases where versions are isolated from each other - a change to V2 cannot accidentally break V1.

## Calculated fields in V2, not V1

V1 returns raw data. V2 applies business logic server-side. This is the correct versioning pattern - rather than modifying an existing contract, a new version introduces the enriched shape while V1 remains stable and unchanged.

## OAuth token endpoint outside versioning

`POST /oauth/token` lives at the root, not under `/api/v1/` or `/api/v2/`. Auth infrastructure is not a versioned resource. Moving a token endpoint under a new version would break all existing clients - there is no good reason to version it. The RAG endpoint (`/api/rag/match`) follows the same principle for the same reason: it is infrastructure-level capability, not a versioned business resource.
