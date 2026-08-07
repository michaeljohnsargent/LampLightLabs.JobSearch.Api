#!/usr/bin/env node
const { spawnSync } = require('child_process');
const path = require('path');

// Read-only sessions (e.g. claude-code-review.yml's PR review, which is explicitly forbidden
// from running the test suite or making code changes per its own prompt) set this so a
// pre-existing, unrelated test failure can't force the agent into a loop it has no way to
// resolve or exit — it isn't allowed to fix anything, and re-injecting the same failure after
// it has already finished and tried to stop just burns turns until --max-turns. Local dev
// sessions and any workflow that actually makes code changes (e.g. claude.yml's @claude
// mentions) don't set this, so the gate still applies where it matters.
if (process.env.SKIP_TEST_HOOK === 'true') {
  process.exit(0);
}

const projectDir = process.env.CLAUDE_PROJECT_DIR || process.cwd();

function run(cmd, args, cwd) {
  return spawnSync(cmd, args, { cwd, shell: true, encoding: 'utf-8' });
}

// Exclude the intentionally-failing race condition demo (see CLAUDE.md); it is not a regression.
const backend = run(
  'dotnet',
  ['test', '--nologo', '--filter', 'FullyQualifiedName!~Counter_WithoutLock_ProducesUnpredictableResults'],
  projectDir
);
if (backend.status !== 0) {
  process.stderr.write(
    'Backend tests failed (dotnet test):\n' + (backend.stdout || '').slice(-2000) + '\n'
  );
  process.exit(2);
}

const frontend = run('npm', ['test'], path.join(projectDir, 'client'));
if (frontend.status !== 0) {
  process.stderr.write(
    'Frontend tests failed (npm test in client/):\n' + (frontend.stdout || '').slice(-2000) + '\n'
  );
  process.exit(2);
}

process.exit(0);