# ADR 0013: Frontend Test Tooling

## Vitest/RTL over a second, heavier test runner

The `client/` React app had zero frontend test coverage before this. Vitest was chosen because it shares Vite's config and transform pipeline (no separate Babel/webpack setup to maintain) and is a drop-in Jest-API replacement, so React Testing Library patterns transfer directly. `vite.config.ts` adds a `test` block (`jsdom` environment, `globals: true`, `src/setupTests.ts` importing `@testing-library/jest-dom` matchers); `tsconfig.app.json` adds `vitest/globals` and `@testing-library/jest-dom` to `types` so `describe`/`it`/`expect`/`vi` and the custom matchers type-check without per-file imports. `npm test` runs the suite once (`vitest run`) for CI/hook use; `npm run test:watch` is the interactive loop. `Message.test.tsx` covers the first component: rendering via `UserContext.Provider` and the thrown error when rendered outside one.
