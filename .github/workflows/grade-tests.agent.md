---
name: "Grade Tests on PR (command)"
description: >-
  Re-grades the new and modified test methods of a pull request when a
  maintainer comments `/grade-tests`.

# The automatic on-open / on-synchronize variant lives in
# `grade-tests-on-pr.agent.md`. They must remain separate workflows
# because mixing `slash_command` with other triggers makes gh-aw's
# activation gate always require a command position match, silently
# skipping the agent on every non-comment event.
on:
  slash_command:
    name: grade-tests
    events: [pull_request_comment]
    strategy: centralized
  roles: [admin, maintainer, write]
  reaction: "eyes"

permissions:
  contents: read
  pull-requests: read

imports:
  - shared/grade-tests-shared.md

safe-outputs:
  noop:
    report-as-issue: false

concurrency:
  group: grade-tests-${{ github.event.issue.number }}
  cancel-in-progress: true

timeout-minutes: 20
---

<!-- Body provided by shared/grade-tests-shared.md -->
