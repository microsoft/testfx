# Feature delivery guidance: research and decisions

## Problem and contract

Feature PRs should explain not only what changed, but why that direction was
chosen and what demonstrates it works. The requested guidance covers practical
screenshots/recordings, research into title prefixes, maintained research and
experiment records, regression sensitivity without the production change, and
constructive challenge of design decisions when rationale is missing.

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

## Validation and demonstration

This is documentation/reviewer guidance, not a runnable product feature.
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

No product build/test, live review-agent evaluation, media upload, or GitHub
publication was performed. Agentic workflow sources and generated locks were not
changed; their existing code-review-skill entry points supply the new gates.

## Open questions

No new title format is enforced. A future title policy needs a concrete consumer
and measurement rather than an assumed AI benefit. Reviewers report missing
decision/media/test evidence proportionately in the summary; they do not invent
code defects or inline fixes to satisfy these documentation expectations.
