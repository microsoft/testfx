// Analyzes build errors and generates a markdown report.
// Reads /tmp/binlog-analysis.json, optionally calls LLM, writes /tmp/analysis-result.md.

const fs = require('fs');
const path = require('path');

const workspace = process.env.GITHUB_WORKSPACE_PATH;
const headSha = process.env.PR_HEAD_SHA;
const ghToken = process.env.GH_TOKEN;
const serverUrl = process.env.GITHUB_SERVER_URL;
const repository = process.env.GITHUB_REPOSITORY;
const repoUrl = `${serverUrl}/${repository}`;

const data = JSON.parse(fs.readFileSync(process.env.BINLOG_DATA, 'utf8'));

// --- Helpers ---

function toRelPath(absPath) {
  const patterns = [
    workspace + '/', workspace + '\\',
    /^\/home\/runner\/work\/[^/]+\/[^/]+\//,
    /^D:\\a\\[^\\]+\\[^\\]+\\/,
  ];
  let rel = absPath;
  for (const p of patterns) {
    if (typeof p === 'string' && rel.startsWith(p)) { rel = rel.substring(p.length); break; }
    else if (p instanceof RegExp) { rel = rel.replace(p, ''); }
  }
  return rel.replace(/\\/g, '/');
}

function fileLink(absPath, line) {
  const rel = toRelPath(absPath);
  const url = `${repoUrl}/blob/${headSha}/${rel}`;
  return line ? `${url}#L${line}` : url;
}

function shortName(p) {
  return p.split(/[/\\]/).slice(-2).join('/');
}

// --- Parse errors ---

let parsedErrors = [];
try { parsedErrors = JSON.parse(data.errors || ''); } catch {}

// --- Read source context ---

const sourceContexts = {};
const seenFiles = new Set();
for (const err of parsedErrors) {
  if (!err.file || !err.line || seenFiles.has(err.file)) continue;
  seenFiles.add(err.file);
  try {
    let filePath = err.file;
    if (!fs.existsSync(filePath)) {
      const rel = toRelPath(filePath);
      filePath = path.join(workspace, rel);
    }
    if (fs.existsSync(filePath)) {
      const lines = fs.readFileSync(filePath, 'utf8').split('\n');
      const start = Math.max(0, err.line - 6);
      const end = Math.min(lines.length, err.line + 10);
      const contextLines = lines.slice(start, end)
        .map((l, i) => `${(start + i + 1).toString().padStart(4)} | ${l}`);
      sourceContexts[err.file] = { content: contextLines.join('\n'), errorLine: err.line };
    }
  } catch {}
}

// --- Build AI prompt ---

const prompt = [
  'You are an MSBuild build failure analyst for a .NET repository.',
  'Analyze the following build failure data and provide:',
  '1. A concise summary of what failed',
  '2. The root cause(s) — group related errors together',
  '3. For each root cause, provide a **concrete code fix** using a markdown diff block',
  '',
  'Keep your response concise and actionable. Use markdown formatting.',
  '', '## Build Overview', data.overview || 'N/A',
  '', '## Build Errors', data.errors || 'N/A',
];

if (Object.keys(sourceContexts).length > 0) {
  prompt.push('', '## Source Context');
  for (const [file, ctx] of Object.entries(sourceContexts)) {
    prompt.push('', `### ${shortName(file)} (around line ${ctx.errorLine})`, '```csharp', ctx.content.trim(), '```');
  }
}

if (data.warnings && data.warnings !== 'No data') {
  prompt.push('', '## Top Warnings', data.warnings);
}

// --- Call LLM ---

async function callLLM(promptText) {
  const endpoints = [
    'https://models.github.ai/inference/chat/completions',
    'https://models.inference.ai.azure.com/chat/completions',
  ];
  for (const endpoint of endpoints) {
    try {
      const response = await fetch(endpoint, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${ghToken}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({
          model: 'gpt-4o-mini',
          messages: [{ role: 'user', content: promptText }],
          max_tokens: 2000,
        }),
      });
      if (response.ok) {
        const result = await response.json();
        return result.choices?.[0]?.message?.content || null;
      }
      console.error(`${endpoint}: ${response.status}`);
    } catch (e) {
      console.error(`${endpoint}: ${e.message}`);
    }
  }
  return null;
}

// --- Linkify file references in AI output ---

function linkifyFileReferences(text) {
  const fileLinks = {};
  for (const err of parsedErrors) {
    if (!err.file) continue;
    const basename = err.file.split(/[/\\]/).pop();
    if (!fileLinks[basename]) {
      fileLinks[basename] = { urlNoLine: fileLink(err.file), fullPath: err.file };
    }
  }
  for (const [basename, info] of Object.entries(fileLinks)) {
    const escaped = basename.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    text = text.replace(
      new RegExp('(?<!\\[)`(' + escaped + ')`(?!\\])', 'g'),
      `[\`${basename}\`](${info.urlNoLine})`
    );
    text = text.replace(
      new RegExp('line (\\d+) of `?' + escaped + '`?', 'g'),
      (_, lineNum) => `[line ${lineNum} of \`${basename}\`](${fileLink(info.fullPath, parseInt(lineNum))})`
    );
  }
  return text;
}

// --- Structured fallback (no AI) ---

function buildFallbackAnalysis() {
  const parts = [];
  const overviewText = data.overview || '';
  if (overviewText) parts.push('### 📊 Build Overview', '', overviewText.trim());

  const realErrors = parsedErrors.filter(e => e.code);
  if (realErrors.length > 0) {
    const byFile = {};
    for (const e of realErrors) {
      const key = e.file || 'unknown';
      if (!byFile[key]) byFile[key] = [];
      byFile[key].push(e);
    }
    parts.push('', '### ❌ Errors', '');
    for (const [fullPath, errors] of Object.entries(byFile)) {
      parts.push(`**[\`${shortName(fullPath)}\`](${fileLink(fullPath)})**`);
      const unique = [...new Map(errors.map(e => [`${e.code}:${e.message}`, e])).values()];
      for (const e of unique) {
        const loc = e.line ? `[\`${e.code}\` (line ${e.line})](${fileLink(fullPath, e.line)})` : `\`${e.code}\``;
        parts.push(`- ${loc}: ${e.message}`);
      }
      parts.push('');
    }
  } else {
    parts.push('', '### ❌ Errors', '', '```', data.errors || 'No errors captured', '```');
  }

  // Source context with fix hints
  if (Object.keys(sourceContexts).length > 0 && realErrors.length > 0) {
    parts.push('### 🔧 Source Context', '');
    for (const [fullPath, ctx] of Object.entries(sourceContexts)) {
      const fileErrors = realErrors.filter(e => e.file === fullPath);
      if (fileErrors.length === 0) continue;
      const errLink = fileLink(fullPath, ctx.errorLine);
      parts.push(`**[\`${shortName(fullPath)}\`](${errLink})**`);
      parts.push('<details><summary>Click to expand</summary>', '', '```csharp', ctx.content.trim(), '```', '</details>', '');
    }
  }

  return parts.join('\n').substring(0, 15000);
}

// --- Main ---

async function main() {
  let analysis = await callLLM(prompt.join('\n'));
  if (analysis) {
    analysis = linkifyFileReferences(analysis);
  } else {
    analysis = buildFallbackAnalysis();
  }
  fs.writeFileSync('/tmp/analysis-result.md', analysis);
  console.log('Analysis written to /tmp/analysis-result.md');
}

main().catch(e => {
  console.error(e);
  process.exitCode = 1;
});
