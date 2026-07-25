#!/usr/bin/env node
const { spawnSync } = require('child_process');
const path = require('path');

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