<!--
- Add a brief summary of what this Pull Request is about.
- Add a link to the issue this Pull Request relates to.
-->

# Pull request

## Summary

<!-- Describe the user-visible change, affected product(s), and related issue. -->

## Design and decision record

<!--
For a new feature, link the maintained design/RFC or docs/decisions/<feature-name>.md.
Include research, approaches actually tried and their outcomes, alternatives,
assumptions, and why this direction was chosen. Challenge material decisions,
especially if the request supplied no rationale. For other changes, link existing
rationale or explain briefly why a separate record is not needed.
-->

## Demonstration

<!--
Whenever practical, add screenshots or a short GIF/video showing the feature
working, including terminal/report changes. Add captions, reproduction commands,
and source/package provenance; prefer before/after when behavior changed.
Use a readable text transcript or artifact when clearer, and explain unavailable
or inapplicable media. Redact sensitive data. Media is not test execution proof.
For output/layout changes, record the actual rendered surface, viewport/terminal
dimensions, relevant long-content/empty/error states, and interactions inspected.
Check clipping, wrapping, alignment, and reachable controls. Generated HTML/JSON
or a publishing payload alone does not prove the consuming UI; name inaccessible
hosted/IDE surfaces as validation gaps, not completed visual checks.
-->

## Validation and regression proof

<!--
List each exact command with its result, source state, configuration/TFM, process
exit, executed test identities/counts, and log/result artifact links.
For new regression coverage, retain the tests and remove the relevant production
change in an isolated comparison. Include the intended assertion failure without
the change (red) and the passing run with it (green). Build/restore/launch failures,
zero tests, and skips are not red-phase proof.
For behavior-preserving changes, show green-before/green-after. If a new API
cannot compile on the baseline, use a compiling behavior-removal/mutation check
and label it accurately, or explain why regression sensitivity remains unproven.
For package/acceptance changes, repack each compared state and verify the consumer
uses those bits. State what was not run and any remaining evidence gap.
For features/changed consumer boundaries, map requirements to exact automated E2E
cases and affected consuming surfaces. Assert final identities/grouping, counts,
and each result's associated name/outcome, not just text presence or a correct
intermediate object. Credit adequate existing tests; unit tests or one correct
reporter do not prove another consumer. Include rendered checks/gaps in the map.
See .github/skills/feature-delivery/SKILL.md for the full evidence contract.
-->

### Test-value evidence

<!--
For behavior changes, identify the tested revision, contract/test case, and
counterfactual (pre-fix behavior or a buildable disabled/wrong feature).
Report the result without the behavior and with the correct implementation.
Include exact commands, results, executed test counts, and artifacts when run.
Distinguish executed proof from static predictions or unavailable evidence.
For refactors, passing on both revisions is expected. If not applicable,
explain why no executable behavior changed.
See .github/skills/testfx-test-value-evidence/SKILL.md.
-->
