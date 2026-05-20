---
name: "Build Failure Analysis"
description: >-
  Runs `./build.sh --binaryLog` on every PR; when the build fails, delegates
  to the `build-failure-analyst` agent (which reads JSON dumps produced from
  the binlog) to identify root causes, post a PR comment summarizing them,
  and attach inline ```suggestion blocks tied to the diff.

# This workflow is **advisory**, not gating:
#  - It posts an analysis comment / inline suggestions when the build fails.
#  - It does NOT mark the PR check as failing on its own (gh-aw has no
#    post-agent step hook). The repository's deterministic build gate lives
#    in azure-pipelines.yml; if you want a GitHub Actions-level required
#    check, add a separate non-agentic `build.yml` workflow alongside this
#    one and configure branch protection accordingly.

on:
  pull_request:
    types: [opened, synchronize, reopened]
    branches: [main, 'rel/*']
    # Fork PRs are skipped: they cannot install from dotnet-eng (auth-gated)
    # and the agent token would lack the `pull-requests: write` scope needed
    # by safe-outputs.
    forks: []
  workflow_dispatch:
    inputs:
      pr-number:
        description: "PR number to scope inline suggestion comments to (optional)"
        required: false
        type: string
  # Manual reruns and dispatch invocations are restricted to repository
  # contributors. (`pull_request` already gets fork-blocking by default
  # via `forks: []`.) For a slash-command rerun path on PR comments, see
  # the companion `build-failure-analysis-command.md` workflow.
  roles: [admin, maintainer, write]
  reaction: "eyes"

permissions:
  contents: read
  pull-requests: read

concurrency:
  group: build-failure-analysis-${{ github.event.pull_request.number || github.event.issue.number || github.ref }}
  cancel-in-progress: true

env:
  BINLOG_MCP_VERSION: '1.0.0-preview.26268.3'

timeout-minutes: 30

network:
  allowed:
    - defaults
    - dotnet

imports:
  - shared/build-failure-analysis-shared.md

# Deterministic setup that runs before the AI agent starts. By the time the
# agent boots: dotnet is on PATH, the binlog has been produced (whether the
# build succeeded or failed), the binlog path and build outcome are exported
# as `GH_AW_*` env vars, `binlog-mcp` is installed, and the binlog data has
# been dumped to `/tmp/binlog-data/*.json` files for the agent to `cat`.
#
# `continue-on-error: true` is essential on the build step: a failed build
# must not abort the job before the agent gets to analyse it.
steps:
  - name: Build with binary log
    id: build
    continue-on-error: true
    run: |
      set -o pipefail
      ./build.sh --binaryLog 2>&1 | tee /tmp/build-output.log

  - name: Put dotnet on the path
    if: always()
    run: echo "$PWD/.dotnet" >> $GITHUB_PATH

  - name: Locate binlog
    id: find-binlog
    run: |
      BINLOG=$(find artifacts/log -name '*.binlog' -type f -printf '%T@ %p\n' 2>/dev/null \
        | sort -rn | head -1 | cut -d' ' -f2-)
      if [ -n "$BINLOG" ] && [ -f "$BINLOG" ]; then
        echo "found=true"   >> "$GITHUB_OUTPUT"
        echo "path=$BINLOG" >> "$GITHUB_OUTPUT"
      else
        echo "found=false" >> "$GITHUB_OUTPUT"
      fi

  - name: Install binlog-mcp
    if: steps.build.outcome == 'failure' && steps.find-binlog.outputs.found == 'true'
    run: |
      mkdir -p /tmp/binlog-tool
      cat > /tmp/binlog-tool/nuget.config <<'EOF'
      <?xml version="1.0" encoding="utf-8"?>
      <configuration>
        <packageSources>
          <clear />
          <add key="dotnet-eng"
               value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json" />
        </packageSources>
      </configuration>
      EOF
      dotnet tool install --global AITools.BinlogMcp \
        --configfile /tmp/binlog-tool/nuget.config \
        --version "$BINLOG_MCP_VERSION"
      echo "$HOME/.dotnet/tools" >> "$GITHUB_PATH"

  - name: Install MCP SDK for dump-binlog.js
    if: steps.build.outcome == 'failure' && steps.find-binlog.outputs.found == 'true'
    run: cd .github/workflows/scripts && npm ci --ignore-scripts

  - name: Dump binlog as JSON
    if: steps.build.outcome == 'failure' && steps.find-binlog.outputs.found == 'true'
    continue-on-error: true
    run: |
      mkdir -p /tmp/binlog-data
      cd .github/workflows/scripts
      timeout 120 node dump-binlog.js \
        "$GITHUB_WORKSPACE/${{ steps.find-binlog.outputs.path }}" \
        /tmp/binlog-data

  - name: Export agent context
    run: |
      {
        echo "GH_AW_BUILD_OUTCOME=${{ steps.build.outcome }}"
        echo "GH_AW_BINLOG_PATH=${{ steps.find-binlog.outputs.path }}"
        echo "GH_AW_PR_NUMBER=${{ github.event.pull_request.number || github.event.issue.number || inputs.pr-number }}"
        echo "GH_AW_PR_HEAD_SHA=${{ github.event.pull_request.head.sha || github.sha }}"
        echo "GH_AW_WORKSPACE=${{ github.workspace }}"
      } >> "$GITHUB_ENV"

tools:
  github:
    toolsets: [pull_requests, repos]
  bash:
    - "cat"
    - "head"
    - "tail"
    - "grep"
    - "wc"
    - "sort"
    - "uniq"
    - "ls"
    - "find"

safe-outputs:
  add-comment:
    max: 1
    hide-older-comments: true
  create-pull-request-review-comment:
    max: 10
  noop:
    report-as-issue: false
---

<!--
  Body provided by shared/build-failure-analysis-shared.md.

  All build-failure analysis expertise (binlog parsing, error grouping,
  suggestion authoring) lives in the reusable agent at
  .github/agents/build-failure-analyst.agent.md.
-->
