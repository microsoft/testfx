---
name: "Build Failure Analysis"
description: >-
  When the Azure Pipelines PR build (`microsoft.testfx`) fails, downloads the binary
  logs that build already produced — it does NOT rebuild — and delegates to
  the `build-failure-analyst` agent, which queries the binlogs live via the
  containerized `binlog-mcp` MCP server to identify root causes, post a PR
  comment summarizing them, and attach inline `suggestion` blocks tied to the
  diff.

# This workflow is **advisory**, not gating, and it performs **no build of its
# own**. testfx's authoritative PR build runs on Azure DevOps
# (dnceng-public/public, pipeline "microsoft.testfx", definitionId 209) and publishes
# each build leg's binary log as a `Logs_Build_<leg>` pipeline artifact. When
# that build's GitHub check reports failure, this workflow downloads the
# binlogs from **all** build legs (anonymously — dnceng-public/public is a
# public project) and the agent analyses whichever leg(s) actually contain
# errors. Reusing the binlogs avoids a duplicate build: the analysis pipeline
# only downloads build artifacts (data) and reads them — it does **not** build
# or execute PR code. (gh-aw's generated jobs may run `actions/checkout` for
# agent context; that is a checkout of the repository for tooling, not a build
# or execution of the PR's code.)

on:
  # `check_run` fires for every check on a commit, so the `fetch-binlog` job
  # below filters tightly to the `microsoft.testfx` build check reporting failure.
  check_run:
    types: [completed]
  # Advisory analysis should run for **every** failing PR — including external
  # contributors' PRs, which are the most likely to break the build. Disable
  # gh-aw's default author-association gate (which would otherwise skip
  # non-write-access actors, and on `check_run` the actor is the pipeline app
  # anyway). This is safe here: the workflow only reads a public binlog and
  # posts advisory comments — it never builds or executes PR code.
  roles: all
  # Manual entry point for reruns / testing: analyse a specific Azure DevOps
  # build id and post to a specific PR.
  workflow_dispatch:
    inputs:
      ado-build-id:
        description: "Azure DevOps build id to analyze (dnceng-public/public)."
        required: true
        type: string
      pr-number:
        description: "PR number to post the analysis on."
        required: true
        type: string
  # Gate the whole AI pipeline on the fetch job so the agent only runs when a
  # binlog was actually retrieved.
  needs: [fetch-binlog]

# Activate (and run the agent) only when the fetch job retrieved at least one
# binlog. When `check_run` fires for an unrelated / passing check the
# fetch-binlog job is skipped, its output is empty, and this cascades into a
# skipped agent — no AI calls on anything but a real `microsoft.testfx` failure whose
# PR targets an in-scope base branch.
if: needs.fetch-binlog.outputs.binlog-found == 'true'

permissions:
  contents: read
  pull-requests: read
  copilot-requests: write

concurrency:
  group: build-failure-analysis-${{ github.event.check_run.pull_requests[0].number || inputs.pr-number || github.event.check_run.head_sha || github.run_id }}
  cancel-in-progress: true

timeout-minutes: 30

network:
  allowed:
    - defaults
    - dotnet

imports:
  - shared/build-failure-analysis-shared.md

# Live binlog access for the agent. The build-leg binlogs are downloaded from
# Azure DevOps by the fetch-binlog job into a directory, uploaded as an
# artifact, downloaded by the agent job to `/tmp/binlogs`, and mounted
# read-only into this container at `/data/binlogs` by the gh-aw MCP gateway.
mcp-servers:
  binlog-mcp:
    container: "mcr.microsoft.com/dotnet-buildtools/prereqs:azurelinux-3.0-binlog-mcp-amd64"
    mounts:
      - "/tmp/binlogs:/data/binlogs:ro"
    allowed: ["*"]

# Custom job that reuses the binlogs from the failed Azure DevOps build instead
# of rebuilding. It resolves the ADO build id (from the check details URL or
# the dispatch input), verifies the PR targets an in-scope base branch,
# downloads every `Logs_Build_*` artifact, extracts each leg's `*.binlog`, and
# uploads them for the agent job.
jobs:
  fetch-binlog:
    name: Fetch binlogs (Azure Pipelines)
    runs-on: ubuntu-latest
    timeout-minutes: 15
    # `check_run` fires for every check; only act on the testfx PR build check
    # reporting failure (or a manual dispatch).
    if: >
      github.event_name == 'workflow_dispatch' ||
      (github.event.check_run.name == 'microsoft.testfx' && github.event.check_run.conclusion == 'failure')
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
      - name: Download binlogs from the failed Azure Pipelines build
        id: fetch
        env:
          GH_TOKEN: ${{ github.token }}
          GH_AW_REPO: ${{ github.repository }}
          ADO_API: "https://dev.azure.com/dnceng-public/public/_apis"
          ADO_BUILD_UI: "https://dev.azure.com/dnceng-public/public/_build/results"
          # microsoft.testfx pipeline definition id in dnceng-public/public (used to
          # validate a dispatched build id belongs to the right pipeline).
          ADO_BUILD_DEFINITION_ID: "209"
          EVENT_NAME: ${{ github.event_name }}
          CHECK_DETAILS_URL: ${{ github.event.check_run.details_url }}
          CHECK_HEAD_SHA: ${{ github.event.check_run.head_sha }}
          CHECK_PR_NUMBER: ${{ github.event.check_run.pull_requests[0].number }}
          DISPATCH_BUILD_ID: ${{ inputs.ado-build-id }}
          DISPATCH_PR_NUMBER: ${{ inputs.pr-number }}
        run: |
          # Advisory + best-effort: on any gap emit binlog-found=false and the
          # agent pipeline stays inert.
          set +e
          set +o pipefail
          emit_none() { echo "binlog-found=false" >> "$GITHUB_OUTPUT"; exit 0; }

          # --- 1. Resolve the Azure DevOps build id ---
          if [ "${EVENT_NAME}" = "workflow_dispatch" ]; then
            BUILD_ID="${DISPATCH_BUILD_ID}"
          else
            # details_url looks like: .../_build/results?buildId=NNN&view=...
            BUILD_ID=$(printf '%s' "${CHECK_DETAILS_URL}" | grep -oE 'buildId=[0-9]+' | head -1 | cut -d= -f2)
          fi
          echo "Azure DevOps build id: '${BUILD_ID}'"
          [ -z "${BUILD_ID}" ] && { echo "::warning::Could not resolve an ADO build id."; emit_none; }

          # --- 2. Resolve the PR number + head SHA ---
          if [ "${EVENT_NAME}" = "workflow_dispatch" ]; then
            PR_NUMBER="${DISPATCH_PR_NUMBER}"
            HEAD_SHA=""
          else
            PR_NUMBER="${CHECK_PR_NUMBER}"
            HEAD_SHA="${CHECK_HEAD_SHA}"
          fi
          # Fork PRs don't populate check_run.pull_requests; fall back to the
          # commit -> PR association API.
          if [ -z "${PR_NUMBER}" ] && [ -n "${HEAD_SHA}" ]; then
            PR_NUMBER=$(gh api "repos/${GH_AW_REPO}/commits/${HEAD_SHA}/pulls" --jq '.[0].number' 2>/dev/null)
          fi
          [ -z "${PR_NUMBER}" ] && { echo "::warning::Could not resolve a PR number."; emit_none; }

          # --- 3. Scope check: only analyse PRs targeting main / rel/* ---
          PR_JSON=$(gh api "repos/${GH_AW_REPO}/pulls/${PR_NUMBER}" 2>/dev/null)
          BASE_REF=$(printf '%s' "${PR_JSON}" | jq -r '.base.ref // empty')
          [ -z "${HEAD_SHA}" ] && HEAD_SHA=$(printf '%s' "${PR_JSON}" | jq -r '.head.sha // empty')
          case "${BASE_REF}" in
            main|rel/*) echo "PR #${PR_NUMBER} base '${BASE_REF}' is in scope." ;;
            *) echo "::warning::PR #${PR_NUMBER} base '${BASE_REF}' is out of scope (main, rel/*); skipping."; emit_none ;;
          esac

          # --- 4. Validate the build for EVERY trigger (not just dispatch):
          #        it must be the microsoft.testfx definition (209), have failed, and
          #        belong to this PR (sourceBranch == refs/pull/<PR>/merge).
          #        For `check_run` the build id is parsed from a check payload
          #        we don't fully trust; for dispatch the build id and PR
          #        number are independent inputs. Validating on both paths
          #        prevents downloading an unrelated build or posting its
          #        analysis to the wrong PR.
          build_json=$(curl -sSL --retry 3 "${ADO_API}/build/builds/${BUILD_ID}?api-version=7.1")
          RESULT=$(printf '%s' "${build_json}" | jq -r '.result // empty')
          DEF_ID=$(printf '%s' "${build_json}" | jq -r '.definition.id // empty')
          SRC_BRANCH=$(printf '%s' "${build_json}" | jq -r '.sourceBranch // empty')
          echo "ADO build ${BUILD_ID}: result='${RESULT}' definition='${DEF_ID}' sourceBranch='${SRC_BRANCH}'"
          if [ "${DEF_ID}" != "${ADO_BUILD_DEFINITION_ID}" ]; then
            echo "::warning::ADO build ${BUILD_ID} is definition '${DEF_ID}', not microsoft.testfx (${ADO_BUILD_DEFINITION_ID}); refusing."; emit_none
          fi
          if [ "${RESULT}" != "failed" ]; then
            echo "::warning::ADO build ${BUILD_ID} did not fail (result='${RESULT}'); nothing to analyze."; emit_none
          fi
          if [ "${SRC_BRANCH}" != "refs/pull/${PR_NUMBER}/merge" ]; then
            echo "::warning::ADO build ${BUILD_ID} sourceBranch '${SRC_BRANCH}' does not match PR #${PR_NUMBER} (refs/pull/${PR_NUMBER}/merge); refusing to avoid posting to the wrong PR."; emit_none
          fi

          # --- 5. Download every Logs_Build_* artifact and extract binlogs ---
          artifacts_json=$(curl -sSL --retry 3 "${ADO_API}/build/builds/${BUILD_ID}/artifacts?api-version=7.1")
          names=$(printf '%s' "${artifacts_json}" | jq -r '.value // [] | map(select(.name | test("^Logs_Build_"))) | .[].name')
          [ -z "${names}" ] && { echo "::warning::No Logs_Build_* artifacts on build ${BUILD_ID}."; emit_none; }

          # Guards for untrusted PR-produced archives: cap the compressed
          # download and the reported uncompressed size, and bound extraction
          # time, so a zip bomb / oversized artifact can't exhaust the runner.
          MAX_ZIP_BYTES=524288000      # 500 MB compressed per artifact
          MAX_UNZIP_BYTES=2147483648   # 2 GB uncompressed per artifact
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

# Steps that run in the agent job. Because the top-level `if:` gates activation
# on `needs.fetch-binlog.outputs.binlog-found == 'true'`, these only run once
# binlogs have been retrieved from the failed Azure DevOps build.
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
      # The binlogs are mounted into the binlog-mcp container at
      # `/data/binlogs`. Build the list of in-container binlog paths (one per
      # build leg) that the agent should query. `GH_AW_BINLOG_PATH` is the
      # first entry for tools/prompts that expect a single path.
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
  # `check_run` carries no native issue/PR context for gh-aw, so the agent must
  # target the resolved PR explicitly (`target: "*"`) using `GH_AW_PR_NUMBER`.
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

  All build-failure analysis expertise (binlog parsing, error grouping,
  suggestion authoring) lives in the reusable agent at
  .github/agents/build-failure-analyst.agent.md.
-->
