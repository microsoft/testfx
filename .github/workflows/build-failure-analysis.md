---
name: "Build Failure Analysis"
description: >-
  Runs `./build.sh --binaryLog` on every PR; when the build fails, delegates
  to the `build-failure-analyst` agent (which reads JSON dumps produced from
  the binlog) to identify root causes, post a PR comment summarizing them,
  and attach inline `suggestion` blocks tied to the diff.

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
    # Fork PRs are skipped: they cannot install from dotnet-tools (auth-gated)
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
  # Make `pre_activation` and `activation` wait for the custom `build` job
  # defined below. Combined with the top-level `if:`, this gates the entire
  # AI agent pipeline on build failure — so transient Copilot AI flakes can
  # never surface as a red workflow check on a successful build.
  needs: [build]

# Skip activation (and therefore the agent job) when the build job reported
# success. gh-aw applies top-level `if:` to the `activation` job, which is a
# dependency of `agent`, so a skipped activation cascades into a skipped
# agent — no AI calls, no safe-output validation, no chance of a noop-loop
# from a transient AI server error on an otherwise green build.
if: needs.build.outputs.outcome == 'failure'

permissions:
  contents: read
  pull-requests: read

concurrency:
  group: build-failure-analysis-${{ github.event.pull_request.number || github.event.issue.number || github.ref }}
  cancel-in-progress: true

env:
  BINLOG_MCP_VERSION: '1.0.0-preview.26272.1'
  NUGET_MCP_VERSION: '1.4.3'

timeout-minutes: 30

network:
  allowed:
    - defaults
    - dotnet

imports:
  - shared/build-failure-analysis-shared.md

# Custom build job that runs unconditionally on every PR. It produces the
# binlog and (on failure) dumps it to JSON files which are uploaded as an
# artifact for the agent job to consume. The agent pipeline only runs when
# this job reports `outcome == 'failure'` (see top-level `if:` above).
jobs:
  build:
    name: Build (for analysis)
    runs-on: ubuntu-latest
    timeout-minutes: 30
    # Mirror the workflow's `forks: []` trigger filter: skip fork PRs at the
    # build-job level too. Without this guard the build job would still run
    # for fork PRs (paying CI time and exposing dotnet-tools auth-gated
    # installs to forks) even though the agent pipeline never runs for them.
    if: github.event_name != 'pull_request' || github.event.pull_request.head.repo.full_name == github.repository
    permissions:
      contents: read
    outputs:
      outcome: ${{ steps.build.outcome }}
      binlog-found: ${{ steps.find-binlog.outputs.found }}
      binlog-relative-path: ${{ steps.find-binlog.outputs.relative-path }}
    env:
      BINLOG_MCP_VERSION: '1.0.0-preview.26272.1'
    steps:
      - uses: actions/checkout@v6

      - name: Build with binary log
        id: build
        continue-on-error: true
        run: |
          set -uo pipefail
          ./build.sh --binaryLog 2>&1 | tee /tmp/build-output.log
          # `tee` is best-effort: rely on the build's own exit code so a
          # logging-pipeline glitch never misclassifies a green build as
          # failed (which would otherwise trigger the AI agent and
          # re-expose us to the Copilot-flake red-X bug).
          exit "${PIPESTATUS[0]}"

      - name: Put dotnet on the path
        if: always()
        run: echo "$PWD/.dotnet" >> $GITHUB_PATH

      - name: Locate binlog
        id: find-binlog
        if: always()
        run: |
          BINLOG=$(find artifacts/log -name '*.binlog' -type f -printf '%T@ %p\n' 2>/dev/null \
            | sort -rn | head -1 | cut -d' ' -f2-)
          if [ -n "$BINLOG" ] && [ -f "$BINLOG" ]; then
            REL=$(realpath --relative-to="$PWD" "$BINLOG")
            echo "found=true"             >> "$GITHUB_OUTPUT"
            echo "relative-path=$REL"     >> "$GITHUB_OUTPUT"
          else
            echo "found=false" >> "$GITHUB_OUTPUT"
          fi

      - name: Install binlog-mcp
        if: steps.build.outcome == 'failure' && steps.find-binlog.outputs.found == 'true'
        continue-on-error: true
        run: |
          mkdir -p /tmp/binlog-tool
          cat > /tmp/binlog-tool/nuget.config <<'EOF'
          <?xml version="1.0" encoding="utf-8"?>
          <configuration>
            <packageSources>
              <clear />
              <add key="dotnet-tools"
                   value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/index.json" />
            </packageSources>
          </configuration>
          EOF
          dotnet tool install --global Microsoft.AITools.BinlogMcp \
            --configfile /tmp/binlog-tool/nuget.config \
            --version "$BINLOG_MCP_VERSION"
          echo "$HOME/.dotnet/tools" >> "$GITHUB_PATH"

      - name: Dump binlog as JSON
        if: steps.build.outcome == 'failure' && steps.find-binlog.outputs.found == 'true'
        continue-on-error: true
        env:
          BINLOG_REL_PATH: ${{ steps.find-binlog.outputs.relative-path }}
        run: |
          mkdir -p /tmp/binlog-data
          timeout 180 dotnet run --project .github/workflows/scripts/DumpBinlog -- \
            "$BINLOG_REL_PATH" \
            /tmp/binlog-data

      # Upload everything the agent needs. Always upload when the build
      # failed (even if dump-binlog failed), so the agent gets the raw
      # build output log and can still emit a "build failed, no binlog
      # data" comment.
      - name: Upload analysis artifact
        if: always() && steps.build.outcome == 'failure'
        continue-on-error: true
        uses: actions/upload-artifact@v7
        with:
          name: build-failure-analysis-data
          path: |
            /tmp/binlog-data/
            /tmp/build-output.log
          if-no-files-found: warn
          retention-days: 1

# Steps that run in the agent job. Because the top-level `if:` gates
# activation on `needs.build.outputs.outcome == 'failure'`, these only run
# for failed builds — the agent never executes on a successful build and a
# transient Copilot AI flake can no longer surface as a red workflow check
# on a passing PR.
steps:
  - name: Download analysis artifact
    uses: actions/download-artifact@v8
    with:
      name: build-failure-analysis-data
      path: /tmp/

  - name: Setup .NET (for NuGet MCP Server)
    uses: actions/setup-dotnet@v5
    with:
      dotnet-version: '9.0.x'

  - name: Install NuGet MCP Server
    continue-on-error: true
    run: dotnet tool install --global NuGet.Mcp.Server --version "$NUGET_MCP_VERSION"

  # On `workflow_dispatch` runs, `github.sha` is the SHA of the dispatched ref
  # (usually the default branch), NOT the PR head. Look up the real PR head
  # SHA via the API so permalinks and inline comment placement match the PR.
  - name: Resolve PR head SHA (workflow_dispatch only)
    if: github.event_name == 'workflow_dispatch' && inputs.pr-number != ''
    id: resolve-pr-sha
    env:
      GH_TOKEN: ${{ github.token }}
      GH_AW_GITHUB_REPOSITORY: ${{ github.repository }}
      GH_AW_INPUTS_PR_NUMBER: ${{ inputs.pr-number }}
    run: |
      SHA=$(gh api "repos/${GH_AW_GITHUB_REPOSITORY}/pulls/${GH_AW_INPUTS_PR_NUMBER}" --jq .head.sha)
      echo "sha=$SHA" >> "$GITHUB_OUTPUT"

  - name: Export agent context
    env:
      GH_AW_BUILD_OUTCOME_VALUE: ${{ needs.build.outputs.outcome }}
      GH_AW_BINLOG_REL_VALUE: ${{ needs.build.outputs.binlog-relative-path }}
      GH_AW_PR_NUMBER_VALUE: ${{ github.event.pull_request.number || github.event.issue.number || inputs.pr-number }}
      GH_AW_PR_HEAD_SHA_VALUE: ${{ steps.resolve-pr-sha.outputs.sha || github.event.pull_request.head.sha || github.sha }}
      GH_AW_GITHUB_WORKSPACE: ${{ github.workspace }}
    run: |
      # The binlog file itself is not transported between jobs (it is large
      # and the agent only needs the pre-dumped JSON files). Set
      # GH_AW_BINLOG_PATH to a synthetic workspace-relative path purely for
      # display / permalink purposes; the agent must rely on
      # /tmp/binlog-data/*.json for actual data (see shared body).
      BINLOG_PATH=""
      if [ -n "${GH_AW_BINLOG_REL_VALUE:-}" ]; then
        BINLOG_PATH="${GH_AW_GITHUB_WORKSPACE}/${GH_AW_BINLOG_REL_VALUE}"
      fi
      {
        echo "GH_AW_BUILD_OUTCOME=${GH_AW_BUILD_OUTCOME_VALUE}"
        echo "GH_AW_BINLOG_PATH=${BINLOG_PATH}"
        echo "GH_AW_PR_NUMBER=${GH_AW_PR_NUMBER_VALUE}"
        echo "GH_AW_PR_HEAD_SHA=${GH_AW_PR_HEAD_SHA_VALUE}"
        echo "GH_AW_WORKSPACE=${GH_AW_GITHUB_WORKSPACE}"
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
    - "dotnet"
    - "NuGet.Mcp.Server"

safe-outputs:
  # The agent runs only when the build job reports failure (see top-level
  # `if:` above). On a failed build the agent normally emits at most one
  # `noop`, one summary comment, and a small set of inline review comments,
  # but the Copilot CLI harness retries with `--continue` on
  # mid-conversation AI flakes (up to 3 retries) and each retry re-emits
  # every safe-output call it has issued so far. The caps below absorb that
  # retry budget without spurious safe-output validation warnings:
  #   - noop max=5: covers 1 happy-path + 4 retry-amplified noops.
  #   - add-comment max=5: covers 1 summary + 4 retries (hide-older-comments
  #     auto-collapses the duplicates anyway).
  #   - create-pull-request-review-comment max=25: shared body asks the
  #     agent for "top 5 highest-priority issues" per run, so 5 × (1 + 3
  #     retries) = 20 is the worst case under flake amplification.
  # We also disable `report-as-issue` / `report-failure-as-issue` so
  # transient flakes never spam tracking issues (see issue #8685).
  report-failure-as-issue: false
  add-comment:
    max: 5
    hide-older-comments: true
  create-pull-request-review-comment:
    max: 25
  noop:
    max: 5
    report-as-issue: false
---

<!--
  Body provided by shared/build-failure-analysis-shared.md.

  All build-failure analysis expertise (binlog parsing, error grouping,
  suggestion authoring) lives in the reusable agent at
  .github/agents/build-failure-analyst.agent.md.
-->
