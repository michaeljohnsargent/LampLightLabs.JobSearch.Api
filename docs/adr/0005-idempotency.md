# ADR 0005: Idempotency

## ConcurrentDictionary for IdempotencyService

Runs in singleton scope and handles concurrent requests. `ConcurrentDictionary` ensures thread-safe reads and writes without manual locking. Uses a composite key of `clientId:idempotencyKey` so two clients sending the same GUID do not collide.

## Idempotency-Key scoped to client identity

The idempotency store keys each entry to the authenticated client identity plus the caller-supplied key. A user JWT resolves to the `name` claim. A client credentials JWT resolves to the `client_id` claim. This prevents one client's key from shadowing another client's identical key.

## SHA-256 request fingerprinting

On first call the server hashes the serialized request body and stores it alongside the cached response. On retry, if the key matches but the hash does not, the server returns 422. This catches a client who reuses a key on a different payload - a bug that would otherwise be silently mishandled.
