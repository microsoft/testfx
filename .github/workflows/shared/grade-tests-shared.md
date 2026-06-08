---
# Shared frontmatter and prompt body for the test-grading workflows.
#
# Two consumers import this file:
#   - grade-tests-on-pr.agent.md  → runs automatically on PRs that touch tests
#   - grade-tests.agent.md        → runs on the /grade-tests slash command
#
# Both consumers contribute their own trigger configuration (`on:`) and
# their own `safe-outputs.noop` block (kept in each consumer until the
# gh-aw import merge fully preserves `report-as-issue: false`). Everything
# else — tools, permissions, network, the deterministic pre-extraction
# steps, and the agent prompt — lives here so the two variants cannot
# drift.
#
# Splitting the workflow was necessary because mixing `slash_command` with
# any other trigger in a single gh-aw workflow makes the activation gate
# require a command-position match on *every* event, which silently skips
# the agent on every `pull_request` invocation.

description: >-
  Grades the new and modified test methods in a pull request and posts a
  single PR comment with a compact per-test scorecard (letter grade A–F,
  score band, and one-line notes).

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
  bash:
    - git
    - gh
    - jq
    - find
    - ls
    - cat
    - grep
    - head
    - tail
    - wc
    - awk
    - sed
    - sort
    - uniq

safe-outputs:
  add-comment:
    max: 5
    target: "*"
    hide-older-comments: true

# Deterministic extraction: figure out which test methods were added or
# modified in this PR. We do this in bash (not in the agent) so the agent
# gets an exact, auditable list — never a hallucinated one. The agent
# then decides which of those methods are *tests* (testfx has many
# derived attributes that a regex would silently miss).
#
# These steps run inside the agent job, after the framework's default
# checkout, and emit a TSV at `$RUNNER_TEMP/changed-test-regions.tsv`.
# Step outputs are exposed to the agent both via `${{ steps.* }}`
# template substitution in the prompt below and via auto-generated
# `GH_AW_STEPS_*` environment variables.
steps:
  - name: Resolve PR base and head
    id: resolve
    env:
      EVENT_NAME: ${{ github.event_name }}
      EVENT_PR_BASE_SHA: ${{ github.event.pull_request.base.sha }}
      EVENT_PR_HEAD_SHA: ${{ github.event.pull_request.head.sha }}
      EVENT_PR_NUMBER: ${{ github.event.pull_request.number }}
      EVENT_ISSUE_NUMBER: ${{ github.event.issue.number }}
      GH_TOKEN: ${{ github.token }}
    run: |
      set -euo pipefail
      case "$EVENT_NAME" in
        pull_request)
          BASE_SHA="$EVENT_PR_BASE_SHA"
          HEAD_SHA="$EVENT_PR_HEAD_SHA"
          PR_NUMBER="$EVENT_PR_NUMBER"
          ;;
        issue_comment)
          PR_NUMBER="$EVENT_ISSUE_NUMBER"
          PR_JSON=$(gh pr view "$PR_NUMBER" --repo "$GITHUB_REPOSITORY" --json baseRefOid,headRefOid)
          BASE_SHA=$(printf '%s' "$PR_JSON" | jq -r '.baseRefOid')
          HEAD_SHA=$(printf '%s' "$PR_JSON" | jq -r '.headRefOid')
          ;;
        *)
          echo "Unsupported event: $EVENT_NAME" >&2
          exit 1
          ;;
      esac
      echo "base_sha=$BASE_SHA" >> "$GITHUB_OUTPUT"
      echo "head_sha=$HEAD_SHA" >> "$GITHUB_OUTPUT"
      echo "pr_number=$PR_NUMBER" >> "$GITHUB_OUTPUT"

  - name: Checkout PR head with full history
    uses: actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd # v6.0.2
    with:
      ref: ${{ steps.resolve.outputs.head_sha }}
      fetch-depth: 0
      persist-credentials: false

  # Emit one row per changed `.cs` file under `test/`, with the HEAD-side
  # changed line ranges. Identifying which methods in those regions are
  # *tests* (vs. helpers, fixtures, sub-classes deriving from custom
  # `[TestMethod]`-like attributes, etc.) is left to the agent — the
  # LLM is significantly more robust at this judgment than any regex
  # would be, especially given testfx's many derived attributes such as
  # `[STATestMethod]`, `[UITestMethod]`, `[IterativeTestMethod]`, and
  # locally-defined `MyTestMethodAttribute : TestMethodAttribute` etc.
  - name: Extract changed test file regions
    id: extract
    env:
      BASE_SHA: ${{ steps.resolve.outputs.base_sha }}
      HEAD_SHA: ${{ steps.resolve.outputs.head_sha }}
    run: |
      set -euo pipefail
      OUT="$RUNNER_TEMP/changed-test-regions.tsv"
      : > "$OUT"

      mapfile -t CHANGED < <(
        git diff --name-only --diff-filter=AMR "$BASE_SHA" "$HEAD_SHA" -- 'test/' \
          | grep -E '\.cs$' || true
      )

      if (( ${#CHANGED[@]} == 0 )); then
        echo "No test files changed."
        echo "has_changed_tests=false" >> "$GITHUB_OUTPUT"
        echo "file_count=0" >> "$GITHUB_OUTPUT"
        echo "tsv_path=$OUT" >> "$GITHUB_OUTPUT"
        exit 0
      fi

      for f in "${CHANGED[@]}"; do
        [[ -f "$f" ]] || continue
        RANGES=$(
          git diff --unified=0 "$BASE_SHA" "$HEAD_SHA" -- "$f" \
            | awk '
                /^@@/ {
                  if (match($0, /\+[0-9]+(,[0-9]+)?/)) {
                    hunk = substr($0, RSTART+1, RLENGTH-1)
                    n = split(hunk, a, ",")
                    start = a[1] + 0
                    count = (n == 2 ? a[2] + 0 : 1)
                    if (count == 0) next
                    end = start + count - 1
                    printf("%d-%d,", start, end)
                  }
                }
              ' \
            | sed 's/,$//'
        )
        if [[ -n "$RANGES" ]]; then
          printf '%s\t%s\n' "$f" "$RANGES" >> "$OUT"
        fi
      done

      if [[ ! -s "$OUT" ]]; then
        echo "No changed line ranges in test files."
        echo "has_changed_tests=false" >> "$GITHUB_OUTPUT"
        echo "file_count=0" >> "$GITHUB_OUTPUT"
        echo "tsv_path=$OUT" >> "$GITHUB_OUTPUT"
        exit 0
      fi

      FILE_COUNT=$(wc -l < "$OUT")
      echo "Found $FILE_COUNT test file(s) with changes."
      echo "has_changed_tests=true" >> "$GITHUB_OUTPUT"
      echo "file_count=$FILE_COUNT" >> "$GITHUB_OUTPUT"
      echo "tsv_path=$OUT" >> "$GITHUB_OUTPUT"
---

# Grade Tests on PR

You are a polyglot test-quality grader. Your single output is a PR comment
that gives a per-test letter grade for the new and modified tests in pull
request #${{ github.event.pull_request.number || github.event.issue.number }}
of ${{ github.repository }}.

You are **read-only and advisory**. Do not edit any files. Do not push.
Do not request changes — your role is to inform, not to block.

## Inputs you have

A deterministic pre-step has already identified the **test files** whose
content changed in this PR and the line ranges that changed in each.
The list is in the tab-separated file at
`${{ steps.extract.outputs.tsv_path }}`. Each row is:

```
<filepath>\t<comma-separated-line-ranges>
```

For example:

```
test/UnitTests/Foo.Tests/BarTests.cs	12-25,40-67
test/IntegrationTests/Acceptance.IntegrationTests/QuxTests.cs	5-30
```

There are **${{ steps.extract.outputs.file_count }}** changed test
file(s). The pre-step intentionally does **not** decide which methods
are tests — that judgment is yours, because testfx has many derived
test attributes (e.g. `[STATestMethod]`, `[UITestMethod]`,
`[IterativeTestMethod]`, and locally-defined `MyTestMethodAttribute`
subclasses) that a regex extractor would silently miss.

If the TSV is empty (file count is 0), use the Step 4 fallback comment
and stop — do not invent grades.

## Instructions

### Step 1 — Identify changed test methods

For each row in the TSV:

1. Read the file (use `cat`, or `sed -n '<start>,<end>p'` for large files).
2. Walk the file to find every method whose source span (attributes
   through closing brace) **overlaps any of the listed line ranges**.
3. Decide which of those methods are **test methods**. A method is a
   test if either:
   - It is decorated with a test attribute. Treat the standard ones —
     `[TestMethod]`, `[DataTestMethod]`, `[Fact]`, `[Theory]`, `[Test]`,
     `[TestCase]` — as tests, **and also** any attribute that
     transitively derives from one of them. testfx-known examples
     include `[STATestMethod]`, `[UITestMethod]`,
     `[IterativeTestMethod]`, `[DerivedSTATestMethod]`, and project-
     local `[MyTestMethod]`-style attributes. When unsure, `grep` the
     repo (e.g. `grep -rn "class FooAttribute" src test`) to verify
     the inheritance chain before grading.
   - The surrounding test file uses a by-convention framework where
     plain methods are tests (rare in C#; ignore unless obvious).
4. Skip helpers, fixtures, `[TestInitialize]` / `[TestCleanup]` /
   `[ClassInitialize]` / `[AssemblyInitialize]` methods, `[DataRow]`
   data providers, and any non-test method even if it was modified.
5. For each kept method, capture its fully-qualified name
   (`Namespace.ClassName.MethodName`, walking up nested classes) and
   keep the source body (including attributes) for grading.

If after filtering no test methods remain, emit a short comment saying
so (see Step 4 fallback) and stop — do not invent grades.

### Step 2 — Grade each test method

This repository is C# / MSTest. Apply the **`grade-tests` skill** (synced
into this repo at `.agents/skills/grade-tests/SKILL.md` from
`dotnet/skills`) to grade each kept method. Invoke it via the `skill`
tool and follow its rubric exactly — do not re-derive or restate the
rubric here; the synced skill is the single source of truth and will
evolve over time.

When the skill asks for the language extension, follow its own Step 1
guidance: invoke the **`test-analysis-extensions`** skill to discover
the available per-language reference files and read the one matching
this codebase (C# / MSTest). Do not hard-code a path to a specific
extension file — the discovery step is what keeps this workflow
resilient to future reshuffles in the extensions layout.

**Pass these inputs to the skill** so it does not fall into its Step 0
refusal branch:

1. The explicit list of kept fully-qualified test method names from
   Step 1.
2. For each method, the file path **and** the method body (captured in
   Step 1).
3. The diff context for this PR — the
   `${{ steps.extract.outputs.tsv_path }}` rows already give the changed
   line ranges per file.

#### testfx-specific deviations (apply on top of the skill rubric)

A small number of repo-local conventions adjust how the standard rubric
should be interpreted in this codebase. Use these as **additions** to —
not replacements for — the synced skill's rubric:

- **Internal framework tests** (under `test/UnitTests/TestFramework.UnitTests/`
  and adjacent projects) use the internal test framework from
  `test/Utilities/TestFramework.ForTestingMSTest`. Treat its
  `Verify(...)` calls as the equivalent of `Assert.IsTrue(...)` — they
  are meaningful assertions, not boolean tautologies.
- **Integration tests** (under `test/IntegrationTests/`) frequently
  spawn processes and have inherently long bodies — do **not** deduct
  for body length below ~120 lines in this folder (overrides the
  rubric's ~30/~60-line thresholds for the Structure sub-grade).
- **AwesomeAssertions** style (`x.Should().Be(...)`, `x.Should().Throw<T>()`)
  is equivalent to MSTest's classic API for grading purposes and is used
  in the adapter unit-test projects (`MSTestAdapter.UnitTests`,
  `MSTestAdapter.PlatformServices.UnitTests`, etc.). Do **not** flag the
  absence of AwesomeAssertions in projects that use MSTest's native
  `Assert`/`StringAssert`/`CollectionAssert` — both are correct styles,
  project-dependent.
- testfx defines many derived test attributes (e.g. `[STATestMethod]`,
  `[UITestMethod]`, `[IterativeTestMethod]`, `[DerivedSTATestMethod]`,
  and project-local `[MyTestMethod]`-style classes that derive from
  `TestMethodAttribute`). Treat all of them as test markers — Step 1
  already filtered to test methods using these.
- Do **not** flag missing `init` accessors, license headers, or other
  repo-stylistic concerns — those are out of scope for this rubric.

Report the **letter grade** and the **score band** only — no
fake-precise 0–100 number.

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

#### Fallback: no test methods found

If the TSV is empty, or if after Step 1 the kept-method list is empty
(every changed method was a helper, fixture, data row, or non-test),
post this short comment instead of the table:

```markdown
### 🧪 Test quality grade — PR #${{ github.event.pull_request.number || github.event.issue.number }}

No new or modified test methods were identified in the changed regions
of this PR. Nothing to grade.

<sub>Re-run with `/grade-tests`.</sub>
```

### Step 5 — Stop

After the single `add-comment` call, call `noop` with a brief status
message such as `"Posted test-quality grade for PR #N (M test methods graded across K files)."`
and stop. Do not call any other tools.
