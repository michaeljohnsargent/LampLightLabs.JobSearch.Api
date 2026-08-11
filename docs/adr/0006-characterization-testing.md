# ADR 0006: Characterization Testing

## Characterization tests before refactoring

`StatusCategorizerService` was a private static method on `ApplicationsController` before extraction. Characterization tests were written against the extracted service before any logic was changed. One test (`Applied_ReturnsUnknown`) explicitly documents a gap in the current logic and freezes it as-is. The fix belongs in a subsequent PR after the safety net is in place - not during the characterization pass.
