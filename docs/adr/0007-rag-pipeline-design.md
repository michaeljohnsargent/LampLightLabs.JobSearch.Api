# ADR 0007: RAG Pipeline Design

## BackgroundService for startup embedding

`ResumeVectorStoreService` implements both `IResumeVectorStoreService` and `BackgroundService`. The singleton instance is registered once and resolved as both, so the host starts it as a background service while DI injects it as the vector store interface. Resume chunks are embedded once at startup and held in memory — no re-embedding per request. A `TaskCompletionSource` signals when initialization is complete; any request that arrives before startup finishes awaits it rather than racing against an empty store.

## Manual cosine similarity over a vector store library

With five resume chunks and one query embedding, the lookup is O(n) over a tiny list. A dedicated vector store library would add a dependency with no runtime benefit at this scale. The math is four lines and fully transparent in the codebase. The pattern scales to a library (Azure AI Search, Qdrant, etc.) with a one-interface swap when the dataset grows.

## IPromptRepository separates prompt authoring from orchestration

`RagMatchService` asks the repository for a system prompt and a user message; it does not construct strings itself. This means prompt content can be reviewed and tested in isolation (`PromptRepositoryTests` has no mocks), swapped without touching the service, or evolved to load from files or a database without changing the caller.

## RetrievedContext injected in the service, not by the model

The chunks returned by the vector store are attached to the response in `RagMatchService`, not sourced from the LLM output. Claude is asked only for `matchScore`, `summary`, `strengths`, and `gaps`. This prevents hallucination in the `retrievedContext` field and keeps the record of which chunks were retrieved authoritative and deterministic.

## System prompt separated from user message

`IClaudeChatService` was extended with a `SendPromptAsync(string systemPrompt, string userMessage, CancellationToken ct)` overload that maps to the Anthropic API's separate `system` and `messages` parameters. The existing single-string overload is unchanged and all prior tests continue to pass. Mixing system instructions into the user message works, but the Anthropic API accepts them separately and Claude responds more reliably when the distinction is explicit.

## Two-layer input sanitization

Sanitizing a job description that a client pastes as raw text requires two separate passes at different points in the pipeline. `NewlineSanitizingMiddleware` handles what the JSON parser would reject before the controller is ever called: literal `\n` and `\r` characters inside a JSON string value are illegal per RFC 8259 — the parser returns a 400 long before model binding or controller logic runs. The middleware reads the raw body, replaces those characters with spaces, and rewrites the stream. `RagController` handles what is legal JSON but still dirty for LLM input: non-printable control characters (`\x00`–`\x1F`, excluding whitespace), excess whitespace runs, and blank line clusters. The sanitized value is re-validated after cleaning so a string composed entirely of control characters still returns 400. This separation is intentional: the middleware fixes a protocol-level violation; the controller fixes application-level hygiene. Mixing them in one place would either put JSON wire-format concerns in a controller or put application semantics in infrastructure.
