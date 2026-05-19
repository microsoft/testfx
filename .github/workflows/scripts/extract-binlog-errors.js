// Script that communicates with binlog-mcp via MCP stdio protocol to extract build errors.
// Uses the official @modelcontextprotocol/sdk for reliable protocol handling.

const path = require('path');
const fs = require('fs');

const binlogPath = process.argv[2];
if (!binlogPath) {
  console.error('Usage: node extract-binlog-errors.js <binlog-path>');
  process.exit(1);
}

if (!fs.existsSync(binlogPath)) {
  console.error(`Binlog not found: ${binlogPath}`);
  process.exit(1);
}

const absolutePath = path.resolve(binlogPath);

async function main() {
  const { Client } = await import('@modelcontextprotocol/sdk/client/index.js');
  const { StdioClientTransport } = await import('@modelcontextprotocol/sdk/client/stdio.js');

  const transport = new StdioClientTransport({
    command: 'binlog-mcp',
    args: [],
  });

  const client = new Client({ name: 'binlog-analyzer', version: '1.0.0' });
  let failed = false;

  try {
    await client.connect(transport);

    // Get build overview
    const overview = await client.callTool({
      name: 'binlog_overview',
      arguments: { binlog_file: absolutePath },
    });

    // Get build errors
    const errors = await client.callTool({
      name: 'binlog_errors',
      arguments: { binlog_file: absolutePath },
    });

    // Get build warnings (limited)
    const warnings = await client.callTool({
      name: 'binlog_warnings',
      arguments: { binlog_file: absolutePath, top: 10 },
    });

    const result = {
      overview: extractText(overview),
      errors: extractText(errors),
      warnings: extractText(warnings),
    };

    console.log(JSON.stringify(result, null, 2));
  } catch (e) {
    const errorMessage = e instanceof Error ? (e.stack ?? e.message) : String(e);
    console.error(`Error: ${errorMessage}`);
    failed = true;
  } finally {
    try { await client.close(); } catch {}
  }

  process.exitCode = failed ? 1 : 0;
}

function extractText(response) {
  if (response?.content) {
    return response.content
      .filter(c => c.type === 'text')
      .map(c => c.text)
      .join('\n');
  }
  return 'No data';
}

main();
