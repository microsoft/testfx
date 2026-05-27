---
name: "Expert Code Review (on open)"
description: "Automatically runs the expert-reviewer agent when a non-draft PR is opened."

# Non-draft PRs trigger this workflow.
# The `roles` setting restricts execution to users with admin, maintainer, or
# write permissions, and fork PRs are blocked by the compiled repository_id guard.
#
# NOTE: Only `opened` is used here; for PRs transitioned from draft to ready,
# use the `/review` slash command.
on:
  pull_request:
    types: [opened]
  roles: [admin, maintainer, write]

# Skip draft PRs and OneLocBuild localization check-in PRs (authored by dotnet-bot)
# — only run for human-authored PRs opened as ready.
if: >
  github.event.pull_request.draft == false
  && !(
    github.event.pull_request.user.login == 'dotnet-bot'
    && startsWith(github.event.pull_request.title, 'Localized file check-in')
  )

permissions:
  contents: read
  pull-requests: read

imports:
  - shared/review-shared.md

safe-outputs:
  noop:
    report-as-issue: false

timeout-minutes: 60
---

<!-- Body provided by shared/review-shared.md -->
