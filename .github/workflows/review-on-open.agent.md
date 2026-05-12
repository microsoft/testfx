---
name: "Expert Code Review (on open)"
description: "Automatically runs the expert-reviewer agent when a non-draft PR is opened."

# Non-draft PRs trigger this workflow.
# The `roles` setting restricts execution to users with admin, maintainer, or
# write permissions.
#
# Uses pull_request_target (not pull_request) so that fork PRs have
# access to repo secrets. This is safe because the agent reads the diff via
# GitHub MCP tools — it does not check out or execute code from the PR branch.
#
# NOTE: Only `opened` is used here; for PRs transitioned from draft to ready,
# use the `/review` slash command.
on:
  pull_request_target:
    types: [opened]
    forks: ["*"]
  roles: [admin, maintainer, write]

checkout: false

# Skip draft PRs — only run for PRs opened as ready or converted from draft
if: github.event.pull_request.draft == false

permissions:
  contents: read
  pull-requests: read

imports:
  - shared/review-shared.md

timeout-minutes: 60
---

<!-- Body provided by shared/review-shared.md -->
