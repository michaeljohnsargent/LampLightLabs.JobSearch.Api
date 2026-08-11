# ADR 0002: Auth Schemes

## Client Credentials over Authorization Code for the first OAuth exercise

Client Credentials is the most common OAuth flow in backend contract work. No browser, no redirect, no user session - a service authenticates and gets a token. Authorization Code is the right next step for user-delegated access scenarios.

## BearerAuthOperationFilter mirrors BasicAuthOperationFilter

Swagger doesn't automatically connect `[Authorize]` to a security scheme in the UI. The operation filters inspect method attributes at doc generation time and wire the correct padlock to the correct endpoints. Adding a new auth scheme means adding a new filter - the pattern is consistent and self-contained.
