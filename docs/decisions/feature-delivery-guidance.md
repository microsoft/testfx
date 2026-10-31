# Feature delivery guidance: research and decisions

## Problem and contract

Feature PRs should explain not only what changed, but why that direction was
chosen and what demonstrates it works. The requested guidance covers practical
screenshots/recordings, research into title prefixes, maintained research and
experiment records, regression sensitivity without the production change, and
constructive challenge of design decisions when rationale is missing.

The 2026-10-30 extension makes consumer-level E2E coverage and actual rendered
UI/layout inspection explicit. Correct intermediate names or one correct report
must not be treated as proof that another consumer preserves identity, grouping,
or per-result presentation.

This change governs contributor instructions, feature implementation, the PR
template, and reviewers that load repository guidance. It does not add runtime
behavior, mandatory title linting, new release tooling, publication permission,
or a requirement to launch extra review agents. Separate review services that
do not load these files are outside this guidance's enforcement.

## PR-title research

Research performed on 2026-10-09:

| Source | Verified observation | Consequence |
| --- | --- | --- |
| [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/) | Defines **commit messages** as `type(scope): description`, with optional scope and breaking-change markers. `[feat]` is not that syntax. | If a parser needs conventional metadata, prefer its supported format, not an ad-hoc bracket convention. A PR-title rule alone is not a commit-history rule. |
| [semantic-release introduction](https://semantic-release.org/intro/) | Version/release decisions consume commit messages; the default convention is Angular. | Structured metadata has a concrete automation benefit when the actual release consumer is configured for it. It does not automatically process PR titles. |
| [GitHub squash-merge configuration](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/configuring-pull-request-merges/configuring-commit-squashing-for-pull-requests) | Squash messages can use the PR title, but defaults and selected formats differ. | Title metadata reaches commit-based tools only if merge settings and the resulting commit preserve it. |
| [GitHub generated release notes](https://docs.github.com/en/repositories/releasing-projects-on-github/automatically-generated-release-notes) | Categories and exclusions use labels/authors; notes list merged PRs. | Descriptive PR titles still matter to users; a prefix is not necessary for GitHub's label-based categorization. |
| [Repository release configuration](../../.github/release.yml) | Currently excludes selected bot authors and defines no title parser. | Do not introduce a global prefix requirement on the assumption that this configuration needs one. |
| [Title-sensitive auto-merge](../../.github/workflows/enable-auto-merge.yml) | Matches localization titles and specific `[main] Update dependencies ...` prefixes. | Preserve existing automation titles; a global rewrite could change routing. |

The repository search also found title-prefix restrictions for specific
improvement bots, not a general `feat:`/`[feat]` contributor rule. Recent local
commit subjects use descriptive prose. These observations establish current
practice, not a controlled measurement of readability.

For **users**, a prefix can offer a quick category cue, but the title must still
name the behavior and affected product. For **maintainers**, consistent categories
can help scanning/filtering when they are accurate; additional classification
rules also cost effort and can mislabel mixed changes. These are plausible
tradeoffs, not measured gains for this repository.

For **deterministic automation**, the benefit is established by the documented
parser contract, provided the metadata reaches that consumer. Brackets can work
with a custom parser but are not Conventional Commits. For **AI**, predictable
metadata may help route a task, but it cannot establish intent, correctness, or
compatibility; the diff, contract, rationale, and execution evidence remain
necessary.

Targeted web searches for empirical comparisons of PR prefixes, human
comprehension, and LLM review accuracy found no direct controlled evidence in the
sources examined. This is a search limitation, not a claim that no such study
exists. Research on general commit-message generation or PR-description quality
does not prove that `[feat]` or `feat:` improves AI review accuracy. Do not
present either an AI improvement or an overall user benefit as established.

**Decision:** retain descriptive user-facing titles without mandatory category
prefixes. Preserve bot-specific prefixes. If a concrete release/routing consumer
later warrants standardization, evaluate `feat(scope): description` against that
consumer, verify merge behavior, and measure parsing accuracy, classification
errors, contributor friction, and human/AI outcomes separately before enforcement.

## Alternatives and decisions

| Choice | Alternative considered | Reason for the chosen approach |
| --- | --- | --- |
| Show screenshots or short GIF/video whenever practical, with text and provenance. | Require media for every PR, or accept visual output as sufficient testing. | Terminal/report features also benefit, but internal/docs-only work may not. Accessible transcripts/artifacts can be clearer; media cannot prove assertions or hidden failure paths. |
| Keep one versioned, evolving decision record per feature, reusing an RFC/design document. | Session-only notes, a final reconstructed rationale, or a new record for every small fix. | Reviewers and future maintainers need durable research, unsuccessful experiments, and reasons. Reuse avoids duplicate histories and keeps overhead proportional. |
| Require executable red/green proof for behavioral regressions. | Treat any nonzero baseline exit as proof, or require every new-API test to compile on old code. | Only the intended assertion failure demonstrates sensitivity. New APIs may need a compiling behavior-removal/mutation check; green/green is correct for behavior-preserving work. |
| Challenge material assumptions and compare existing/simpler approaches. | Rubber-stamp the first proposal, or require extra agents and speculative objections for every PR. | Decisions need evidence, but missing rationale is not a proven defect and routine choices do not warrant heavyweight orchestration. |
| Share a feature-delivery skill and wire existing contributor/review entry points. | Duplicate detailed procedures in every workflow or add a new enforcement workflow. | Existing expert/test-review workflows already load the code-review skill. Central guidance reaches them without changing workflow permissions or generated lock files. |
| Map affected consumer boundaries to E2E assertions and inspect actual rendered surfaces at representative dimensions/states. | Rely on formatter/unit tests, one correct reporter, or source-text checks; alternatively require every reporter/IDE and pixel snapshots for every feature. | The reported DataRow regression crosses an external identity/grouping boundary. Risk-based final-output assertions plus rendered checks address that gap without a blanket matrix, new browser dependency, or a claim that payload inspection proves a hosted UI. |

## Research and experiments

- 2026-10-09: Read contributor instructions, the PR template, feature-adjacent
  skills, expert-review routing, test-review publication gates, release settings,
  and title-sensitive workflows. Found an almost empty PR template and existing
  calibrated review gates that distinguish defects from missing evidence.
- 2026-10-09: Used web searches to discover sources, then fetched the primary
  specifications and product documentation above. Discarded unsupported search
  summary claims that prefixes inherently improve AI reliability. Followed
  semantic-release's old documentation redirect to its current official site.
- No product experiment or title A/B trial was run for this documentation change.
  Alternatives above were considered, not implementations attempted.
- 2026-10-09: Formatting validation caught the touched PR template's missing
  final newline; corrected it. A first link-check implementation treated fenced
  attribution placeholders as real links; changed the check to scan prose outside
  fenced examples rather than changing valid reviewer examples to satisfy it.
- 2026-10-30: Read [#11770](https://github.com/microsoft/testfx/issues/11770),
  [#12143](https://github.com/microsoft/testfx/issues/12143), and the latter's two
  attached screenshots. The reporter states that default, full, and arguments-only
  names work in Test Explorer/HTML, while Azure DevOps displays the second row's
  name for both results. The screenshots independently show distinct HTML names
  and repeated Azure DevOps names with separate durations. This is evidence of
  the reported mismatch, not a local reproduction or a new live UI run.
- 2026-10-30: Inspected the existing delivery/acceptance/review guidance and PR
  template. They require packed-consumer provenance, red/green proof, and useful
  media, but do not explicitly require a requirement-to-consumer coverage map or
  distinguish rendered layout from correct HTML/JSON source. Extend those
  existing entry points rather than adding a workflow or duplicating procedures.
- 2026-10-30: Read the open [fix #12150](https://github.com/microsoft/testfx/pull/12150)
  and its changed-file list. It owns the Azure DevOps identity fix and packed
  folded/unfolded-row regression, so this guidance change does not duplicate that
  production work. Its stated payload proof does not claim live Azure DevOps UI
  or release-branch validation; retain that distinction.
- 2026-10-30: Chose representative affected consumers, state/width checks, and
  explicit inaccessible-UI gaps over a mandatory all-products visual matrix.
  Keep existing harnesses and credit adequate existing tests. No new visual-test
  dependency or external publishing authority is introduced.

## Validation and demonstration

The guidance changes in this record are documentation/reviewer guidance, not a
runnable product feature.
Screenshots and product red/green tests are inapplicable; the relevant checks are
final-file/diff inspection, skill metadata and local-link validation, and bounded
review calibration against the cases in the code-review skill.

Completed locally on 2026-10-09:

- `git --no-pager diff --check`: passed, exit 0.
- Inline PowerShell validation: passed, exit 0; checked eight final Markdown
  files, three skill metadata blocks, 44 local links/anchors, and nine
  evidence-contract presence checks. Presence checks establish wiring, not
  runtime reviewer behavior.
- Manually evaluated the eight added review calibration cases: accepted complete
  evidence, green/green equivalence, and labeled safety-net mutations; treated
  missing rationale/media, baseline compilation/restore/skips, and stale-package
  comparisons as evidence gaps rather than invented code defects. Existing
  context/finding/publication gates remain in force.

For the 2026-10-09 guidance change, no product build/test, live review-agent
evaluation, media upload, or GitHub publication was performed. Agentic workflow
sources and generated locks were not changed; their existing code-review-skill
entry points supply the new gates.

Completed locally on 2026-10-30 for the guidance extension:

- Inline PowerShell validation: passed, exit 0; checked six Markdown files,
  32 local links, four anchors, three skill metadata blocks, and preservation of
  the PR template's existing headings/order and comment sections.
- `git --no-pager diff --check`: passed, exit 0.
- `python -m unittest discover -s .github\skills\testfx-acceptance-validation\scripts -p "test_*.py"`:
  passed, exit 0; all 16 execution-verifier self-tests ran. These are evidence-gate
  tests, not product E2E or UI checks.
- Manually evaluated the four added review calibration cases: distinguish a
  verified identity collision from missing consumer evidence; credit content
  assertions without claiming layout proof; accept payload proof with an
  explicit live-UI gap; and credit sufficient existing E2E/rendered evidence
  without demanding duplicate tests or a new stack.

These checks establish guidance consistency and wiring, not automated
enforcement, a live review-agent evaluation, or external UI validation.

### Accompanying display-name safety net

The accompanying
[ArgumentsDisplayNameTests](../../test/IntegrationTests/MSTest.Acceptance.IntegrationTests/ArgumentsDisplayNameTests.cs)
exercise the real packed MSTest consumer through discovery, terminal, TRX, and
HTML, rather than adding more formatter-only tests. The new
`Mtp_RowNamesAndIdentitiesArePreservedInTerminalTrxAndHtml` cases execute both
reflection and source-generated metadata. Each checks 18 discovered identities
and 23 final result rows, including six folded rows sharing one identity,
full-name/arguments-only/default naming, ignored rows, and long/escaped/Unicode
labels with their own output.

Executed locally on 2026-10-30 against base
`5f0e9d66441c483e0bb68af02d0f632fc498cfdf` plus retained test patch SHA-256
`C1DC51F9C842625FC495B04CB8B5FC26807114C6354F99B49353EB0939FF55DF`:

| State | Exact selection | Observation |
| --- | --- | --- |
| Existing baseline | `FullyQualifiedName~MSTest.Acceptance.IntegrationTests.ArgumentsDisplayNameTests` | 15 executed/passed, exit 0. |
| Final safety net | Same class selection | 17 executed/passed, no outer skips, exit 0; independent discovery and TRX/JUnit export verified. |
| Correct packaged report | `FullyQualifiedName~MSTest.Acceptance.IntegrationTests.ArgumentsDisplayNameTests.Mtp_RowNamesAndIdentitiesArePreservedInTerminalTrxAndHtml` | Both cases executed/passed, exit 0. |
| Isolated compiling report mutation | Same two cases/assertions; omit each HTML `displayName` while preserving terminal/TRX | Both execute and fail the exact method/name/outcome assertion, exit 2; child test execution remains correct. |
| Restored correct package | Same two cases/assertions with fresh caches | Both executed/passed, exit 0; original package bytes and consumer DLL provenance verified. |

The mutation is a test-only sensitivity experiment, not a reproduction or fix
of #12143. Product source and assertion bytes were not altered between runs.
MSTest packages were `4.6.0-dev`, MTP/report packages `2.6.0-dev`, repository SDK
`12.0.100-alpha.1.26509.101`, Release/win-x64, outer host net12.0, and the new
generated consumers net11.0. Exact commands and package/cache/binary evidence
are retained in the session and included in the PR description when published.

On 2026-10-31, inspected the actual passing reflection report in the app Browser
at 1280 x 720 CSS pixels and in an embedded browsing context at 638 x 898 CSS
pixels, using the same report DOM/data/scripts rather than fabricated results.
Filtering to the four presentation rows, sorting by name, class grouping,
expanding a long-name row, and an empty search all worked. Literal markup stayed
text; outcome/name/detail associations remained correct. At the narrow width,
long names ellipsized, the duration column was hidden by the responsive layout,
and expanded output wrapped without overlap while controls remained reachable.

The narrow document measured 712 CSS pixels of horizontal content for its
638-pixel viewport, including the long generated assembly-path header. This
pre-existing whole-page overflow is not fixed by these test/guidance changes;
do not describe the entire report as overflow-free. Captures and measurements
are local evidence, not an automated browser regression suite. Live Azure DevOps
Tests UI, IDE Test Explorer, release branches, and CI execution remain unverified.

## Open questions

No new title format is enforced. A future title policy needs a concrete consumer
and measurement rather than an assumed AI benefit. Reviewers report missing
decision/media/test evidence proportionately in the summary; they do not invent
code defects or inline fixes to satisfy these documentation expectations.

The consumer E2E/rendered-check matrix is contributor and reviewer guidance, not
an automated CI gate. A correctly serialized publishing request cannot prove an
external service's presentation. Live hosted/IDE UI validation requires suitable
tooling/access and authorization, and remains unverified when those are absent.
