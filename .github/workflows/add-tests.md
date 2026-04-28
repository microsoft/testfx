---
description: "Generates unit tests for code introduced in a pull request when a maintainer comments /add-tests."

on:
  slash_command:
    name: add-tests
    events: [pull_request_comment]

permissions:
  contents: read
  pull-requests: read

imports:
  - shared/repo-build-setup.md

tools:
  github:
    toolsets: [pull_requests, repos]
  edit:
  bash: ["dotnet", "git", "find", "ls", "cat", "grep", "head", "tail", "wc", "mkdir"]

safe-outputs:
  noop:
    report-as-issue: false
  create-pull-request:
    title-prefix: "[tests] "
    labels: [test, automated]
    draft: true
    max: 1
    protected-files: fallback-to-issue
  add-comment:
    max: 3

timeout-minutes: 45
---

# Add Tests for PR Changes

Generate comprehensive unit tests for the code changes introduced in pull request #${{ github.event.issue.number }}.

## Context

The PR comment that triggered this workflow: "${{ steps.sanitized.outputs.text }}"

## Goal

Analyze the pull request diff to identify source files that were added or modified, then generate unit tests that cover those changes. The resulting tests should be submitted as a new draft pull request.

## Instructions

### Step 1: Understand the PR Changes

1. Use the GitHub pull requests tools to fetch the PR diff for PR #${{ github.event.issue.number }}
2. Identify all **source files** (under `src/`) that were added or modified — ignore test files, build files, docs, and config
3. For each changed source file, understand what classes, methods, or functionality was added or changed

### Step 2: Identify Test Gaps

1. For each changed source file, find the corresponding existing test project. Test projects are organized under `test/`:
   - `src/TestFramework/` → `test/UnitTests/TestFramework.UnitTests/`
   - `src/Adapter/MSTest.TestAdapter/` → `test/UnitTests/MSTestAdapter.UnitTests/`
   - `src/Adapter/MSTestAdapter.PlatformServices/` → `test/UnitTests/MSTestAdapter.PlatformServices.UnitTests/`
   - `src/Adapter/MSTest.Engine/` → `test/UnitTests/MSTest.Engine.UnitTests/`
   - `src/Analyzers/MSTest.Analyzers/` → `test/UnitTests/MSTest.Analyzers.Tests/` (if exists)
   - `src/Analyzers/MSTest.SourceGeneration/` → `test/UnitTests/MSTest.SourceGeneration.UnitTests/`
   - `src/Platform/` → `test/UnitTests/` (find matching test project by name)
2. Check if the changed code already has test coverage
3. Focus on code that is **not yet covered** by existing tests

### Step 3: Generate Tests

Use the `code-testing-generator` agent (defined at `.github/agents/code-testing-generator.agent.md`) via the task tool to generate tests:

1. Follow the Research → Plan → Implement pipeline from the skill
2. **Scope**: Only generate tests for code modified in this PR — do not attempt full-repo coverage
3. **Test framework**: This repo uses MSTest with `[TestMethod]`, `[DataRow]` attributes for MTP and analyzer tests. Unit tests for MSTest itself MUST use the internal test framework from `test/Utilities/TestFramework.ForTestingMSTest`
4. **Test type preference**: For MSTest framework code, **prefer integration tests** (under `test/IntegrationTests/`) over unit tests — unit tests are often not sufficient to validate framework behavior. The exception is `MSTest.Analyzers`, where unit tests are appropriate
5. **Assertions**: All assertions must use FluentAssertions style
6. **Naming**: Test classes as `{Feature}Tests`, test methods as PascalCase descriptive names following `Method_Condition_ExpectedResult` pattern
7. **License header**: Every `.cs` file must start with the .NET Foundation MIT license header:

   ```csharp
   // Copyright (c) Microsoft Corporation. All rights reserved.
   // Licensed under the MIT license. See LICENSE file in the project root for full license information.
   ```

8. **Style**: Follow existing test patterns in the repo — check adjacent test files for conventions. Use file-scoped namespaces, `is null`/`is not null` patterns, and respect StyleCop rules
9. **Build**: Use `dotnet build <TestProject.csproj>` for scoped builds during development
10. **Test**: Use `dotnet test <TestProject.csproj>` to verify tests pass
11. **Public API**: Do NOT use `init` accessors in any new public API

### Step 4: Validate

1. Build the specific test project(s) you modified
2. Run the tests to verify they pass
3. If tests fail, fix assertions based on actual production code behavior — never skip or ignore tests

### Step 5: Create the PR

Commit all test files and create a draft pull request. The PR description should:

- Reference the original PR (#${{ github.event.issue.number }})
- List the test files created
- Summarize what is covered by the new tests
