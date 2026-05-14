---
name: "Re-review After Autofix"
description: >-
  Re-runs the expert code review after new commits are pushed to a
  Copilot-created PR. This closes the autofix loop: after the address-review
  workflow pushes fixes, this workflow verifies them with the expert-reviewer.

on:
  pull_request:
    types: [synchronize]

# Only re-review when:
# 1. The PR is not a draft
# 2. The PR was created by Copilot OR has the 'copilot-autofix' label
if: >-
  github.event.pull_request.draft == false
  && (
    github.event.pull_request.user.login == 'copilot-swe-agent[bot]'
    || contains(github.event.pull_request.labels.*.name, 'copilot-autofix')
  )

checkout: false

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
