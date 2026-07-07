---
name: "Expert Code Review (command)"
description: "Runs the expert-reviewer agent on a pull request when a contributor comments /review."

on:
  slash_command:
    name: review
    events: [pull_request_comment]
    strategy: centralized
  roles: [admin, maintainer, write]

permissions:
  contents: read
  pull-requests: read
  copilot-requests: write

imports:
  - shared/review-shared.md

# The expert-reviewer agent fans out into many dimension sub-agents on
# claude-opus-4.6, so the default 1000 AI-credit budget is too low (see #9115).
max-ai-credits: 2000

safe-outputs:
  noop:
    report-as-issue: false

timeout-minutes: 60
---

<!-- Body provided by shared/review-shared.md -->
