---
name: session-delivery-hygiene
description: Gate local-session scope, persistent delivery, live PR state, integration provenance, and owned cleanup. Use before implementing guidance changes, changing or pushing PR work, diagnosing package/output mismatches, or handing off implementation.
---

# Session delivery hygiene

Read [repository instructions](../../copilot-instructions.md) first. Apply only
the gates relevant to the task's risks; this skill grants no additional scope,
publication permission, or authority over another session.

## Risk-based gates

1. **Outcome and ownership.** Classify the request as analysis, implementation,
   publication, or an explicitly requested combination. Improving reviewer or
   workflow guidance means editing that guidance, not running or posting a
   review. Preserve the requested repository, branch, paths, and exclusions.
   Confirm the active worktree and existing changes before editing; do not
   overwrite unrelated work or alter user-global settings or other sessions.
2. **Persistent delivery.** For implementation, inspect the actual final files
   and Git diff, including staged and untracked files, against each requested
   outcome. Inspect commits too when the worktree is clean. A plan, status
   update, successful command, or agent's completion message is not a delivered
   change. Validate the changed artifact itself; for guidance, check skill
   metadata, local links, examples, and consistency with existing instructions.
   If no change is needed, explain the evidence rather than claiming a fix.
3. **Live PR destination.** Immediately before editing PR-linked work or
   pushing it, read the PR's live state, base/head repositories and refs, and
   head SHA; confirm the destination still matches the requested work. Recheck
   before publication if the state may have changed. Never push to or recreate
   a merged PR's old head branch. Use a new follow-up branch and, only when
   publication is authorized, a new PR targeting the intended base branch;
   do not infer that target from a local checkout or default to `main`.
   For a closed unmerged PR, stop publication unless reopening or a follow-up
   is authorized. If live state cannot be read, do not push based on stale
   session metadata. Local commits do not prove delivery to a live PR.
4. **Integration provenance.** Before diagnosing stale-package or launch
   failures, identify the exact consumer checkout, configuration, TFM/RID,
   executable/output layout, producer commit, and package ID/version. Inspect
   the consumer's effective NuGet configuration, `NUGET_PACKAGES` or
   `RestorePackagesPath`, and resolved paths in `project.assets.json`; a global
   cache listing does not identify a repository-local restore. For packaged
   apps, verify package identity/version, registration install location, and
   the launched process/loaded binary paths belong to the output under test.
   Establish or correct that provenance before concluding the implementation
   is defective; rebuilding elsewhere is not proof the consumer used it.
5. **Owned cleanup.** Track only resources created or temporarily changed for
   this task: process IDs and executable paths, exact package registrations and
   install locations, temporary files, and prior override values. Reconfirm
   ownership before cleanup; stop processes by specific PID, remove only
   task-owned registrations/files, and restore temporary overrides to their
   prior values. Never kill by process name, unregister all matching apps,
   clear entire shared/global caches, or recursively delete broad directories.
   Prefer isolated outputs/caches; even a targeted cache eviction requires
   proof of the consumer's actual path and ownership. Leave borrowed resources
   untouched and report any owned cleanup that could not be completed.

## Evidence-based handoff

Lead with the implemented outcome or concrete blocker, then name changed
artifacts and their actual delivery state. These states are not interchangeable:

| State | Required evidence |
| --- | --- |
| Local | Final file/diff exists in the identified worktree. |
| Committed | Commit SHA contains the intended changes. |
| Pushed | Read the destination remote ref and verify it contains that commit. |
| Published | Read the live PR/comment/other object and verify the intended content and current head where applicable. |
| CI-verified | Relevant checks passed for the exact published head SHA, not an earlier or merged PR. |

Include the decisive validation result and any remaining blocker or owned
cleanup. Do not label local validation as CI, or a push as publication. Existing
GitHub-body readback and composed-product validation requirements still apply.
Finish the requested work without status-only handoffs, invented follow-up
tasks, or unsolicited publication.
