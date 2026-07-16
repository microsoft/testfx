---
name: "Build Failure Analysis (command)"
description: >-
  Rerun the build-failure analysis on a pull request when a maintainer comments
  `/analyze-build-failure`. Same body as `build-failure-analysis.md` — it does
  NOT rebuild: it finds the PR's most recent **failed** Azure Pipelines
  `microsoft.testfx` build, downloads the binary logs that build already produced
  (all build legs), and delegates to the `build-failure-analyst` agent (which
  queries the binlogs live via the containerized `binlog-mcp` MCP server).
  Useful when a previous run was cancelled, the analysis comment was dismissed,
  or the agent needs another pass after a force-push.

on:
  slash_command:
    name: analyze-build-failure
    events: [pull_request_comment]
    strategy: centralized
  roles: [admin, maintainer, write]
  reaction: "eyes"
  # Gate the AI pipeline on the fetch job so the agent only runs when a binlog
  # was actually retrieved from a failed Azure DevOps build.
  needs: [fetch-binlog]

# Skip activation (and the agent) unless a binlog was retrieved — e.g. if the
# PR's latest Azure DevOps build did not fail, or the PR is out of scope.
if: needs.fetch-binlog.outputs.binlog-found == 'true'

permissions:
  contents: read
  pull-requests: read
  copilot-requests: write

concurrency:
  group: build-failure-analysis-${{ github.event.issue.number || github.event.pull_request.number || fromJSON(github.event.inputs.aw_context || github.event.client_payload.aw_context || '{}').item_number || github.run_id }}
  cancel-in-progress: true

timeout-minutes: 30

network:
  allowed:
    - defaults
    - dotnet

imports:
  - shared/build-failure-analysis-shared.md

# Live binlog access for the agent — see build-failure-analysis.md for the
# rationale. The fetch-binlog job downloads each build leg's binlog from Azure
# DevOps into a directory and uploads it; the agent job downloads it to
# `/tmp/binlogs` and the gh-aw MCP gateway mounts it read-only at
# `/data/binlogs`.
mcp-servers:
  binlog-mcp:
    container: "mcr.microsoft.com/dotnet-buildtools/prereqs:azurelinux-3.0-binlog-mcp-amd64"
    mounts:
      - "/tmp/binlogs:/data/binlogs:ro"
    allowed: ["*"]

# Custom job that reuses the binlogs from the PR's most recent failed Azure
# DevOps `microsoft.testfx` build instead of rebuilding. Mirrors the fetch-binlog job
# in build-failure-analysis.md; it locates the build by the PR's merge branch
# (no `check_run` payload is available on a slash command).
jobs:
  fetch-binlog:
    name: Fetch binlogs (Azure Pipelines)
    runs-on: ubuntu-latest
    timeout-minutes: 15
    permissions:
      contents: read
      pull-requests: read
    outputs:
      binlog-found: ${{ steps.fetch.outputs.binlog-found }}
      pr-number: ${{ steps.fetch.outputs.pr-number }}
      pr-head-sha: ${{ steps.fetch.outputs.pr-head-sha }}
      ado-build-id: ${{ steps.fetch.outputs.ado-build-id }}
      ado-build-url: ${{ steps.fetch.outputs.ado-build-url }}
    steps:
      - name: Download binlogs from the PR's latest failed Azure Pipelines build
        id: fetch
        env:
          GH_TOKEN: ${{ github.token }}
          GH_AW_REPO: ${{ github.repository }}
          ADO_API: "https://dev.azure.com/dnceng-public/public/_apis"
          ADO_BUILD_UI: "https://dev.azure.com/dnceng-public/public/_build/results"
          # microsoft.testfx pipeline definition id in dnceng-public/public.
          ADO_BUILD_DEFINITION_ID: "209"
          PR_NUMBER: ${{ github.event.issue.number || fromJSON(github.event.inputs.aw_context || github.event.client_payload.aw_context || '{}').item_number }}
        run: |
          # Advisory + best-effort. On any gap emit binlog-found=false so the
          # agent pipeline stays inert.
          set +e
          set +o pipefail
          emit_none() { echo "binlog-found=false" >> "$GITHUB_OUTPUT"; exit 0; }

          [ -z "${PR_NUMBER}" ] && { echo "::warning::No PR number resolved from the slash-command event / aw_context."; emit_none; }

          # --- Scope check: only analyse PRs targeting main / rel/* ---
          PR_JSON=$(gh api "repos/${GH_AW_REPO}/pulls/${PR_NUMBER}" 2>/dev/null)
          BASE_REF=$(printf '%s' "${PR_JSON}" | jq -r '.base.ref // empty')
          HEAD_SHA=$(printf '%s' "${PR_JSON}" | jq -r '.head.sha // empty')
          case "${BASE_REF}" in
            main|rel/*) echo "PR #${PR_NUMBER} base '${BASE_REF}' is in scope." ;;
            *) echo "::warning::PR #${PR_NUMBER} base '${BASE_REF}' is out of scope (main, rel/*); skipping."; emit_none ;;
          esac

          # --- Find the PR's most recent microsoft.testfx build (merge ref) ---
          # Query the newest build REGARDLESS of status (queue-time desc). If
          # the newest build is still queued/running — e.g. right after a
          # force-push — skip: analysing an older completed failure now would
          # pair a stale binlog with the PR's current head. Only proceed when
          # the newest build is completed AND failed. The head SHA is then
          # anchored to that build's own revision (below), so links/suggestions
          # always match the analysed binlog.
          builds_json=$(curl -sSL --retry 3 \
            "${ADO_API}/build/builds?definitions=${ADO_BUILD_DEFINITION_ID}&branchName=refs/pull/${PR_NUMBER}/merge&queryOrder=queueTimeDescending&\$top=1&api-version=7.1")
          BUILD_ID=$(printf '%s' "${builds_json}" | jq -r '.value // [] | .[0].id // empty')
          BUILD_STATUS=$(printf '%s' "${builds_json}" | jq -r '.value // [] | .[0].status // empty')
          BUILD_RESULT=$(printf '%s' "${builds_json}" | jq -r '.value // [] | .[0].result // empty')
          echo "Newest microsoft.testfx build for PR #${PR_NUMBER}: id='${BUILD_ID}' status='${BUILD_STATUS}' result='${BUILD_RESULT}'"
          [ -z "${BUILD_ID}" ] && { echo "::warning::No microsoft.testfx build found for PR #${PR_NUMBER}."; emit_none; }
          if [ "${BUILD_STATUS}" != "completed" ]; then
            echo "::warning::PR #${PR_NUMBER}'s newest microsoft.testfx build (${BUILD_ID}) is still '${BUILD_STATUS}'; wait for it to finish before analysing."
            emit_none
          fi
          if [ "${BUILD_RESULT}" != "failed" ]; then
            echo "::warning::PR #${PR_NUMBER}'s newest microsoft.testfx build (${BUILD_ID}) result is '${BUILD_RESULT}', not failed — the failure looks resolved; nothing to analyse."
            emit_none
          fi

          # Anchor the head SHA to the revision this build analyzed
          # (`triggerInfo["pr.sourceSha"]`) rather than the PR's current head,
          # so permalinks / inline suggestions match the binlog exactly.
          build_json=$(curl -sSL --retry 3 "${ADO_API}/build/builds/${BUILD_ID}?api-version=7.1")
          BUILD_PR_SHA=$(printf '%s' "${build_json}" | jq -r '.triggerInfo["pr.sourceSha"] // empty')
          [ -n "${BUILD_PR_SHA}" ] && HEAD_SHA="${BUILD_PR_SHA}"
          echo "Analyzing build ${BUILD_ID} at PR head revision '${HEAD_SHA}'."

          # --- Download every Logs_Build_* artifact and extract binlogs ---
          artifacts_json=$(curl -sSL --retry 3 "${ADO_API}/build/builds/${BUILD_ID}/artifacts?api-version=7.1")
          names=$(printf '%s' "${artifacts_json}" | jq -r '.value // [] | map(select(.name | test("^Logs_Build_"))) | .[].name')
          [ -z "${names}" ] && { echo "::warning::No Logs_Build_* artifacts on build ${BUILD_ID}."; emit_none; }

          # Guards for untrusted PR-produced archives: cap the compressed
          # download and the reported uncompressed size per artifact, bound
          # extraction time, AND enforce a cumulative uncompressed budget across
          # all legs so many individually-small artifacts can't collectively
          # exhaust the runner's disk.
          MAX_ZIP_BYTES=524288000       # 500 MB compressed per artifact
          MAX_UNZIP_BYTES=2147483648    # 2 GB uncompressed per artifact
          MAX_TOTAL_BYTES=4294967296    # 4 GB uncompressed across all artifacts
          TOTAL_BYTES=0
          mkdir -p /tmp/binlogs
          count=0
          for name in ${names}; do
            url=$(printf '%s' "${artifacts_json}" | jq -r --arg n "${name}" '.value[] | select(.name==$n) | .resource.downloadUrl // empty')
            [ -z "${url}" ] && continue
            rm -rf /tmp/ax /tmp/a.zip
            mkdir -p /tmp/ax
            curl -sSL --retry 3 --max-filesize "${MAX_ZIP_BYTES}" "${url}" -o /tmp/a.zip \
              || { echo "::warning::Skipping ${name}: download failed or exceeded ${MAX_ZIP_BYTES} bytes."; continue; }
            UNCOMP=$(unzip -l /tmp/a.zip 2>/dev/null | tail -1 | awk '{print $1}')
            # Fail safe: if the uncompressed size isn't a plain integer (corrupt
            # zip / unexpected `unzip -l` output), we can't verify it — skip the
            # artifact rather than let a non-numeric value bypass the `-gt` guard.
            if ! printf '%s' "${UNCOMP}" | grep -qE '^[0-9]+$'; then
              echo "::warning::Skipping ${name}: could not determine uncompressed size (unparseable unzip output)."; continue
            fi
            if [ "${UNCOMP}" -gt "${MAX_UNZIP_BYTES}" ]; then
              echo "::warning::Skipping ${name}: uncompressed size ${UNCOMP} exceeds ${MAX_UNZIP_BYTES} guard (possible zip bomb)."; continue
            fi
            if [ $((TOTAL_BYTES + UNCOMP)) -gt "${MAX_TOTAL_BYTES}" ]; then
              echo "::warning::Cumulative uncompressed budget ${MAX_TOTAL_BYTES} reached at ${name}; stopping extraction."; break
            fi
            TOTAL_BYTES=$((TOTAL_BYTES + UNCOMP))
            # Extract ONLY `*.binlog` entries with paths junked (`-j`) under a
            # timeout so a malicious/relative entry (zip-slip) can't escape the
            # destination and extraction can't run unbounded.
            timeout 120 unzip -j -o /tmp/a.zip '*.binlog' -d /tmp/ax >/dev/null 2>&1 \
              || { echo "::warning::Skipping ${name}: extraction failed or timed out."; continue; }
            i=0
            while IFS= read -r bl; do
              [ -f "${bl}" ] || continue
              dest="/tmp/binlogs/${name}"
              [ ${i} -gt 0 ] && dest="/tmp/binlogs/${name}.${i}"
              cp "${bl}" "${dest}.binlog"
              count=$((count + 1))
              i=$((i + 1))
            done < <(find /tmp/ax -maxdepth 1 -name '*.binlog' -type f)
          done
          echo "Extracted ${count} binlog(s) into /tmp/binlogs:"
          ls -la /tmp/binlogs || true
          [ "${count}" -eq 0 ] && { echo "::warning::No *.binlog found in any Logs_Build_* artifact of build ${BUILD_ID}."; emit_none; }

          {
            echo "binlog-found=true"
            echo "pr-number=${PR_NUMBER}"
            echo "pr-head-sha=${HEAD_SHA}"
            echo "ado-build-id=${BUILD_ID}"
            echo "ado-build-url=${ADO_BUILD_UI}?buildId=${BUILD_ID}"
          } >> "$GITHUB_OUTPUT"

      - name: Upload analysis artifact
        if: steps.fetch.outputs.binlog-found == 'true'
        uses: actions/upload-artifact@v7.0.1
        with:
          name: build-failure-analysis-data
          path: /tmp/binlogs
          if-no-files-found: warn
          retention-days: 1

# Steps that run in the agent job. The top-level `if:` gates these on binlogs
# having been retrieved, so the agent never runs without something to analyse.
steps:
  - name: Download analysis artifact
    uses: actions/download-artifact@v8.0.1
    with:
      name: build-failure-analysis-data
      path: /tmp/binlogs

  - name: Export agent context
    env:
      GH_AW_BINLOG_FOUND_VALUE: ${{ needs.fetch-binlog.outputs.binlog-found }}
      GH_AW_PR_NUMBER_VALUE: ${{ needs.fetch-binlog.outputs.pr-number }}
      GH_AW_PR_HEAD_SHA_VALUE: ${{ needs.fetch-binlog.outputs.pr-head-sha }}
      GH_AW_ADO_BUILD_URL_VALUE: ${{ needs.fetch-binlog.outputs.ado-build-url }}
      GH_AW_GITHUB_WORKSPACE: ${{ github.workspace }}
    run: |
      # See build-failure-analysis.md for the binlog path conventions. The
      # per-leg binlogs are read through the binlog-mcp MCP server (mounted at
      # `/data/binlogs`); GH_AW_BINLOG_HOST_PATH points at the Azure DevOps
      # build for human-facing references.
      BINLOG_DIR="/data/binlogs"
      LIST=""
      if [ "${GH_AW_BINLOG_FOUND_VALUE:-false}" = "true" ] && [ -d /tmp/binlogs ]; then
        for f in /tmp/binlogs/*.binlog; do
          [ -f "$f" ] || continue
          LIST="${LIST}${BINLOG_DIR}/$(basename "$f")"$'\n'
        done
      fi
      FIRST=$(printf '%s' "$LIST" | head -1)
      {
        echo "GH_AW_BUILD_OUTCOME=failure"
        echo "GH_AW_BINLOG_DIR=${BINLOG_DIR}"
        echo "GH_AW_BINLOG_PATH=${FIRST}"
        echo "GH_AW_BINLOG_HOST_PATH=${GH_AW_ADO_BUILD_URL_VALUE}"
        echo "GH_AW_PR_NUMBER=${GH_AW_PR_NUMBER_VALUE}"
        echo "GH_AW_PR_HEAD_SHA=${GH_AW_PR_HEAD_SHA_VALUE}"
        echo "GH_AW_WORKSPACE=${GH_AW_GITHUB_WORKSPACE}"
        echo "GH_AW_BINLOG_LIST<<GH_AW_EOF"
        printf '%s' "$LIST"
        echo "GH_AW_EOF"
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
  messages:
    footer: "> 🤖 **Automated content by GitHub Copilot.** Generated by the [{workflow_name}]({agentic_workflow_url}) workflow.{ai_credits_suffix} · [◷]({history_link})"
  # The agent targets the resolved PR via `GH_AW_PR_NUMBER` (`target: "*"`),
  # matching the auto-trigger workflow.
  report-failure-as-issue: false
  add-comment:
    max: 5
    target: "*"
    hide-older-comments: true
  create-pull-request-review-comment:
    max: 25
    target: "*"
  noop:
    max: 5
    report-as-issue: false
---

<!--
  Body provided by shared/build-failure-analysis-shared.md.
-->
