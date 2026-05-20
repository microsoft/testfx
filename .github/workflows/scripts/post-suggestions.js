// Posts LLM-generated inline fix suggestions as GitHub PR review comments
// with "Commit suggestion" buttons.

module.exports = async ({ github, context, core }) => {
  const fs = require('fs');
  const path = require('path');
  const workspace = process.env.GITHUB_WORKSPACE_PATH;
  const ghToken = process.env.GH_TOKEN;

  let data;
  try {
    data = JSON.parse(fs.readFileSync(process.env.BINLOG_DATA, 'utf8'));
  } catch { return; }

  let parsedErrors = [];
  try { parsedErrors = JSON.parse(data.errors || ''); } catch {}
  if (parsedErrors.length === 0) return;

  const prNumber = context.payload.pull_request.number;
  const headSha = context.payload.pull_request.head.sha;

  // Get PR diff info
  const prFiles = await github.paginate(
    github.rest.pulls.listFiles,
    { owner: context.repo.owner, repo: context.repo.repo, pull_number: prNumber, per_page: 100 },
  );
  const prFilePaths = new Set(prFiles.map(f => f.filename));

  // Build set of lines in the PR diff (GitHub rejects suggestions on non-diff lines)
  const diffLines = new Set();
  for (const f of prFiles) {
    if (!f.patch) continue;
    const hunkRegex = /^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@/gm;
    let match;
    while ((match = hunkRegex.exec(f.patch)) !== null) {
      const start = parseInt(match[1]);
      const count = parseInt(match[2] || '1');
      for (let i = start; i < start + count; i++) {
        diffLines.add(`${f.filename}:${i}`);
      }
    }
  }

  function toRelPath(absPath) {
    const patterns = [workspace + '/', workspace + '\\', /^\/home\/runner\/work\/[^/]+\/[^/]+\//, /^D:\\a\\[^\\]+\\[^\\]+\\/];
    let rel = absPath;
    for (const p of patterns) {
      if (typeof p === 'string' && rel.startsWith(p)) { rel = rel.substring(p.length); break; }
      else if (p instanceof RegExp) { rel = rel.replace(p, ''); }
    }
    return rel.replace(/\\/g, '/');
  }

  // Collect suggestion candidates: errors on diff lines (direct or declaration-side)
  const candidates = [];

  for (const err of parsedErrors) {
    if (!err.file || !err.line || !err.code) continue;
    const relPath = toRelPath(err.file);
    if (!prFilePaths.has(relPath) || !diffLines.has(`${relPath}:${err.line}`)) continue;
    let contextLines = '';
    try {
      let filePath = err.file;
      if (!fs.existsSync(filePath)) filePath = path.join(workspace, relPath);
      if (fs.existsSync(filePath)) {
        const lines = fs.readFileSync(filePath, 'utf8').split('\n');
        const start = Math.max(0, err.line - 4);
        const end = Math.min(lines.length, err.line + 4);
        contextLines = lines.slice(start, end).map((l, i) => `${start + i + 1}: ${l}`).join('\n');
      }
    } catch {}
    if (contextLines) candidates.push({ err, relPath, contextLines });
  }

  // For errors on non-PR files, find the declaration in PR files
  const nonPrErrors = [...new Map(
    parsedErrors.filter(e => e.file && e.line && e.code && !prFilePaths.has(toRelPath(e.file)))
      .map(e => [e.message, e])
  ).values()];

  for (const err of nonPrErrors.slice(0, 3)) {
    const nameMatch = err.message.match(/'([^']+)'/);
    if (!nameMatch) continue;
    for (const prFile of prFiles) {
      if (!prFile.filename.endsWith('.cs')) continue;
      try {
        const fullPath = path.join(workspace, prFile.filename);
        if (!fs.existsSync(fullPath)) continue;
        const lines = fs.readFileSync(fullPath, 'utf8').split('\n');
        for (let i = 0; i < lines.length; i++) {
          if (!lines[i].includes(nameMatch[1]) || !diffLines.has(`${prFile.filename}:${i + 1}`)) continue;
          const start = Math.max(0, i - 3);
          const end = Math.min(lines.length, i + 4);
          const contextLines = lines.slice(start, end).map((l, idx) => `${start + idx + 1}: ${l}`).join('\n');
          candidates.push({
            err: { ...err, file: fullPath, line: i + 1 },
            relPath: prFile.filename,
            contextLines,
            isDeclaration: true,
          });
          break;
        }
      } catch {}
      if (candidates.length >= 10) break;
    }
  }

  if (candidates.length === 0) {
    core.info('No suggestion candidates found in PR diff');
    return;
  }

  // Ask LLM for exact replacement lines
  const fixPrompt = [
    'You are a C# code fix assistant. For each build error below, produce the EXACT fixed line(s) to replace the erroring line.',
    'Reply ONLY with a JSON array. Each element: {"index": N, "fixed_lines": "replacement code", "explanation": "one sentence"}',
    'Rules: preserve indentation, set fixed_lines to "" to delete a line, omit index if no fix, no markdown fences.',
    '',
  ];
  candidates.slice(0, 10).forEach((c, idx) => {
    fixPrompt.push(`--- Error ${idx} ---`);
    fixPrompt.push(`File: ${c.relPath}, Line: ${c.err.line}`);
    fixPrompt.push(`Error: ${c.err.code}: ${c.err.message}`);
    if (c.isDeclaration) fixPrompt.push('(Declaration that caused caller errors — make backward-compatible)');
    fixPrompt.push('Context:', c.contextLines, '');
  });

  let fixes = [];
  const endpoints = [
    'https://models.github.ai/inference/chat/completions',
    'https://models.inference.ai.azure.com/chat/completions',
  ];
  for (const endpoint of endpoints) {
    if (fixes.length > 0) break;
    try {
      const resp = await fetch(endpoint, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${ghToken}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({
          model: 'gpt-4o-mini',
          messages: [{ role: 'user', content: fixPrompt.join('\n') }],
          max_tokens: 2000,
        }),
      });
      if (resp.ok) {
        const result = await resp.json();
        let content = result.choices?.[0]?.message?.content || '';
        content = content.replace(/^```(?:json)?\n?/m, '').replace(/\n?```$/m, '').trim();
        fixes = JSON.parse(content);
        core.info(`LLM returned ${fixes.length} fix suggestion(s)`);
      } else {
        core.info(`${endpoint}: ${resp.status}`);
      }
    } catch (e) {
      core.info(`Fix suggestion LLM failed: ${e.message}`);
    }
  }

  // Post suggestions
  let posted = 0;
  for (const fix of fixes) {
    if (fix.index == null || fix.index >= candidates.length) continue;
    const c = candidates[fix.index];
    const body = `🔧 **\`${c.err.code}\`**: ${fix.explanation || ''}\n\`\`\`suggestion\n${fix.fixed_lines ?? ''}\n\`\`\``;
    try {
      await github.rest.pulls.createReviewComment({
        owner: context.repo.owner, repo: context.repo.repo,
        pull_number: prNumber, commit_id: headSha,
        path: c.relPath, line: c.err.line, side: 'RIGHT', body,
      });
      posted++;
      core.info(`Posted suggestion on ${c.relPath}:${c.err.line}`);
    } catch (e) {
      core.info(`Could not post on ${c.relPath}:${c.err.line}: ${e.message}`);
    }
    if (posted >= 10) break;
  }
  core.info(`Posted ${posted} inline suggestion(s)`);
};
