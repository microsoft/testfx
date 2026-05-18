// Script that communicates with binlog-mcp via MCP stdio protocol to extract build errors.
// Uses raw JSON-RPC over stdin/stdout — no npm dependencies required.

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

// Spawn the binlog-mcp server with pre-loaded binlog
const server = spawn('binlog-mcp', ['--binlog', absolutePath], {
  stdio: ['pipe', 'pipe', 'pipe'],
});

let buffer = '';
let responseResolve = null;
let msgId = 0;

server.stdout.on('data', (chunk) => {
  buffer += chunk.toString();
  // MCP uses Content-Length header framing
  while (true) {
    const headerEnd = buffer.indexOf('\r\n\r\n');
    if (headerEnd === -1) break;

    const header = buffer.substring(0, headerEnd);
    const match = header.match(/Content-Length:\s*(\d+)/i);
    if (!match) break;

    const contentLength = parseInt(match[1], 10);
    const bodyStart = headerEnd + 4;
    if (buffer.length < bodyStart + contentLength) break;

    const body = buffer.substring(bodyStart, bodyStart + contentLength);
    buffer = buffer.substring(bodyStart + contentLength);

    try {
      const msg = JSON.parse(body);
      if (responseResolve && msg.id !== undefined) {
        responseResolve(msg);
        responseResolve = null;
      }
    } catch (e) {
      // ignore parse errors from notifications
    }
  }
});

server.stderr.on('data', (chunk) => {
  // Suppress stderr (logging)
});

function sendRequest(method, params) {
  return new Promise((resolve, reject) => {
    const id = ++msgId;
    const request = JSON.stringify({ jsonrpc: '2.0', id, method, params });
    const message = `Content-Length: ${Buffer.byteLength(request)}\r\n\r\n${request}`;
    responseResolve = resolve;
    server.stdin.write(message);
    setTimeout(() => reject(new Error(`Timeout waiting for response to ${method}`)), 30000);
  });
}

async function main() {
  try {
    // Initialize MCP connection
    await sendRequest('initialize', {
      protocolVersion: '2024-11-05',
      capabilities: {},
      clientInfo: { name: 'binlog-analyzer', version: '1.0.0' },
    });

    // Send initialized notification
    const initialized = JSON.stringify({ jsonrpc: '2.0', method: 'notifications/initialized' });
    server.stdin.write(`Content-Length: ${Buffer.byteLength(initialized)}\r\n\r\n${initialized}`);

    // Wait a moment for the server to be ready
    await new Promise(r => setTimeout(r, 2000));

    // Get build overview
    const overview = await sendRequest('tools/call', {
      name: 'binlog_overview',
      arguments: { binlog: absolutePath },
    });

    // Get build errors
    const errors = await sendRequest('tools/call', {
      name: 'binlog_errors',
      arguments: { binlog: absolutePath },
    });

    // Get build warnings (just the count — limit output)
    const warnings = await sendRequest('tools/call', {
      name: 'binlog_warnings',
      arguments: { binlog: absolutePath, top: 10 },
    });

    const result = {
      overview: extractText(overview),
      errors: extractText(errors),
      warnings: extractText(warnings),
    };

    console.log(JSON.stringify(result, null, 2));
  } catch (e) {
    console.error('Error:', e.message);
    process.exit(1);
  } finally {
    server.kill();
  }
}

function extractText(response) {
  if (response?.result?.content) {
    return response.result.content
      .filter(c => c.type === 'text')
      .map(c => c.text)
      .join('\n');
  }
  if (response?.error) {
    return `Error: ${response.error.message}`;
  }
  return 'No data';
}

main();
