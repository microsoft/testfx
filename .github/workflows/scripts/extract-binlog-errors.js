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

// Spawn the binlog-mcp server — do NOT pre-load binlog at startup to avoid timeout
const server = spawn('binlog-mcp', [], {
  stdio: ['pipe', 'pipe', 'pipe'],
});

let stderrLog = '';
server.stderr.on('data', (chunk) => {
  stderrLog += chunk.toString();
});

server.on('error', (err) => {
  console.error(`Failed to start binlog-mcp: ${err.message}`);
  process.exit(1);
});

let buffer = '';
let pendingResolves = new Map();
let msgId = 0;

server.stdout.on('data', (chunk) => {
  buffer += chunk.toString();
  while (true) {
    const headerEnd = buffer.indexOf('\r\n\r\n');
    if (headerEnd === -1) break;

    const header = buffer.substring(0, headerEnd);
    const match = header.match(/Content-Length:\s*(\d+)/i);
    if (!match) {
      // Skip malformed data
      buffer = buffer.substring(headerEnd + 4);
      continue;
    }

    const contentLength = parseInt(match[1], 10);
    const bodyStart = headerEnd + 4;
    if (buffer.length < bodyStart + contentLength) break;

    const body = buffer.substring(bodyStart, bodyStart + contentLength);
    buffer = buffer.substring(bodyStart + contentLength);

    try {
      const msg = JSON.parse(body);
      if (msg.id !== undefined && pendingResolves.has(msg.id)) {
        pendingResolves.get(msg.id)(msg);
        pendingResolves.delete(msg.id);
      }
    } catch (e) {
      // ignore parse errors from notifications
    }
  }
});

function sendRequest(method, params, timeoutMs = 60000) {
  return new Promise((resolve, reject) => {
    const id = ++msgId;
    const request = JSON.stringify({ jsonrpc: '2.0', id, method, params });
    const message = `Content-Length: ${Buffer.byteLength(request)}\r\n\r\n${request}`;
    const timer = setTimeout(() => {
      pendingResolves.delete(id);
      reject(new Error(`Timeout (${timeoutMs}ms) waiting for response to ${method}`));
    }, timeoutMs);
    pendingResolves.set(id, (msg) => {
      clearTimeout(timer);
      resolve(msg);
    });
    server.stdin.write(message);
  });
}

function sendNotification(method, params) {
  const notification = JSON.stringify({ jsonrpc: '2.0', method, params });
  server.stdin.write(`Content-Length: ${Buffer.byteLength(notification)}\r\n\r\n${notification}`);
}

async function main() {
  try {
    // Initialize MCP connection
    const initResult = await sendRequest('initialize', {
      protocolVersion: '2024-11-05',
      capabilities: {},
      clientInfo: { name: 'binlog-analyzer', version: '1.0.0' },
    }, 15000);

    if (initResult.error) {
      throw new Error(`Initialize failed: ${JSON.stringify(initResult.error)}`);
    }

    // Send initialized notification
    sendNotification('notifications/initialized');

    // Small delay for server readiness
    await new Promise(r => setTimeout(r, 500));

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

    // Get build warnings (limited)
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
    console.error(`Error: ${e.message}`);
    if (stderrLog) {
      console.error(`Server stderr: ${stderrLog.substring(0, 500)}`);
    }
    process.exit(1);
  } finally {
    server.kill();
    // Give it a moment to exit
    setTimeout(() => process.exit(0), 500);
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
