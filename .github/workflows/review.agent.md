---
name: "Expert Code Review (command)"
description: "Runs the expert-reviewer agent on /review and submits one consolidated review with bundled inline findings."

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

# Broad PRs can require several risk-scope agents, validators, and specialist
# reviews on claude-opus-4.6, so retain headroom above the default budget.
max-ai-credits: 2000

safe-outputs:
  noop:
    report-as-issue: false

timeout-minutes: 60
---

<!-- Body provided by shared/review-shared.md -->
