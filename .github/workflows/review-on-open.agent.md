---
name: "Expert Code Review (on PR ready)"
description: "Automatically runs the expert-reviewer agent when a PR becomes ready for review — either opened as non-draft, or transitioned from draft to ready."

# Non-draft PRs trigger this workflow.
# The `roles` setting restricts execution to users with admin, maintainer, or
# write permissions, and fork PRs are blocked by the compiled repository_id guard.
#
# Triggers on both `opened` (PR opened as non-draft) and `ready_for_review`
# (PR transitioned from draft to ready). The `if:` condition below filters out
# draft PRs so the `opened` event is a no-op for drafts.
on:
  pull_request:
    types: [opened, ready_for_review]
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
