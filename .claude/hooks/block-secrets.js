#!/usr/bin/env node
// PreToolUse hook (Read|Edit|Write): blocks the agent from touching files that
// may hold real secrets (appsettings.*.json overrides, .env files). The base
// appsettings.json is fine - it only ever holds placeholder values.

let input = '';
process.stdin.on('data', (chunk) => (input += chunk));
process.stdin.on('end', () => {
  let payload;
  try {
    payload = JSON.parse(input.replace(/^﻿/, ''));
  } catch {
    process.exit(0);
  }

  const filePath = payload?.tool_input?.file_path || payload?.tool_input?.path || '';
  const fileName = filePath.split(/[\\/]/).pop() || '';

  const isEnvFile = /^\.env(\..+)?$/i.test(fileName);
  const isConfigOverride = /^appsettings\..+\.json$/i.test(fileName);

  if (isEnvFile || isConfigOverride) {
    process.stderr.write(
      `Blocked: ${payload.tool_name} on "${fileName}" is not allowed. ` +
        'appsettings.*.json overrides and .env files may hold real secrets and must not be read or edited by the agent.\n'
    );
    process.exit(2);
  }

  process.exit(0);
});
