---
description: >-
  Shared `fetch-binlog` job for the Build Failure Analysis workflows. Resolves
  the PR's failed `microsoft.testfx` Azure DevOps build, downloads the binary
  logs that build already produced and stages them for the analysis agent. It
  performs **no build**: it only reads published build artifacts.

# Both Build Failure Analysis workflows — the automatic one (`check_run`) and
# the `/analyze-build-failure` slash command — need exactly the same download
# engine, but they cannot be a single workflow: gh-aw's `roles:` is one
# workflow-scoped gate and the two need different ones. The automatic analysis
# is advisory and must run on **every** failing PR including external
# contributors' (`roles: all`), while the slash command spends the download on
# demand and is restricted to `[admin, maintainer, write]`.
#
# So the job lives here instead and both workflows import it. Only the build/PR
# resolution differs by entry point, and that is a single `if` at the top of the
# script; everything after it — artifact enumeration, missing-leg detection, the
# download/extraction budgets and the fail-closed completeness checks — is
# shared, so a fix lands in both workflows at once.
jobs:
  fetch-binlog:
    name: Fetch binlogs (Azure Pipelines)
    runs-on: ubuntu-latest
    timeout-minutes: 15
    # Cheap pre-gate covering every trigger this job is imported under. Each
    # importing workflow only ever fires one of these branches.
    #
    # `check_run` fires for every check on a commit, so only the
    # `microsoft.testfx` build check reporting failure is acted on. The
    # `workflow_dispatch` clause covers both the automatic workflow's manual
    # rerun entry point and the slash command, which uses
    # `strategy: centralized` and is therefore started by `agentic_commands.yml`
    # via `workflow_dispatch` — the permission step below is what guards that
    # path, since this job runs BEFORE gh-aw's `pre_activation` role check.
    if: >-
      github.event_name == 'workflow_dispatch' ||
      (github.event_name == 'check_run' &&
       github.event.check_run.name == 'microsoft.testfx' &&
       github.event.check_run.conclusion == 'failure')
    permissions:
      contents: read
      pull-requests: read
    outputs:
      binlog-found: ${{ steps.fetch.outputs.binlog-found }}
      pr-number: ${{ steps.fetch.outputs.pr-number }}
      pr-head-sha: ${{ steps.fetch.outputs.pr-head-sha }}
      pr-merge-sha: ${{ steps.fetch.outputs.pr-merge-sha }}
      ado-build-id: ${{ steps.fetch.outputs.ado-build-id }}
      ado-build-url: ${{ steps.fetch.outputs.ado-build-url }}
      missing-legs: ${{ steps.fetch.outputs.missing-legs }}
    steps:
      # Cost + abuse pre-gate for the slash command. gh-aw's own role check
      # (`roles: [admin, maintainer, write]`) lives in the generated
      # `pre_activation` job, and that job is compiled with `needs:
      # fetch-binlog` — so it only runs *after* this job has already pulled
      # every build leg's logs artifact from Azure DevOps (hundreds of MB).
      # Inverting the dependency would create a cycle, so the same permission
      # check is repeated here, before anything is downloaded. `pre_activation`
      # remains the authoritative role + command-position check; this is purely
      # an early-out.
      #
      # Both importing workflows can receive `workflow_dispatch`, so
      # `github.event_name` cannot tell them apart. What does distinguish them
      # is the payload: the automatic workflow is always TOLD a build (a
      # `check_run` names it in `details_url`, its manual dispatch takes a
      # required `ado-build-id`), while the centrally-dispatched slash command
      # carries neither and only names a PR in `aw_context`. So the absence of
      # both build-id sources is exactly "this is the slash command", and that
      # is the condition the gate runs under. `route_slash_command.cjs` in the
      # dispatcher performs no permission check of its own, so without this step
      # any commenter on any PR could trigger the full download.
      #
      # INVARIANT: `build-failure-analysis-command.md` must never gain a
      # `check_run` trigger or an `ado-build-id` dispatch input. Either one
      # would make the command path indistinguishable from the automatic path,
      # and this gate would silently stop running on it. (GitHub drops
      # `workflow_dispatch` inputs a workflow does not declare, so an attacker
      # cannot inject `ado-build-id` from outside — only a change to that file
      # can break this.)
      #
      # Check the `.permission` field. The REST docs for this endpoint say it
      # returns the legacy base roles admin|write|read|none, "where the maintain
      # role is mapped to write and the triage role is mapped to read", so
      # `admin|write` is exactly "has push access or better" — precisely the set
      # `roles: [admin, maintainer, write]` describes, maintainers included.
      # KEEP IN SYNC with that list.
      #
      # `.role_name` is deliberately NOT consulted. It reports "the name of the
      # assigned role, including custom roles", and a custom organization role
      # only has to avoid the base names read/triage/write/maintain/admin — so
      # matching on it would let a role merely *named* like a privileged one
      # (say a custom `maintainer` inheriting read) clear this gate with no push
      # access at all.
      #
      # On any API failure the response carries no `.permission`, so the check
      # falls into the deny branch; failing closed is the safe direction here.
      - name: Verify the commenter has write access
        id: perm
        if: github.event.check_run.details_url == '' && github.event.inputs['ado-build-id'] == ''
        shell: bash
        env:
          GH_TOKEN: ${{ github.token }}
          COMMENTER: ${{ github.event.comment.user.login || fromJSON(github.event.inputs.aw_context || github.event.client_payload.aw_context || '{}').actor }}
        run: |
          set +e
          authorized=false
          # `COMMENTER` has the same untrusted provenance as `PR_NUMBER` below
          # (an `aw_context` payload), and it is interpolated into an API path
          # and into log output, so give it the same shape check. GitHub logins
          # are alphanumerics and hyphens; anything else is a malformed or
          # hostile payload rather than a real account, and is dropped so it
          # lands in the deny branch.
          case "${COMMENTER}" in
            ""|*[!A-Za-z0-9-]*)
              [ -n "${COMMENTER}" ] && echo "::warning::Ignoring implausible actor name from the event payload."
              COMMENTER="" ;;
          esac
          if [ -z "${COMMENTER}" ]; then
            echo "::warning::No actor resolved from the slash-command event / aw_context; skipping the binlog download."
          else
            resp=$(gh api "repos/${GITHUB_REPOSITORY}/collaborators/${COMMENTER}/permission" 2>/dev/null)
            # Extract with `jq` rather than `gh api --jq`: on a non-2xx response
            # `gh` prints the error document to stdout, which `--jq` does not
            # filter, so the raw JSON would land in `perm` and be echoed into
            # the log. Reading the field ourselves yields "" for any error shape.
            perm=$(printf '%s' "${resp}" | jq -r '.permission // empty' 2>/dev/null)
            case "${perm}" in
              admin|write) authorized=true ;;
              *)           authorized=false ;;
            esac
            if [ "${authorized}" = "true" ]; then
              echo "'${COMMENTER}' has '${perm}' access to ${GITHUB_REPOSITORY}; proceeding."
            else
              echo "::warning::'${COMMENTER}' does not have write access to ${GITHUB_REPOSITORY} (resolved permission '${perm:-none}'); skipping the binlog download."
            fi
          fi
          echo "authorized=${authorized}" >> "$GITHUB_OUTPUT"

      - name: Download binlogs from the failed Azure Pipelines build
        id: fetch
        # The gate above only runs for the slash command; when the build is
        # named by a check payload or a dispatch input it is skipped and its
        # output is empty, so the first two clauses let those paths through
        # unchanged.
        if: github.event.check_run.details_url != '' || github.event.inputs['ado-build-id'] != '' || steps.perm.outputs.authorized == 'true'
        shell: bash
        env:
          GH_TOKEN: ${{ github.token }}
          GH_AW_REPO: ${{ github.repository }}
          ADO_API: "https://dev.azure.com/dnceng-public/public/_apis"
          ADO_BUILD_UI: "https://dev.azure.com/dnceng-public/public/_build/results"
          # microsoft.testfx pipeline definition id in dnceng-public/public
          # (used to validate the resolved build belongs to the right pipeline).
          ADO_BUILD_DEFINITION_ID: "209"
          # `check_run` payload.
          CHECK_DETAILS_URL: ${{ github.event.check_run.details_url }}
          CHECK_PR_NUMBER: ${{ github.event.check_run.pull_requests[0].number }}
          # `workflow_dispatch` inputs, read from `github.event.inputs` rather
          # than the `inputs` context: `inputs` is typed per workflow, while
          # `github.event.inputs` is simply absent (empty) when the importing
          # workflow does not declare that input, so one shared job can
          # reference both entry points.
          DISPATCH_BUILD_ID: ${{ github.event.inputs['ado-build-id'] }}
          DISPATCH_PR_NUMBER: ${{ github.event.inputs['pr-number'] }}
          # Slash-command payload (`aw_context` when the command is routed
          # through the central dispatcher, `issue.number` when inline).
          COMMAND_PR_NUMBER: ${{ github.event.issue.number || fromJSON(github.event.inputs.aw_context || github.event.client_payload.aw_context || '{}').item_number }}
        run: |
          # Advisory + best-effort: on any gap emit binlog-found=false and the
          # agent pipeline stays inert.
          set +e
          set +o pipefail
          emit_none() { echo "binlog-found=false" >> "$GITHUB_OUTPUT"; exit 0; }

          # --- 1. Resolve the Azure DevOps build and the PR it belongs to ---
          # This is the ONLY part of the job that differs between the two
          # workflows, because they learn about the build in opposite
          # directions:
          #
          #   * `check_run` / manual dispatch are TOLD which build to look at —
          #     the check payload names it in `details_url`, a manual dispatch
          #     passes it explicitly — so the build is resolved first and the PR
          #     is derived from it.
          #   * The slash command is told nothing about a build: it is a request
          #     to re-analyse whatever the PR's newest build is, so the PR comes
          #     first and the build is looked up from it. That build is usable
          #     only once it has COMPLETED; a still running newest build (e.g.
          #     right after a force-push) would otherwise pair an older failure
          #     with the PR's current head.
          #
          # Both importing workflows can receive `workflow_dispatch` (the slash
          # command uses `strategy: centralized`), so the branch is taken on the
          # payload rather than on the event name: having a build id at all is
          # what makes this the automatic workflow.
          if [ -n "${DISPATCH_BUILD_ID}" ] || [ -n "${CHECK_DETAILS_URL}" ]; then
            ENTRY="build"
            if [ -n "${DISPATCH_BUILD_ID}" ]; then
              BUILD_ID="${DISPATCH_BUILD_ID}"
            else
              # details_url looks like: .../_build/results?buildId=NNN&view=...
              BUILD_ID=$(printf '%s' "${CHECK_DETAILS_URL}" | grep -oE 'buildId=[0-9]+' | head -1 | cut -d= -f2)
            fi
            echo "Azure DevOps build id: '${BUILD_ID}'"
            [ -z "${BUILD_ID}" ] && { echo "::warning::Could not resolve an ADO build id."; emit_none; }
            # The build id feeds directly into ADO API URLs below; require it to
            # be purely numeric (esp. on workflow_dispatch, where it is free-form
            # input) so a malformed value can't alter the request path/query.
            if ! printf '%s' "${BUILD_ID}" | grep -qE '^[0-9]+$'; then
              echo "::warning::Resolved ADO build id '${BUILD_ID}' is not numeric; refusing."; emit_none
            fi
            # The build metadata is the authoritative source for the PR number
            # (via sourceBranch) as well as for the definition / result /
            # revision validated in step 3.
            build_json=$(curl -sSL --retry 3 "${ADO_API}/build/builds/${BUILD_ID}?api-version=7.1")
            # A PR build's sourceBranch is exactly `refs/pull/<n>/merge`, so it
            # identifies the PR unambiguously — unlike the commit->PRs API,
            # which can return several PRs in an unspecified order.
            BUILD_PR_NUM=$(printf '%s' "${build_json}" | jq -r '.sourceBranch // empty' | sed -n 's#^refs/pull/\([0-9]\{1,\}\)/merge$#\1#p')
            if [ -n "${DISPATCH_BUILD_ID}" ]; then
              PR_NUMBER="${DISPATCH_PR_NUMBER}"
            else
              # Prefer the PR named by the build's own sourceBranch
              # (authoritative) over check_run.pull_requests[0], whose order
              # isn't guaranteed and can name a different PR sharing the commit.
              PR_NUMBER="${BUILD_PR_NUM:-${CHECK_PR_NUMBER}}"
            fi
            [ -z "${PR_NUMBER}" ] && { echo "::warning::Could not resolve a PR number."; emit_none; }
            # PR_NUMBER feeds `gh api .../pulls/<n>` and the `refs/pull/<n>/merge`
            # comparison; require it numeric so a malformed value can't reach the
            # GitHub API path (traversal-like input) or skew the branch match.
            if ! printf '%s' "${PR_NUMBER}" | grep -qE '^[0-9]+$'; then
              echo "::warning::Resolved PR number '${PR_NUMBER}' is not numeric; refusing."; emit_none
            fi
          else
            ENTRY="command"
            PR_NUMBER="${COMMAND_PR_NUMBER}"
            [ -z "${PR_NUMBER}" ] && { echo "::warning::No PR number resolved from the slash-command event / aw_context."; emit_none; }
            # PR_NUMBER feeds GitHub API paths and the `refs/pull/<n>/merge`
            # branch query; require it numeric so a malformed event/aw_context
            # payload can't reach those URLs with unexpected content.
            if ! printf '%s' "${PR_NUMBER}" | grep -qE '^[0-9]+$'; then
              echo "::warning::Resolved PR number '${PR_NUMBER}' is not numeric; refusing."; emit_none
            fi
            # Newest build for the PR's merge ref REGARDLESS of status
            # (queue-time descending), so a build queued after an older failure
            # is seen rather than the stale one being analysed silently.
            builds_json=$(curl -sSL --retry 3 \
              "${ADO_API}/build/builds?definitions=${ADO_BUILD_DEFINITION_ID}&branchName=refs/pull/${PR_NUMBER}/merge&queryOrder=queueTimeDescending&\$top=1&api-version=7.1")
            BUILD_ID=$(printf '%s' "${builds_json}" | jq -r '.value // [] | .[0].id // empty')
            BUILD_STATUS=$(printf '%s' "${builds_json}" | jq -r '.value // [] | .[0].status // empty')
            echo "Newest microsoft.testfx build for PR #${PR_NUMBER}: id='${BUILD_ID}' status='${BUILD_STATUS}'"
            [ -z "${BUILD_ID}" ] && { echo "::warning::No microsoft.testfx build found for PR #${PR_NUMBER}."; emit_none; }
            # Require a numeric build id before it feeds subsequent ADO API
            # URLs, so a malformed query response can't inject path/query.
            if ! printf '%s' "${BUILD_ID}" | grep -qE '^[0-9]+$'; then
              echo "::warning::ADO build id '${BUILD_ID}' is not numeric; refusing."; emit_none
            fi
            if [ "${BUILD_STATUS}" != "completed" ]; then
              echo "::warning::PR #${PR_NUMBER}'s newest microsoft.testfx build (${BUILD_ID}) is still '${BUILD_STATUS}'; wait for it to finish before analysing."
              emit_none
            fi
            build_json=$(curl -sSL --retry 3 "${ADO_API}/build/builds/${BUILD_ID}?api-version=7.1")
          fi
          RESULT=$(printf '%s' "${build_json}" | jq -r '.result // empty')
          DEF_ID=$(printf '%s' "${build_json}" | jq -r '.definition.id // empty')
          SRC_BRANCH=$(printf '%s' "${build_json}" | jq -r '.sourceBranch // empty')

          # --- 2. Scope check: only analyse PRs targeting main / rel/* ---
          PR_JSON=$(gh api "repos/${GH_AW_REPO}/pulls/${PR_NUMBER}" 2>/dev/null)
          BASE_REF=$(printf '%s' "${PR_JSON}" | jq -r '.base.ref // empty')
          case "${BASE_REF}" in
            main|rel/*) echo "PR #${PR_NUMBER} base '${BASE_REF}' is in scope." ;;
            *) echo "::warning::PR #${PR_NUMBER} base '${BASE_REF}' is out of scope (main, rel/*); skipping."; emit_none ;;
          esac

          # --- 3. Validate the build, whichever way it was resolved ---
          # It must be the microsoft.testfx definition (209), have failed, and
          # belong to this PR (sourceBranch == refs/pull/<PR>/merge). No entry
          # point is fully trusted: `check_run` parses the build id out of a
          # check payload, dispatch takes the build id and PR number as
          # independent free-form inputs, and the slash command derives the
          # build from a query. Validating here — rather than per entry point —
          # prevents downloading an unrelated build or posting its analysis to
          # the wrong PR.
          echo "ADO build ${BUILD_ID}: result='${RESULT}' definition='${DEF_ID}' sourceBranch='${SRC_BRANCH}'"
          if [ "${DEF_ID}" != "${ADO_BUILD_DEFINITION_ID}" ]; then
            echo "::warning::ADO build ${BUILD_ID} is definition '${DEF_ID}', not microsoft.testfx (${ADO_BUILD_DEFINITION_ID}); refusing."; emit_none
          fi
          if [ "${RESULT}" != "failed" ]; then
            if [ "${ENTRY}" = "command" ]; then
              echo "::warning::PR #${PR_NUMBER}'s newest microsoft.testfx build (${BUILD_ID}) result is '${RESULT}', not failed — the failure looks resolved; nothing to analyse."
            else
              echo "::warning::ADO build ${BUILD_ID} did not fail (result='${RESULT}'); nothing to analyze."
            fi
            emit_none
          fi
          if [ "${SRC_BRANCH}" != "refs/pull/${PR_NUMBER}/merge" ]; then
            echo "::warning::ADO build ${BUILD_ID} sourceBranch '${SRC_BRANCH}' does not match PR #${PR_NUMBER} (refs/pull/${PR_NUMBER}/merge); refusing to avoid posting to the wrong PR."; emit_none
          fi

          # Require the build's analyzed revision to equal the PR's CURRENT
          # head. gh-aw safe-output review comments carry no `commit_id` — they
          # target the current PR diff — so analyzing a stale revision would
          # produce inline suggestions that get rejected or land on the wrong
          # lines. The PR can advance between selecting the build and reaching
          # this point, so the head is re-read here rather than reused from the
          # scope check above.
          BUILD_PR_SHA=$(printf '%s' "${build_json}" | jq -r '.triggerInfo["pr.sourceSha"] // empty')
          # ADO builds GitHub's `refs/pull/<n>/merge` ref, so build_json.sourceVersion
          # is the merge commit GitHub produced at build time and equals the PR's
          # `merge_commit_sha` then. If the base branch advances (even with the PR
          # head unchanged) GitHub recomputes that merge and merge_commit_sha
          # changes, so this catches base-advance staleness the head check misses.
          BUILD_MERGE_SHA=$(printf '%s' "${build_json}" | jq -r '.sourceVersion // empty')
          # Re-read the PR rather than reusing the snapshot from the scope check:
          # selecting the build costs an ADO round trip, and right after a
          # force-push the newest-build query can still return the previous
          # failed build. The point of this check is to skip BEFORE paying for
          # the download, so it should compare against the freshest head
          # available. A post-download re-read below independently catches a
          # head that moves while the artifacts are being fetched.
          PR_JSON=$(gh api "repos/${GH_AW_REPO}/pulls/${PR_NUMBER}" 2>/dev/null)
          CURRENT_HEAD=$(printf '%s' "${PR_JSON}" | jq -r '.head.sha // empty')
          CURRENT_MERGE=$(printf '%s' "${PR_JSON}" | jq -r '.merge_commit_sha // empty')
          # Fail CLOSED: if either the build's analyzed revision or the current
          # PR head can't be resolved, skip — we must not analyze a possibly
          # stale binlog against the current diff (inline comments have no
          # commit_id and target the current PR diff).
          if [ -z "${BUILD_PR_SHA}" ] || [ -z "${CURRENT_HEAD}" ]; then
            echo "::warning::Could not resolve build revision ('${BUILD_PR_SHA}') and/or current PR head ('${CURRENT_HEAD}'); skipping to avoid analyzing a stale binlog against the current diff."
            emit_none
          fi
          if [ "${BUILD_PR_SHA}" != "${CURRENT_HEAD}" ]; then
            echo "::warning::Build ${BUILD_ID} analyzed revision '${BUILD_PR_SHA}' but PR #${PR_NUMBER} head is now '${CURRENT_HEAD}'; skipping stale build (a newer build/check will cover the current revision)."
            emit_none
          fi
          # When both merge revisions are known and differ, the base branch moved
          # since the build — the binlog reflects an obsolete merge. Skip.
          if [ -n "${BUILD_MERGE_SHA}" ] && [ -n "${CURRENT_MERGE}" ] && [ "${BUILD_MERGE_SHA}" != "${CURRENT_MERGE}" ]; then
            echo "::warning::Build ${BUILD_ID} merge revision '${BUILD_MERGE_SHA}' but PR #${PR_NUMBER} current merge is '${CURRENT_MERGE}' (base branch advanced); skipping stale merge."
            emit_none
          fi
          # Consistent now: build revision == current PR head. Use it for
          # permalinks so they line up with the inline comments' diff target.
          HEAD_SHA="${CURRENT_HEAD}"
          echo "Analyzing build ${BUILD_ID} at PR head revision '${HEAD_SHA}'."

          # --- 4. Download every Logs_Build_* artifact and extract binlogs ---
          artifacts_json=$(curl -sSL --retry 3 "${ADO_API}/build/builds/${BUILD_ID}/artifacts?api-version=7.1")
          mapfile -t names < <(printf '%s' "${artifacts_json}" | jq -r '.value // [] | map(select(.name | test("^Logs_Build_"))) | .[].name')
          [ "${#names[@]}" -eq 0 ] && { echo "::warning::No Logs_Build_* artifacts on build ${BUILD_ID}."; emit_none; }

          # --- 5a. Which failed legs never published logs at all? ---
          # The fail-closed check further down compares staged legs against the
          # artifacts ADO *returned*, so it cannot see a leg that died before
          # publishing its logs artifact — that leg is simply absent from
          # `names`. Ask the timeline instead. This is advisory rather than
          # fail-closed: a failed job that legitimately publishes no logs would
          # otherwise suppress analysis of a real compile break in the same build. The agent is told about the gap so it cannot conclude
          # "no build failure" from the legs that happened to upload.
          #
          # Ask the timeline whether each leg's log *publish* succeeded rather
          # than guessing its artifact name from its display name. The artifact
          # is named from `$(Agent.Os)`, which is not what the job is called:
          # `MacOS Debug` publishes `Logs_Build_Darwin_Debug`, and
          # `WindowsSamples Debug` is named from `$(Agent.JobName)` instead. Name
          # matching reported those healthy legs as missing on real builds —
          # every macOS failure, and every `msbuild_cache_seed` job. Arcade's
          # `Publish logs` task record answers the question directly, so no
          # spelling has to be inferred. A failed job carrying no such task
          # (the `Detect changed paths` classifier, the cache-seed stage) does
          # not publish logs at all and is not treated as a missing leg.
          #
          # `canceled` and `abandoned` legs count alongside `failed`: they also
          # finish without logs, and are a real gap in the artifact set.
          timeline_json=$(curl -sSL --retry 3 --max-time 60 "${ADO_API}/build/builds/${BUILD_ID}/timeline?api-version=7.1" 2>/dev/null || true)
          MISSING_LEGS=""
          # An unreadable timeline must not look like a complete build. A failed
          # request, a non-JSON error page and an ADO error document all left
          # the list empty, which is exactly how "every failed leg published
          # logs" is reported — so a transient outage could let the agent
          # conclude "non-build failure" from an artifact set whose completeness
          # was never established. Probe for the `records` array first and
          # report an explicit unknown when it isn't there.
          timeline_ok=0
          if printf '%s' "${timeline_json}" | jq -e 'type == "object" and has("records")' >/dev/null 2>&1; then
            timeline_ok=1
          fi
          if [ "${timeline_ok}" -eq 1 ]; then
            # Job display names come from the pipeline YAML in the PR branch, so
            # on a fork PR they are attacker-controlled. Strip control characters
            # and bound the length before this value reaches `$GITHUB_OUTPUT` and
            # `$GITHUB_ENV`, where an embedded newline would inject further
            # `key=value` lines. The task name is matched on its alphanumerics
            # because arcade spells it both `Publish logs` and `Publish Logs`,
            # and some pipelines prefix a decorative emoji.
            MISSING_LEGS=$(printf '%s' "${timeline_json}" | jq -r '
              (.records // []) as $records
              | ($records
                 | map(select(.type == "Task"
                              and (.name | ascii_downcase | gsub("[^a-z0-9]"; "") | test("publishlogs"))))) as $publishes
              | $records
              | map(select(.type == "Job"
                           and (.result == "failed" or .result == "canceled" or .result == "abandoned")))
              | map(. as $job
                    | ($publishes | map(select(.parentId == $job.id))) as $mine
                    | select(($mine | length) > 0
                             and (($mine | map(select(.result == "succeeded")) | length) == 0))
                    | ($job.name | gsub("[[:cntrl:]]"; " ")))
              | join(", ")' 2>/dev/null | tr -d '\r\n' | cut -c1-400)
          fi
          if [ "${timeline_ok}" -ne 1 ]; then
            MISSING_LEGS="(unknown - could not read the build timeline)"
            echo "::warning::Could not read the timeline for build ${BUILD_ID}; unable to verify that every failed leg published a logs artifact."
          elif [ -n "${MISSING_LEGS}" ]; then
            echo "::warning::Failed leg(s) whose logs were never published: ${MISSING_LEGS}"
          fi

          # Guards for untrusted PR-produced archives: cap the compressed
          # download and the reported uncompressed size per artifact, bound
          # extraction time, AND enforce cumulative budgets across all legs so
          # many individually-small artifacts can't collectively exhaust the
          # runner's disk or its network time.
          MAX_ZIP_BYTES=524288000       # 500 MB compressed per artifact
          MAX_UNZIP_BYTES=2147483648    # 2 GB uncompressed per artifact
          MAX_TOTAL_BYTES=4294967296    # 4 GB uncompressed across all artifacts
          MAX_TOTAL_ZIP_BYTES=3221225472 # 3 GB compressed downloaded in total
          MAX_ARTIFACTS=40              # cap only; the real count is path-dependent
          TOTAL_BYTES=0
          TOTAL_ZIP_BYTES=0
          # Bound the work before starting: a pipeline change (or repeated leg
          # retries) could grow the matched set well past today's count. Refuse
          # rather than process a prefix of the list, because a partial view is
          # exactly what the fail-closed check below exists to prevent.
          if [ "${#names[@]}" -gt "${MAX_ARTIFACTS}" ]; then
            echo "::warning::Build ${BUILD_ID} matched ${#names[@]} log artifacts, above the ${MAX_ARTIFACTS} cap; skipping."
            emit_none
          fi
          mkdir -p /tmp/binlogs
          count=0
          staged_legs=0
          ai=0
          for name in "${names[@]}"; do
            # `name` is PR-controlled ADO artifact metadata and the
            # `^Logs_Build_` filter only anchors the prefix, so sanitize it
            # before using it in any on-disk path (guards against `/` or `..`
            # traversal); keep the original `name` for the artifacts_json lookup.
            safe_name=$(printf '%s' "${name}" | tr -c 'A-Za-z0-9._-' '_')
            ai=$((ai + 1))
            url=$(printf '%s' "${artifacts_json}" | jq -r --arg n "${name}" '.value[] | select(.name==$n) | .resource.downloadUrl // empty')
            [ -z "${url}" ] && { echo "::warning::No download URL for ${name}."; continue; }
            rm -rf /tmp/ax /tmp/a.zip
            mkdir -p /tmp/ax
            # Hard-cap the bytes written to disk regardless of Content-Length:
            # stream through `head -c` (cap + 1) and bound total time. This
            # closes the gap where `curl --max-filesize` alone would let a
            # length-less response write unbounded data before any post-check.
            curl -sSL --retry 3 --max-time 300 "${url}" 2>/dev/null | head -c $((MAX_ZIP_BYTES + 1)) > /tmp/a.zip || true
            ZIP_BYTES=$(stat -c%s /tmp/a.zip 2>/dev/null || echo 0)
            # Bound cumulative *compressed* bytes too: the per-artifact and
            # cumulative-uncompressed caps still allow many mid-sized archives
            # to be pulled over the network before any of them is inspected.
            #
            # Charge the budget here, before the skips below, because the bytes
            # are already on the wire by this point — `curl` above streams into
            # `head -c` and only then is the size known. Charging after the
            # per-artifact skip would let every oversized artifact cost a full
            # MAX_ZIP_BYTES of network without ever being counted, so a build of
            # MAX_ARTIFACTS oversized legs would download far past this budget
            # while appearing to stay inside it.
            TOTAL_ZIP_BYTES=$((TOTAL_ZIP_BYTES + ZIP_BYTES))
            if [ "${TOTAL_ZIP_BYTES}" -gt "${MAX_TOTAL_ZIP_BYTES}" ]; then
              echo "::warning::Cumulative compressed download budget ${MAX_TOTAL_ZIP_BYTES} reached at ${name}; stopping."; break
            fi
            if [ "${ZIP_BYTES}" -eq 0 ]; then
              echo "::warning::Skipping ${name}: empty or failed download."; continue
            fi
            if [ "${ZIP_BYTES}" -gt "${MAX_ZIP_BYTES}" ]; then
              echo "::warning::Skipping ${name}: download exceeded ${MAX_ZIP_BYTES} bytes."; continue
            fi
            UNCOMP=$(unzip -l /tmp/a.zip 2>/dev/null | tail -1 | awk '{print $1}')
            # Fail safe: if the uncompressed size isn't a plain integer (corrupt
            # zip / unexpected `unzip -l` output), we can't verify it — skip the
            # artifact rather than let a non-numeric value bypass the `-gt` guard.
            if ! printf '%s' "${UNCOMP}" | grep -qE '^[0-9]+$'; then
              echo "::warning::Skipping ${name}: could not determine uncompressed size (unparseable unzip output)."; continue
            fi
            # ZIP64 uncompressed sizes can reach ~20 digits — beyond Bash's
            # signed 64-bit range, where `-gt` (and the cumulative `$((...))`
            # below) error out and, under `set +e`, would let an oversized
            # archive slip past the guard. Any value with more digits than the
            # limit is unambiguously larger, so reject on decimal length first;
            # after this, UNCOMP fits safely in the integer range used below.
            if [ "${#UNCOMP}" -gt "${#MAX_UNZIP_BYTES}" ]; then
              echo "::warning::Skipping ${name}: uncompressed size has ${#UNCOMP} digits, exceeding the ${MAX_UNZIP_BYTES} guard (possible zip bomb)."; continue
            fi
            if [ "${UNCOMP}" -gt "${MAX_UNZIP_BYTES}" ]; then
              echo "::warning::Skipping ${name}: uncompressed size ${UNCOMP} exceeds ${MAX_UNZIP_BYTES} guard (possible zip bomb)."; continue
            fi
            if [ $((TOTAL_BYTES + UNCOMP)) -gt "${MAX_TOTAL_BYTES}" ]; then
              echo "::warning::Cumulative uncompressed budget ${MAX_TOTAL_BYTES} reached at ${name}; stopping extraction."; break
            fi
            # Refuse the archive if any entry path is absolute or has a `..`
            # component (defense-in-depth over unzip's own traversal guard),
            # then extract `*.binlog` entries *preserving* their in-archive
            # paths (no `-j`) under a fresh dir + timeout, so two binlogs that
            # share a basename in different folders don't overwrite each other.
            if unzip -Z1 /tmp/a.zip 2>/dev/null | grep -qE '(^/|(^|/)\.\.(/|$))'; then
              echo "::warning::Skipping ${name}: archive has a suspicious (absolute or ..) entry path."; continue
            fi
            # `unzip` exit 11 means "no files matched" — the artifact carries no
            # binlog at all. That is not an extraction failure: the leg did
            # publish its logs, they simply contain no binlog, and the
            # fail-closed check below already accounts for a leg that staged
            # nothing. Reporting it as "extraction failed or timed out" sends
            # the reader chasing a corrupt-archive theory that isn't there. Any
            # other non-zero exit (corrupt archive, timeout) is a real failure.
            #
            # Both cases `continue`, so nothing was written to /tmp/ax and the
            # uncompressed budget below is left untouched. Charging it for an
            # archive that extracted nothing would let one large binlog-free
            # artifact push a genuinely useful later leg past MAX_TOTAL_BYTES.
            uz=0
            timeout 120 unzip -o /tmp/a.zip '*.binlog' -d /tmp/ax >/dev/null 2>&1 || uz=$?
            if [ "${uz}" -eq 11 ]; then
              echo "::warning::${name}: published logs contain no binlog; nothing to analyse from this leg."; continue
            fi
            if [ "${uz}" -ne 0 ]; then
              echo "::warning::Skipping ${name}: extraction failed or timed out (unzip exit ${uz})."; continue
            fi
            # Consume the cumulative budget only once the archive actually
            # extracted — not on a suspicious-path or extraction-failure skip
            # above — so a skipped leg can't wrongly exhaust the budget and
            # force later legs to be dropped as "incomplete".
            TOTAL_BYTES=$((TOTAL_BYTES + UNCOMP))
            i=0
            leg_staged=0
            while IFS= read -r bl; do
              [ -f "${bl}" ] || continue
              # Every destination is uniquely prefixed with the artifact index
              # (`ai`) and a per-file counter (`i`), so neither a cross-artifact
              # sanitize collision nor same-basename entries within one archive
              # can overwrite a previously staged leg's binlog. `safe_name` is
              # kept only for readability.
              dest="/tmp/binlogs/${ai}_${i}_${safe_name}.binlog"
              # Only count a staged binlog when the copy actually succeeds —
              # `set +e` is on, so a failed `cp` must not inflate the counts.
              if cp "${bl}" "${dest}"; then
                count=$((count + 1))
                i=$((i + 1))
                leg_staged=1
              else
                echo "::warning::Failed to stage ${bl}; skipping."
              fi
            done < <(find /tmp/ax -type f -name '*.binlog')
            # This leg produced at least one usable binlog.
            [ "${leg_staged}" -eq 1 ] && staged_legs=$((staged_legs + 1))
          done
          echo "Extracted ${count} binlog(s) from ${staged_legs}/${#names[@]} legs into /tmp/binlogs:"
          ls -la /tmp/binlogs || true
          [ "${count}" -eq 0 ] && { echo "::warning::No *.binlog found in any Logs_Build_* artifact of build ${BUILD_ID}."; emit_none; }
          # Fail CLOSED on a partial set: if any Logs_Build_* leg did not yield
          # a usable binlog (download/extract failure, size-guard skip, or no
          # binlog inside), we cannot see every leg. Activating anyway would let
          # the agent treat the retrieved legs as the whole build and possibly
          # mis-classify a real build break in a missing leg as a clean compile /
          # non-build failure. A later build/check will re-trigger the analysis.
          if [ "${staged_legs}" -ne "${#names[@]}" ]; then
            echo "::warning::Only ${staged_legs} of ${#names[@]} Logs_Build_* legs produced a usable binlog; skipping to avoid analyzing an incomplete build (a missing leg could be the one that failed)."
            emit_none
          fi

          # The download/extract loop above can take minutes. Re-read the PR
          # head right before activating and fail CLOSED if it moved or can't
          # be resolved: a force-push during that window would otherwise leave
          # the analyzed binlog stale relative to the current diff (inline
          # comments carry no commit_id and target the current diff).
          LATEST_PR=$(gh api "repos/${GH_AW_REPO}/pulls/${PR_NUMBER}" 2>/dev/null)
          LATEST_HEAD=$(printf '%s' "${LATEST_PR}" | jq -r '.head.sha // empty')
          LATEST_MERGE=$(printf '%s' "${LATEST_PR}" | jq -r '.merge_commit_sha // empty')
          if [ -z "${LATEST_HEAD}" ] || [ "${LATEST_HEAD}" != "${HEAD_SHA}" ]; then
            echo "::warning::PR #${PR_NUMBER} head changed during artifact download ('${HEAD_SHA}' -> '${LATEST_HEAD}') or could not be re-resolved; skipping to avoid posting stale-build suggestions against the new diff."
            emit_none
          fi
          # The base branch may also have advanced during the download; if the
          # merge revision moved from what the build analyzed, skip (stale merge).
          if [ -n "${BUILD_MERGE_SHA}" ] && [ -n "${LATEST_MERGE}" ] && [ "${LATEST_MERGE}" != "${BUILD_MERGE_SHA}" ]; then
            echo "::warning::PR #${PR_NUMBER} merge revision changed during artifact download ('${BUILD_MERGE_SHA}' -> '${LATEST_MERGE}'); skipping stale merge."
            emit_none
          fi

          {
            # `missing-legs` is derived from ADO job display names, which come
            # from pipeline YAML in the PR branch and are therefore
            # fork-controlled. It is sanitized where it is assembled, and it is
            # written first here so that even a future regression in that
            # sanitizing cannot let it override a key emitted below.
            echo "missing-legs=${MISSING_LEGS}"
            echo "binlog-found=true"
            echo "pr-number=${PR_NUMBER}"
            echo "pr-head-sha=${HEAD_SHA}"
            echo "pr-merge-sha=${BUILD_MERGE_SHA}"
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
          # Quoted so the import's YAML round-trip keeps it `1` — an unquoted
          # integer comes back out of the shared-job merge as `1.0`.
          retention-days: "1"
---
