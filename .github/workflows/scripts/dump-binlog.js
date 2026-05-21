// Dumps overview / errors / warnings from a `*.binlog` file as JSON, by
// speaking the MCP stdio protocol to the locally-installed `binlog-mcp`
// dotnet global tool. Used by the build-failure-analysis agentic workflows
// as a pre-agent step, because the gh-aw MCP gateway does not support
// non-containerized stdio MCP servers — running this script ahead of the
// agent gets us the same data via plain JSON files the agent can `cat`.
//
// Usage:
//   node dump-binlog.js <binlog-path> <output-dir>
//
// Writes (best-effort — missing tools or parse failures are tolerated):
//   <output-dir>/binlog-overview.json
//   <output-dir>/binlog-errors.json
//   <output-dir>/binlog-warnings.json
//
// Exit code:
//   0 on success or partial success (agent can still work from what it got)
//   1 if the MCP client could not start at all
//
// Keep this script intentionally small. All interpretation / fix proposal
// lives in the `build-failure-analyst` agent — this is plumbing only.

'use strict';

const fs = require('fs');
const path = require('path');

const binlogPath = process.argv[2];
const outputDir = process.argv[3] || '/tmp';

if (!binlogPath) {
  console.error('Usage: node dump-binlog.js <binlog-path> [<output-dir>]');
  process.exit(1);
}

if (!fs.existsSync(binlogPath)) {
  console.error(`Binlog not found: ${binlogPath}`);
  process.exit(1);
}

const absoluteBinlog = path.resolve(binlogPath);
fs.mkdirSync(outputDir, { recursive: true });

function write(file, value) {
  const target = path.join(outputDir, file);
  fs.writeFileSync(target, JSON.stringify(value, null, 2));
  console.error(`wrote ${target}`);
}

function extractText(response) {
  if (!response?.content) {
    return null;
  }
  return response.content
    .filter((c) => c.type === 'text')
    .map((c) => c.text)
    .join('\n');
}

function tryParseJson(text) {
  if (text === null || text === undefined) {
    return null;
  }
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

async function callTool(client, name, args) {
  try {
    const response = await client.callTool({ name, arguments: args });
    return tryParseJson(extractText(response));
  } catch (e) {
    console.error(`tool ${name} failed: ${e?.message ?? e}`);
    return null;
  }
}

async function main() {
  let client;
  try {
    const { Client } = await import('@modelcontextprotocol/sdk/client/index.js');
    const { StdioClientTransport } = await import('@modelcontextprotocol/sdk/client/stdio.js');

    const transport = new StdioClientTransport({ command: 'binlog-mcp', args: [] });
    client = new Client({ name: 'dump-binlog', version: '1.0.0' });
    await client.connect(transport);

    const overview = await callTool(client, 'binlog_overview', { binlog_file: absoluteBinlog });
    const errors = await callTool(client, 'binlog_errors', { binlog_file: absoluteBinlog });
    const warnings = await callTool(client, 'binlog_warnings', { binlog_file: absoluteBinlog, top: 10 });

    write('binlog-overview.json', overview ?? { error: 'binlog_overview failed' });
    write('binlog-errors.json', errors ?? { error: 'binlog_errors failed' });
    write('binlog-warnings.json', warnings ?? { error: 'binlog_warnings failed' });
  } catch (e) {
    console.error(`fatal: ${e?.stack ?? e?.message ?? e}`);
    process.exitCode = 1;
  } finally {
    try {
      if (client) {
        await client.close();
      }
    } catch {
      /* ignore */
    }
  }
}

main();
