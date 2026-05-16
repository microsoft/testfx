---
# Shared configuration for expert-review workflows.
#
# Imported by review.agent.md (slash command) and review-on-open.agent.md
# (pull request opened). Keeps permissions, tools, and safe-outputs
# in one place.

description: "Shared configuration for expert-review workflows"

permissions:
  contents: read
  pull-requests: read

tools:
  cache-memory:
    - id: repo-history
      key: repo-history  # shared cache produced by the repo-historian workflow
  github:
    toolsets: [pull_requests, repos]

safe-outputs:
  create-pull-request-review-comment:
    max: 30
  submit-pull-request-review:
    max: 1
    allowed-events: [COMMENT, REQUEST_CHANGES]
  add-comment:
    max: 5
  # NOTE: Consumers must also define this explicitly until workflow import/merge
  # preserves `report-as-issue: false` in compiled lock files.
  noop:
    report-as-issue: false
---

# Expert Code Review

Review pull request #${{ github.event.pull_request.number || github.event.issue.number }} using the `expert-reviewer` agent defined at `.github/agents/expert-reviewer.agent.md`.

## Instructions

1. Fetch the full diff for the pull request.
2. Call the `expert-reviewer` agent as a **background** task (`task` tool, `agent_type: "general-purpose"`, `model: "claude-opus-4.6"`, `mode: "background"`). Include the PR number, repository owner/name, and the full diff content in the subagent prompt. Also remind the subagent in its prompt that the `submit_pull_request_review` safe-output only accepts `event: "COMMENT"` or `event: "REQUEST_CHANGES"` — `APPROVE` is not allowed and will cause the entire review to be dropped.
3. **Immediately after launching the background task** — do NOT wait for it to finish and do NOT read its result — call `noop` with a brief status message such as `"Expert-reviewer launched in background for PR #N. It will post the review directly."`. Then stop. The subagent has direct access to the safe-output tools and will post its own review (`create_pull_request_review_comment`, `add_comment`, `submit_pull_request_review`) without any further action from you.

> **Important**: Reading the background agent result would pull its entire conversation (2+ million tokens from spawning 21 dimension sub-agents) into your context, causing a server error. Do not call `read_agent` or any equivalent after calling `noop`.
