# ADR 0008: Rate Limiting

## Config-driven rate limiting limits

All rate limit values (`PermitLimit`, `WindowSeconds`, `TokenLimit`, etc.) are read from `appsettings.json` at startup with hard-coded fallbacks. Tuning a limit in production is a config change — no rebuild, no redeploy of binaries. This also makes integration tests straightforward: each test overrides the limit to 2 via in-memory configuration so the limit is hit with just 3 requests, without touching production values.

## Partition key falls back to IP for anonymous requests

The rate limiter partition key checks `HttpContext.User.Identity.Name` (user JWT) and then the `client_id` claim (OAuth client JWT). If neither is present (anonymous request), it falls back to the remote IP address with IPv6-mapped IPv4 normalized for consistent key strings. This gives each authenticated caller their own independent counter while still throttling unauthenticated abuse by IP. The fallback is placed in a local static function (`GetPartitionKey`) so all three policies share the same logic with no duplication.

## UseRouting called explicitly

Without an explicit `app.UseRouting()` call before `app.UseRateLimiter()`, ASP.NET Core 8 would implicitly insert routing at the `app.MapControllers()` call site — which comes *after* the rate limiter in the pipeline. The rate limiter would then run before endpoint metadata is resolved, making `[EnableRateLimiting]` attributes invisible to it and causing all requests to be treated as unrated. Adding the explicit call makes the ordering unambiguous and matches the ASP.NET Core documentation recommendation.

## Three policies, three endpoint types

A single global limiter would either be too strict for high-frequency data reads or too lenient for expensive AI calls. Three named policies match three cost profiles: auth endpoints get a tight fixed window to resist credential stuffing; general data endpoints get a sliding window to smooth out burst traffic while allowing sustained use; LLM endpoints get a token bucket that allows a small burst (5 tokens) but replenishes slowly (+2 per 30s), reflecting the actual cost of an inference call. `[EnableRateLimiting]` attributes are placed at the controller level (so all current and future actions inherit the policy) except on the token-issuance actions, where action-level placement leaves room for non-auth actions on those controllers later.
