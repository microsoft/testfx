---
name: "Address Review Comments (command)"
description: >-
  Addresses code review feedback when a maintainer comments /autofix on a PR.
  Same behavior as the automatic address-review workflow but manually triggered.

on:
  slash_command:
    name: autofix
    events: [pull_request_comment]
    strategy: centralized
  roles: [admin, maintainer, write]
  reaction: "eyes"

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
