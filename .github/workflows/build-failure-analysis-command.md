---
name: "Build Failure Analysis (command)"
description: >-
  Rerun the build-failure analysis on a pull request when a maintainer
  comments `/analyze-build-failure`. Same body as `build-failure-analysis.md`
  — re-runs `./build.sh --binaryLog`, captures the binlog, and delegates to
  the `build-failure-analyst` agent. Useful when a previous run was
  cancelled, the analysis comment was dismissed, or the agent needs another
  pass after a force-push.

on:
  slash_command:
    name: analyze-build-failure
    events: [pull_request_comment]
  roles: [admin, maintainer, write]
  reaction: "eyes"

permissions:
  contents: read
  pull-requests: read

concurrency:
  group: build-failure-analysis-${{ github.event.issue.number }}
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

# Same deterministic setup as build-failure-analysis.md. The slash-command
# trigger fires on a `pull_request_comment` event; gh-aw handles the PR
# checkout when the comment originates on a PR.
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

  # `pull_request_comment` events use the `issues` event payload, so
  # `github.sha` is the default branch tip — NOT the PR head. Always resolve
  # the real PR head SHA via the API so permalinks and inline comment
  # placement match the PR.
  - name: Resolve PR head SHA
    id: resolve-pr-sha
    env:
      GH_TOKEN: ${{ github.token }}
    run: |
      SHA=$(gh api "repos/${{ github.repository }}/pulls/${{ github.event.issue.number }}" --jq .head.sha)
      echo "sha=$SHA" >> "$GITHUB_OUTPUT"

  - name: Export agent context
    run: |
      {
        echo "GH_AW_BUILD_OUTCOME=${{ steps.build.outcome }}"
        echo "GH_AW_BINLOG_PATH=${{ steps.find-binlog.outputs.path }}"
        echo "GH_AW_PR_NUMBER=${{ github.event.issue.number }}"
        echo "GH_AW_PR_HEAD_SHA=${{ steps.resolve-pr-sha.outputs.sha || github.sha }}"
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
-->
