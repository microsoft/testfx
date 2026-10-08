# Runtime execution contracts

Use this reference with the router's matching upstream prompt. Preserve the
workflow's intended outcome; fix the failing contract rather than rewriting
unrelated workflows. Read the source, its imports, and the generated lock file.
Use documentation matching the repository's pinned gh-aw toolchain and check
local `gh aw <command> --help`; upstream examples can lag runtime capabilities.

## Define the contract before editing

For each supported trigger, fill in this compact table in the working notes or
change description. Record actual commands/tools and credential expressions,
not just "GitHub access". Name the owner of each effect and its failure path.

| Trigger / phase | Tool / token / network | Source / artifact | Output timing / failure contract |
| --- | --- | --- | --- |
| Schedule, event, or manual input -> trusted pre-agent work | Workflow-authored Actions steps; consuming job's token, permissions, and runner egress | Resolve repository, PR or upstream run, attempt, commit, and inputs once | Emit validated inputs and explicit gate reason; required lookup errors stop or report incomplete |
| Resolved input -> agent sandbox | Declared shell/MCP/CLI-proxy tools; read credential; sandbox egress allowlist | Checkout exact source SHA; consume only identified and validated artifacts | Analyze and verify locally; optional missing data is recorded; output requests are queued, not published |
| Agent request -> safe-output application | Declared handler; its scoped credential, target repositories/files/branches, and bounds | Validated payload and patch; handler checkout/base and detection result | Runs after agent completion and applicable checks; may publish, stage, defer, skip, fail, or fall back |
| Prior attempt -> later reconciliation | Read-only lookup in a later run, or explicitly designed deterministic post-application processing | Stable operation key, originating run, intended branch/target, actual GitHub object | Confirm effect before marking published/completed; reconcile partial effects before any retry |

These are execution phases, not a proposal for multiple AI jobs or a built-in
pause/resume protocol. Inspect the generated `needs`, `if`, permissions,
credential wiring, and checkout refs to establish which job owns each phase.
Trusted workflow code must still treat event text, repository content, and
downloaded artifacts as untrusted data. Pass event values through environment
variables and quote them, rather than interpolating them into shell scripts.
Never execute arbitrary PR/fork code in privileged pre-agent or output jobs.

## Separate no-result, optional-data, and error paths

`gh` documents exit `0` as success, `1` as failure, `2` as cancellation, and
`4` as authentication required. Check command-specific exceptions before using
exit codes for control flow. **Exit 4 is not an empty search result.**

A no-result decision requires both a successful query and a valid response
shape. For example, in a trusted Bash step with its own `GH_TOKEN` configured:

```bash
set -euo pipefail
if prs=$(gh pr list --repo "$GITHUB_REPOSITORY" --state open \
  --search 'in:title "[example]"' --json number); then
  count=$(jq -er 'if type == "array" then length else error("expected array") end' <<< "$prs")
else
  rc=$?
  printf 'PR lookup failed (exit %s)\n' "$rc" >&2
  exit "$rc"
fi
# Only a successful query returning [] establishes count=0.
```

Do not hide authentication, authorization, rate-limit, parse, or transport errors
with `2>/dev/null`, `|| true`, `|| echo 0`, or an unqualified "no matches".
Keep useful diagnostics without leaking tokens or signed download URLs.

Classify every input as required or optional before fetching it. Required data
failures must fail the owning step or use the supported incomplete/error path
(for example `report_incomplete` when enabled); do not report successful analysis.
For an optional scrape, catch the failure at that operation, record its status
and reason, and continue with independent evidence. Validate HTTP status,
non-empty content, and schema before parsing it. A failed optional lookup is
"unavailable", not a measured zero or evidence that no findings exist. Avoid a
blanket `continue-on-error` that also swallows failures in required work.

Distinguish a work gate from a whole-run gate. A PR backlog cap may suppress
new PR creation while reporting, maintenance, and reconciliation must continue.
Put that cap around the relevant phase; a top-level `if` or pre-activation skip
suppresses the entire agent. Represent an intentional gate separately from a
failed query, and state whether manual/command triggers bypass it.

## Bind source and artifacts to the same operation

Resolve and carry a single identity tuple: repository, upstream workflow/run ID,
run attempt when applicable, head SHA, artifact name/ID, and module. On
`workflow_run`, do not confuse the consumer's default-branch `github.sha` with
the upstream `github.event.workflow_run.head_sha`. For manual dispatch, either
require an explicit upstream run or define a bounded query for a completed run
of the intended workflow, repository, and branch. Resolve its identity before
downloading anything; never reuse absent `workflow_run` fields.

Download from that exact run, verify artifact availability/expiry and expected
files, then validate report schema, module status, and manifest `sourceCommit`
when provided. Check out the matching head SHA before source analysis or local
verification; verify `git rev-parse HEAD`. Use immutable SHA-based source links.
Do not combine a report from one commit with default-branch source, another
run's artifact, or stale local download files.

A failed/cancelled aggregate can contain a successful module. Use it only when
its manifest proves successful completion and the expected source identity;
report the aggregate and module statuses separately. Missing, expired,
malformed, incompatible, or mismatched artifacts must not trigger edits or
claims based on fresh measurements.

Before queuing a patch, identify the safe-output handler's actual base/checkout.
If it differs from the verified source, reapply and reverify on that base within
the remaining budget, or defer the patch. Account for base movement between
verification and application; do not claim that an untested rebase is verified.
On PR workflows, resolve base/head and merge-base consistently with GitHub's
diff; do not substitute a merge commit for a HEAD-side review anchor.

## Verify tools, credentials, and egress per phase

Permission in one phase does not configure the others:

- Trigger-time queries use `on.permissions` and, when needed, `on.github-token`
  or `on.github-app`. A runner shell step needs its own credential wiring.
- Sandbox GitHub reads use `tools.github` and its credential. A Bash allowlist
  alone does not authenticate `gh`; `mode: gh-proxy` provides mediated reads,
  not arbitrary API/write access. Confirm the requested command is supported.
- `copilot-requests: write` authorizes model inference, not GitHub issue/PR
  mutations. Keep the agent's repository access read-only.
- GitHub writes belong to `safe-outputs` handlers. Configure an exceptional
  token/App on the consuming handler rather than exporting a write token into
  the sandbox or widening every phase. Check repository policy as well as token
  permissions, including upstream/fork installation and head/base access.

For network operations, identify where each request executes: trusted runner,
proxy/MCP service, sandbox, or output job. `network.allowed` governs sandbox
egress; `safe-outputs.allowed-domains` governs URLs in published content, not
download connectivity. API read success does not prove artifact access:
downloads may follow redirects to signed storage hosts, and a proxy may handle
the API request but leave the redirected transfer to the sandbox.

Use the supported `github-actions` ecosystem or the evidenced storage domains
when the sandbox really downloads Actions artifacts. Inspect the compiled
allowlist and blocked-host diagnostics before adding access. Do not add blanket
GitHub/Blob/telemetry wildcards, PATs, or `toolsets: [all]` speculatively. Where
appropriate, download identified inputs in deterministic trusted pre-agent
steps and pass them as data to the sandbox instead. Disable nonessential
telemetry rather than granting egress solely to silence its failure.

## Reconcile effects, not intentions

An agent's safe-output call acknowledges a request, not a created PR/issue or
successful write. Staged outputs do not publish at all. Detection, permissions,
quotas, protected-file restrictions, conflicts, and fallback behavior can
prevent or change the requested effect. Do not poll for a queued creation
inside the same agent execution or invent its number/URL.

Record an attempt as **pending** with a stable key, originating run, intended
branch/target, verified base SHA, and evidence summary. Memory persistence is
not confirmation: its generated job can run independently of safe-output
application. A cursor may record analysis completed, but must not suppress
unpublished work. Use supported temporary IDs only for handler-resolved
same-run references; they are not publication evidence.

On a later run, read the output results and actual GitHub state before marking
an attempt published. Match the durable workflow/operation marker and expected
repository, branch, target, and source identity rather than a mutable title
alone. Read back the body, check its length and a distinctive expected substring,
and verify the patch or other effect relevant to the contract. Reconcile partial
success (for example branch pushed but PR creation failed) and fallback issues
before retrying; repair existing objects rather than creating duplicates.
If reads fail or application is still pending/deferred, preserve uncertainty.
Once failure is confirmed, permit a bounded retry instead of leaving a phantom
"opened PR" in memory forever.

## Bound and exercise the paths

Define caps for candidates, API pages, retries/backoff, verification commands,
agent time/credits, and handler output/patch limits. Retry transient failures
within those bounds; never retry authentication failures by pretending they
are empty results. Reserve time/output capacity for required reporting. Respect
cancellation, avoid indefinite polling, and use concurrency plus reconciliation
to prevent reruns or overlapping triggers from duplicating effects.

| Path | Observable check |
| --- | --- |
| Normal | Exact source/artifact selected; verification succeeds; expected effect is read back after application |
| Empty / duplicate | Successful, valid query proves no work or existing effect; intentional `noop`, no extra publication |
| Backlog / suppressed work | No new PR; required reporting/reconciliation still happens; gate reason is visible |
| Optional data unavailable | Independent work/report survives; missing evidence is explicit, never reported as zero |
| Partial upstream failure | Only identity-validated successful modules are used; aggregate failure remains visible |
| Required failure / cancellation | Auth/API/schema/download failure is not success or empty; no unverified patch or completed publication state |
| Manual / command | Missing event fields are resolved explicitly; same identity and trust checks apply |
| Staged / rejected / deferred output | Pending is not published; later reconciliation handles fallback, partial effects, and bounded retry |

For actual workflow source changes, compile strictly, review the lock-file
diff/job graph, and run the action-pin audit as required by repository guidance.
Then exercise the applicable paths using fixtures for deterministic branches
and, when authorized, representative runs with the real token/proxy/firewall
and safe-output setup. Staging can check payloads, not publication permissions
or actual effects. Record exact commands, selected identities, exits, emitted
requests, application results, and read-back evidence. `gh aw audit <run-id>`
and `gh aw outcomes <run-id>` can assist inspection when supported; neither an
agent exit 0 nor a successful compile proves the intended effect happened.
Do not dispatch a live workflow or publish test objects without authorization.

## References and repository prior art

- [gh exit-code contract](https://cli.github.com/manual/gh_help_exit-codes).
- Upstream [jobs](https://github.com/github/gh-aw/blob/main/.github/aw/jobs.md),
  [runtime safe outputs](https://github.com/github/gh-aw/blob/main/.github/aw/safe-outputs-runtime.md),
  [network](https://github.com/github/gh-aw/blob/main/.github/aw/network.md), and
  [memory](https://github.com/github/gh-aw/blob/main/.github/aw/memory.md):
  read the revision matching the pinned toolchain, not just `main`.
- [Repository compile/pin guidance](../../../workflows/README.md).
- [PR snapshot extraction](../../../workflows/shared/test-reviewer-shared.md):
  resolve base/head, checkout exact head without persisted credentials, and
  compute merge-base and HEAD-side regions.
- [Mutation report consumer](../../../workflows/mutation-test-improver.md):
  carry upstream run/SHA, validate successful modules, keep reporting independent
  of the PR cap, and reconcile pending PR requests.
- [Quality improver state](../../../workflows/repository-quality-improver.md):
  queued issue creation does not supply a known issue number; unpublished
  findings are search hints, not grounds for suppressing future publication.

These are focused patterns, not guarantees that every part of those workflows
is correct. Recheck their current source and compiled behavior before reuse.
