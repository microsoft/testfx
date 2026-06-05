---
name: "Grade Tests on PR"
description: >-
  Grades the new and modified test methods in a pull request and posts a
  single PR comment with a compact per-test scorecard (letter grade A–F,
  score band, and one-line notes). Runs automatically on PR open and on
  pushes that touch `test/**`, and can be re-triggered manually via the
  `/grade-tests` slash command.

# Triggers:
# - pull_request `opened` / `ready_for_review` — initial grade on the PR's
#   first appearance as a non-draft.
# - pull_request `synchronize` — re-grade when new commits are pushed so the
#   comment stays current. Combined with `paths` so we only fire when test
#   files change, and with `concurrency.cancel-in-progress` so superseded
#   runs are cancelled.
# - slash_command `/grade-tests` — manual re-run by a maintainer.
#
# The `roles` setting restricts execution to users with admin, maintainer,
# or write permissions, and fork PRs are blocked by the compiled
# repository_id guard.
on:
  pull_request:
    types: [opened, synchronize, ready_for_review]
    paths:
      - "test/**"
      - "src/**"
  slash_command:
    name: grade-tests
    events: [pull_request_comment]
    strategy: centralized
  roles: [admin, maintainer, write]
  reaction: "eyes"
  permissions:
    contents: read
    pull-requests: read
  steps:
    # Deterministic extraction: figure out which test methods were added or
    # modified in this PR. We do this in bash (not in the agent) so the agent
    # gets an exact, auditable list — never a hallucinated one. We then gate
    # the agent on `has_changed_tests == 'true'` so PRs with no test changes
    # never burn agent runtime.
    - name: Resolve PR base and head
      id: resolve
      env:
        EVENT_NAME: ${{ github.event_name }}
        EVENT_PR_BASE_SHA: ${{ github.event.pull_request.base.sha }}
        EVENT_PR_HEAD_SHA: ${{ github.event.pull_request.head.sha }}
        EVENT_PR_NUMBER: ${{ github.event.pull_request.number }}
        EVENT_PR_DRAFT: ${{ github.event.pull_request.draft }}
        EVENT_PR_AUTHOR: ${{ github.event.pull_request.user.login }}
        EVENT_PR_TITLE: ${{ github.event.pull_request.title }}
        EVENT_ISSUE_NUMBER: ${{ github.event.issue.number }}
        GH_TOKEN: ${{ github.token }}
      run: |
        set -euo pipefail
        case "$EVENT_NAME" in
          pull_request)
            BASE_SHA="$EVENT_PR_BASE_SHA"
            HEAD_SHA="$EVENT_PR_HEAD_SHA"
            PR_NUMBER="$EVENT_PR_NUMBER"
            DRAFT="$EVENT_PR_DRAFT"
            AUTHOR="$EVENT_PR_AUTHOR"
            TITLE="$EVENT_PR_TITLE"
            ;;
          issue_comment)
            PR_NUMBER="$EVENT_ISSUE_NUMBER"
            PR_JSON=$(gh pr view "$PR_NUMBER" --repo "$GITHUB_REPOSITORY" --json baseRefOid,headRefOid,isDraft,author,title)
            BASE_SHA=$(printf '%s' "$PR_JSON" | jq -r '.baseRefOid')
            HEAD_SHA=$(printf '%s' "$PR_JSON" | jq -r '.headRefOid')
            DRAFT=$(printf '%s' "$PR_JSON" | jq -r '.isDraft')
            AUTHOR=$(printf '%s' "$PR_JSON" | jq -r '.author.login')
            TITLE=$(printf '%s' "$PR_JSON" | jq -r '.title')
            ;;
          *)
            echo "Unsupported event: $EVENT_NAME" >&2
            exit 1
            ;;
        esac
        echo "base_sha=$BASE_SHA" >> "$GITHUB_OUTPUT"
        echo "head_sha=$HEAD_SHA" >> "$GITHUB_OUTPUT"
        echo "pr_number=$PR_NUMBER" >> "$GITHUB_OUTPUT"
        echo "draft=$DRAFT" >> "$GITHUB_OUTPUT"
        echo "author=$AUTHOR" >> "$GITHUB_OUTPUT"
        echo "title=$TITLE" >> "$GITHUB_OUTPUT"

    - name: Skip draft and OneLocBuild PRs
      id: skip
      env:
        EVENT_NAME: ${{ github.event_name }}
        DRAFT: ${{ steps.resolve.outputs.draft }}
        AUTHOR: ${{ steps.resolve.outputs.author }}
        TITLE: ${{ steps.resolve.outputs.title }}
      run: |
        set -euo pipefail
        SHOULD_RUN=true
        if [[ "$DRAFT" == "true" && "$EVENT_NAME" == "pull_request" ]]; then
          echo "Skipping: PR is a draft."
          SHOULD_RUN=false
        fi
        if [[ "$AUTHOR" == "dotnet-bot" && "$TITLE" == "Localized file check-in"* ]]; then
          echo "Skipping: OneLocBuild localization PR."
          SHOULD_RUN=false
        fi
        echo "should_run=$SHOULD_RUN" >> "$GITHUB_OUTPUT"

    - name: Checkout PR head
      if: steps.skip.outputs.should_run == 'true'
      uses: actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd # v6.0.2
      with:
        ref: ${{ steps.resolve.outputs.head_sha }}
        fetch-depth: 0

    - name: Extract changed test methods
      id: extract
      if: steps.skip.outputs.should_run == 'true'
      env:
        BASE_SHA: ${{ steps.resolve.outputs.base_sha }}
        HEAD_SHA: ${{ steps.resolve.outputs.head_sha }}
      run: |
        set -euo pipefail
        OUT="$RUNNER_TEMP/changed-tests.tsv"

        python3 .github/workflows/scripts/grade-tests/extract-changed-tests.py \
          "$BASE_SHA" "$HEAD_SHA" "$OUT"

        if [[ ! -s "$OUT" ]]; then
          echo "No changed test methods detected."
          echo "has_changed_tests=false" >> "$GITHUB_OUTPUT"
          echo "count=0" >> "$GITHUB_OUTPUT"
          exit 0
        fi

        COUNT=$(wc -l < "$OUT")
        echo "Found $COUNT changed test method(s)."
        echo "has_changed_tests=true" >> "$GITHUB_OUTPUT"
        echo "count=$COUNT" >> "$GITHUB_OUTPUT"
        echo "tsv_path=$OUT" >> "$GITHUB_OUTPUT"

# Gate the agent on (a) skip checks passing and (b) at least one changed test method.
if: >
  needs.pre_activation.outputs.should_run == 'true'
  && needs.pre_activation.outputs.has_changed_tests == 'true'

concurrency:
  group: grade-tests-${{ github.event.pull_request.number || github.event.issue.number }}
  cancel-in-progress: true

permissions:
  contents: read
  pull-requests: read

network:
  allowed:
    - defaults

tools:
  github:
    lockdown: true
    toolsets: [pull_requests, repos]
    min-integrity: none
  bash: ["git", "find", "ls", "cat", "grep", "head", "tail", "wc", "awk", "sed", "sort", "uniq", "python3"]

safe-outputs:
  noop:
    report-as-issue: false
  add-comment:
    max: 5
    target: "*"
    hide-older-comments: true

timeout-minutes: 20
---

# Grade Tests on PR

You are a polyglot test-quality grader. Your single output is a PR comment
that gives a per-test letter grade for the new and modified tests in pull
request #${{ github.event.pull_request.number || github.event.issue.number }}
of ${{ github.repository }}.

You are **read-only and advisory**. Do not edit any files. Do not push.
Do not request changes — your role is to inform, not to block.

## Inputs you have

A deterministic pre-step has already identified the changed test methods.
The list is in the tab-separated file at
`${{ steps.extract.outputs.tsv_path }}`. Each row is:

```
<filepath>\t<start_line>\t<end_line>\t<fully.qualified.name>
```

There are **${{ steps.extract.outputs.count }}** changed test methods.

## Instructions

### Step 1 — Read the changed test methods

1. `cat` the TSV file to get the full list.
2. For each row, read the corresponding file and extract the method body
   from `start_line` to `end_line` (inclusive). The line numbers are
   already accurate spans for the method including its attributes.
3. If a method's source range is now out-of-bounds (file was rewritten),
   record it as `N/A — method not found` and continue.

### Step 2 — Grade each method

This repository is C# / MSTest. Apply the **grade-tests rubric** below to
each test method. Start every test at grade **A (band 90–100)** and deduct
only for **observable issues** in the captured body. Do not deduct for
hypothetical concerns.

#### Three sub-dimensions (each A–F)

**A. Assertion strength**

| Sub-grade | Pattern |
|-----------|---------|
| A | Meaningful value assertion (equality / structural / exception / state) plus, where appropriate, additional checks (negative, type, collection contents). Mock-call verifications (`Verify`, `Should -Invoke`) count. |
| B | One clear meaningful assertion that verifies the behavior under test. |
| C | Only trivial assertions (single `Assert.IsNotNull(result)`), or one field checked while the operation produces a richer result. |
| D | Self-referential / tautological (`Assert.AreEqual(x, x)`, round-trip identity without a non-trivial input), broad exception (`Assert.ThrowsException<Exception>` without a more specific type), or always-true assertions. |
| F | No assertions, or all assertions are silently un-awaited (e.g., `async Task` test calling `Assert.ThrowsAsync` without `await`). |

Exception tests (`Assert.ThrowsException<T>(…)`) are complete on their own
— do not require additional assertions.

**B. Structure & focus**

| Sub-grade | Pattern |
|-----------|---------|
| A | Clear Arrange-Act-Assert separation. Single behavior. Body under ~30 lines. |
| B | One mild structural issue (slightly long body, missing blank lines between phases) but intent is clear. |
| C | Multiple behaviors in one test, or AAA phases interleaved enough to slow comprehension. |
| D | Conditional logic in the test (`if`/`switch` driving assertions); or test relies on previous test state. |
| F | Test exceeds ~60 lines verifying multiple unrelated behaviors; or shares mutable state without reset. |

**C. Anti-pattern hygiene**

Each finding deducts one sub-grade level (A→B→C→D→F). Use the lowest
sub-grade among findings.

- **Critical (drop to F or D)**: no assertions; swallowed exceptions
  (`try { … } catch { }`, `catch (Exception)` without rethrow/assert);
  assert-in-catch (`Assert.Fail(ex.Message)` instead of
  `Assert.ThrowsException`); always-true / tautological assertions;
  commented-out assertions.
- **High (drop one or two)**: wall-clock sleep (`Thread.Sleep`,
  `Task.Delay`) used for synchronization in a unit test; unseeded
  randomness (`new Random()`); wall-clock reads without abstraction
  (`DateTime.Now`, `DateTime.UtcNow`); hard-coded environment paths;
  ordering dependency on mutable static state; broad exception assertion
  without specific type; over-mocking; implementation coupling
  (reflection on private members).
- **Medium (drop one)**: poor name (`Test1`, `TestMethod`, single-word);
  unexplained magic values; giant test (>30 lines for one behavior);
  assertion messages that just repeat the assertion; missing
  AAA separation when the test is non-trivial.
- **Low (note only)**: leftover `Console.WriteLine` / `Debug.WriteLine`;
  unused setup/teardown hooks; inconsistent naming versus sibling tests;
  leftover TODO comments. Mention in notes but do not deduct.

#### Combine sub-grades

Numeric points: A=4, B=3, C=2, D=1, F=0.
- Overall = `0.45 × Assertion + 0.30 × Anti-pattern + 0.25 × Structure`.
- Map: ≥ 3.5 → **A** (90–100), ≥ 2.8 → **B** (80–89),
  ≥ 2.0 → **C** (70–79), ≥ 1.2 → **D** (60–69), < 1.2 → **F** (0–59).
- If any sub-grade is **F**, cap overall at **D**.
- If Assertion sub-grade is **F**, overall is **F**.

Report the **letter grade** and the **score band** only — no fake-precise
0–100 number.

#### testfx-specific rules

This repo uses MSTest as the test framework. Specifically:

- **Internal framework tests** (under `test/UnitTests/TestFramework.UnitTests/`
  and adjacent projects) use the internal test framework from
  `test/Utilities/TestFramework.ForTestingMSTest`. Treat its
  `Verify(...)` calls as the equivalent of `Assert.IsTrue(...)`.
- **Integration tests** (under `test/IntegrationTests/`) frequently
  spawn processes and have inherently long bodies — do **not** deduct
  for body length below ~120 lines in this folder.
- **FluentAssertions** style (`x.Should().Be(...)`, `x.Should().Throw<T>()`)
  is the preferred assertion style and is fully equivalent to MSTest's
  classic API.
- Do **not** flag missing `init` accessors, license headers, or other
  repo-stylistic concerns — those are out of scope for this rubric.

### Step 3 — Build the note

One short sentence per test (≤ 120 chars) that states the single most
important reason for the grade. If a test is clean, the note may simply
read `No issues found.` — do not invent weaknesses to balance the note.

### Step 4 — Post the comment

Use **exactly one** `add-comment` call. The comment body must follow this
structure:

```markdown
### 🧪 Test quality grade — PR #${{ github.event.pull_request.number || github.event.issue.number }}

<!-- 2–4 sentence summary: total graded, grade distribution, most common
issue, and the single most important recommendation. -->

| Test | Grade | Band | Notes |
|------|-------|------|-------|
| `Namespace.ClassName.Method` | A | 90–100 | … |
| `Namespace.ClassName.Other`  | C | 70–79  | … |

<sub>This advisory comment was generated automatically. Grades are heuristic
and informational — they do not block merging. Re-run with
`/grade-tests`.</sub>
```

Rules for the table:
- **Order**: lowest grade first (F → D → C → B → A); within a grade, by
  fully-qualified name.
- **Caps**: if there are more than 50 rows, show all rows with grade < B
  first, then a sample of the best rows, and wrap any overflow in a
  collapsed `<details><summary>Remaining N tests</summary>…</details>` block.
- **Prefix** each row with `(new)` or `(modified)` if the diff context
  makes that distinction clear from `git log` on the file at HEAD; if
  unsure, omit the prefix rather than guessing.

**Important**: Emit **only one** `add-comment` call. The workflow is
configured with `hide-older-comments: true`, so re-runs will replace any
earlier grade comment automatically — do not append additional comments.

### Step 5 — Stop

After the single `add-comment` call, call `noop` with a brief status
message such as `"Posted test-quality grade comment for PR #N (M tests graded)."`
and stop. Do not call any other tools.
