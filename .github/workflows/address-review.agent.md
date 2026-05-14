---
name: "Address Review Comments"
description: >-
  Automatically addresses code review feedback on Copilot-created PRs.
  Reads review comments, applies fixes, verifies the build, and pushes changes.
  Includes a circuit breaker (max 3 iterations) to prevent infinite loops.

on:
  pull_request_review:
    types: [submitted]
  reaction: "eyes"

# Only run when:
# 1. The review explicitly requests changes (not an approval or informational comment)
# 2. The PR is from the same repo (not a fork — we need push access)
# 3. The PR was created by Copilot OR has the 'copilot-autofix' label
if: >-
  github.event.review.state == 'changes_requested'
  && github.event.pull_request.head.repo.id == github.repository_id
  && (
    github.event.pull_request.user.login == 'copilot-swe-agent[bot]'
    || contains(github.event.pull_request.labels.*.name, 'copilot-autofix')
  )

permissions:
  contents: read
  pull-requests: read
  actions: read

imports:
  - shared/address-review-shared.md

safe-outputs:
  noop:
    report-as-issue: false

timeout-minutes: 60
---

<!-- Body provided by shared/address-review-shared.md -->
