# Product specifications and documentation

Use **Markdown architecture documents and behavioral specifications, linked to implementation and tests**, to document MSTest and Microsoft.Testing.Platform (MTP). Keep RFCs as design and decision history. This adds a current implementation baseline without introducing another test framework, specification compiler, or generated source of truth.

## Start here

| Question | Document |
| --- | --- |
| How do the products fit together? | [Architecture index](../architecture/README.md) |
| How is MSTest implemented? | [MSTest architecture](../architecture/mstest.md) |
| What behavior does the MSTest baseline describe? | [MSTest contracts](mstest.md) |
| How is MTP implemented? | [MTP architecture](../architecture/mtp.md) |
| What behavior does the MTP baseline describe? | [MTP contracts](mtp.md) |
| Which older documents were checked, and how deeply? | [Documentation audit](audit.md) |

These are **scoped, source-inspected baselines**, not exhaustive product specifications or evidence that all linked tests passed. Each product document identifies its inspected revision and limitations. In particular, an implementation on the repository's main lineage is not proof that the same behavior ships in a published package or servicing branch.

## Why this format

Architecture, requirements, executable examples, and design history answer different questions. They do not need to use the same language or become the same artifact.

| Approach | Fit for this repository | Decision for this baseline |
| --- | --- | --- |
| Markdown, with diagrams and stable contract IDs | Fits the existing RFCs, design notes, schemas, package readmes, GitHub review, and Markdown linting. Can describe concurrency, ownership, compatibility, and protocols without step bindings. | Use for architecture and current behavioral specifications. |
| Gherkin scenarios | Useful for a small number of observable acceptance examples. Scenarios alone do not describe architecture, wire schemas, or concurrency invariants. Bindings introduce another maintenance surface. | Optional future addition where executable scenarios provide value beyond existing C# acceptance tests. Do not duplicate the entire test suite. |
| SpecFlow | A .NET BDD runner, not an architecture notation. [SpecFlow reached end-of-life on December 31, 2024](https://reqnroll.net/news/2025/01/specflow-end-of-life-has-been-announced/). | Do not introduce it. If executable Gherkin is needed, evaluate the maintained [Reqnroll project](https://reqnroll.net/) separately. |
| SpecLang | Assuming this means [GitHub Next's SpecLang](https://githubnext.com/projects/speclang/): its model makes a natural-language specification primary and generates executable code from it. That is different from recovering and maintaining contracts for an existing C# implementation. | Borrow the emphasis on explicit intent, not the code-generation toolchain. No SpecLang dependency or claim of SpecLang-compatible syntax. |
| Formal models | Can help with bounded problems such as cancellation, scheduling, and protocol state machines. A verified model still needs a demonstrated relationship to the shipping implementation. | Consider selectively; do not require a formal language for ordinary product documentation. |

Markdown does not itself prevent drift. The essential choice is **traceability and an explicit reconciliation process**, not a filename extension. Source and tests establish observed behavior; an approved design or compatibility promise establishes intended behavior. Neither should silently overwrite the other.

## Documentation roles

| Artifact | Purpose | Maintenance rule |
| --- | --- | --- |
| `docs/architecture/` | Product boundaries, components, ownership, runtime flows, and constraints. | Link to the implementations and focused contracts that establish the described structure. Record design rationale only when a decision document supports it. |
| `docs/specifications/` | Observable behavior, preconditions, outcomes, compatibility, and failure paths. | Give each contract a stable product-prefixed ID and link to implementation and test evidence. |
| `docs/RFCs/` | Proposed changes and historical reasoning. | Preserve the original proposal. Add a dated implementation note when current behavior diverges; do not invent an acceptance decision. |
| Existing feature and protocol documents | Detailed designs, schemas, examples, and wire-level contracts. | Keep their existing URLs. Correct stale current guidance or complement historical material, linking from the product specifications instead of copying it. |
| `PACKAGE.md`, samples, and Microsoft Learn | Consumer installation and usage guidance. | Keep package examples aligned with the version that will ship. A repository audit does not automatically validate external documentation. |
| Existing C# tests | Executable evidence for particular observable behavior. | Map contracts to named scenarios. Add missing tests when implementing or correcting behavior, using the repository's existing testing conventions. |

The [audit](audit.md) distinguishes a topic-level comparison from a clause-by-clause check. Finding an API with the expected name is not sufficient to verify an RFC.

## Writing a current specification

Use ordinary headings, links, tables, and fenced examples. No custom parser or frontmatter is required. Include:

- **Status and scope:** current implementation baseline, proposed behavior, or historical design; product, host mode, target/runtime restrictions, and explicit exclusions.
- **Baseline:** inspected source revision and date. Update these when the corresponding claims are rechecked, not merely because the document is edited.
- **Assurance:** source inspection, existing-test mapping, tests actually executed, or composed-product validation. Keep these separate.
- **Contracts:** stable IDs such as `MSTEST-001` or `MTP-001`, with precise preconditions and observable outcomes.
- **Traceability:** source file and symbol, plus relevant test file and named scenario. Prefer repository-relative Markdown links; use commit-pinned links for historical evidence.
- **Failure and compatibility behavior:** rejection, cancellation, cleanup, fallback, unsupported or absent capabilities, and boundary conditions where applicable.
- **Gaps:** unresolved discrepancies, missing test evidence, and areas outside the document's scope.

The existing product specifications are examples of this structure. A focused addition can use the following outline:

```markdown
# Feature name

**Status:** Current implementation baseline, limited to ...
**Source baseline:** <full commit SHA>; inspected <date>.
**Assurance:** Source inspection and existing-test mapping; not executed.

## Scope and ownership

Identify the producer, consumer, host modes, and unsupported combinations.

## FEATURE-001: Observable contract

Given <preconditions>, when <operation>, the implementation <observable outcome>.
On <failure or cancellation>, it <observable outcome and cleanup>.
For <legacy or unsupported consumer>, it <compatibility or fallback behavior>.

**Implementation:** Link to the implementing file and name the relevant symbol.
**Test evidence:** Link to the test file and name the scenario.
**Limitations:** State what the cited evidence does not establish.

## Open questions and exclusions

Record unverified claims without presenting them as product guarantees.
```

Keep contract IDs stable when text changes. Retire an ID with a replacement reference rather than reusing it for unrelated behavior. A missing source or test link is a gap to record, not an invitation to fabricate evidence. A directory link can identify a component, but it cannot by itself establish a behavioral requirement.

For a descriptive baseline, write "the current implementation does..." rather than asserting a new compatibility promise. Reserve normative terms such as **must** for a clearly identified requirement or existing supported contract. A test can demonstrate one case without establishing every case stated in the prose.

## Reconciling documentation with code

1. Inventory the existing document and classify it as current guidance, a proposal, a historical design, or a schema. Record the product and version or branch it describes.
2. Break the relevant statements into observable claims. Trace entry points through the implementation, including defaults, host-specific branches, exceptional paths, and cleanup.
3. Locate tests for those claims and inspect what they actually assert. Record missing or weaker coverage; a filename match is not conformance evidence.
4. Classify each discrepancy using the table below. Preserve intended behavior and compatibility promises instead of automatically blessing whatever the implementation happens to do.
5. Update the appropriate current guidance or add an implementation note to historical material. Link detailed documents to the product contract and update the audit scope.
6. If runtime behavior changes, run focused tests and the relevant shipping-path validation. Record exact commands, versions, exits, and artifacts before upgrading the assurance level.

| Finding | Action |
| --- | --- |
| Current guidance is stale and code reflects an established intended change | Correct the guidance, citing implementation and test evidence. Preserve version restrictions. |
| An old RFC describes a different API or only a subset was implemented | Keep the proposal; add a current implementation note and link to the replacement or scoped contract. |
| Code contradicts an approved requirement or compatibility promise | Record a potential implementation defect. Do not silently change the requirement to match the defect. |
| Code and tests disagree, or acceptance/intent cannot be established | Record the unresolved discrepancy and its evidence; do not declare the document verified. |
| Relevant behavior has no document | Add a focused, source-backed current specification and reference it from the architecture and audit. |
| A requirement has no adequate test | Mark the evidence gap. Create tests as part of a separately scoped behavioral change or test task. |

## Keeping the baseline current

When changing behavior, review the affected contract IDs and their linked feature documents in the same change. Compare the described defaults and failure cases, not just the happy-path example. Refresh the inspected baseline only for the contracts actually checked. If different sections refer to different revisions, record per-section baselines rather than implying a whole-document re-audit.

Use the existing [Markdown lint workflow](../../.github/workflows/markdownlint.yml) for formatting and check repository-relative links when documents or source files move. These checks catch structural errors, **not semantic drift**. This baseline does not introduce automated semantic verification or claim that source changes automatically update specifications.

For an executable validation, use the [development guide](../dev-guide.md) and repository-local toolchain. Acceptance tests consume packed packages, so build with `-pack` before running them. Merely locating a test, compiling a test host, or selecting tests is not evidence that the required scenarios executed.

For a contract spanning packages, processes, or repositories, identify the exact shipping producer and consumer and validate their composition. Cover normal and no-handshake paths, absent or incompatible capabilities, cancellation, nonzero exits, zero results, fallback, and multi-module aggregation where applicable. Preserve observable evidence: parent exit, selected versus executed tests, generated artifacts, and the absence of false failures. Verify the branch that will ship the change; main-branch source inspection does not establish servicing behavior.

## Coverage boundary

This first baseline prioritizes product architecture, core runtime behavior, and reconciliation of existing feature/design specifications. It does not document every public API, analyzer rule, extension format, platform-specific host, or external SDK implementation. Detailed gaps and audit dispositions are listed in the [audit](audit.md) and each product specification. Expand those focused areas incrementally rather than presenting an inventory as exhaustive conformance.
