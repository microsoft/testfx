// Script that communicates with binlog-mcp via MCP stdio protocol to extract build errors.
// Uses the official @modelcontextprotocol/sdk for reliable protocol handling.

const { spawn } = require('child_process');
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

    // Extract source context around each error for fix suggestions
    const errorText = extractText(errors);
    let parsedErrors = [];
    try { parsedErrors = JSON.parse(errorText); } catch {}

    const fileContexts = {};
    const uniqueFiles = [...new Set(
      parsedErrors
        .filter(e => e.file && e.line)
        .map(e => ({ file: e.file, line: e.line }))
        .map(e => JSON.stringify(e))
    )].map(s => JSON.parse(s));

    // Fetch source context for each unique error location (max 5 files)
    for (const { file, line } of uniqueFiles.slice(0, 5)) {
      try {
        const startLine = Math.max(1, line - 5);
        const endLine = line + 10;
        const fileContent = await client.callTool({
          name: 'binlog_files',
          arguments: {
            binlog_file: absolutePath,
            filePath: file,
            startLine,
            endLine,
          },
        });
        const content = extractText(fileContent);
        if (content && !content.includes('not found in binlog')) {
          fileContexts[file] = { startLine, endLine, content, errorLine: line };
        }
      } catch (e) {
        // Skip files we can't retrieve
      }
    }

    const result = {
      overview: extractText(overview),
      errors: errorText,
      warnings: extractText(warnings),
      sourceContexts: fileContexts,
    };

    console.log(JSON.stringify(result, null, 2));
  } catch (e) {
    console.error(`Error: ${e.message}`);
    process.exit(1);
  } finally {
    try { await client.close(); } catch {}
    setTimeout(() => process.exit(0), 1000);
  }
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
