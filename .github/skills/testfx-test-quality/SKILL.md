---
name: testfx-test-quality
description: Turn bounded TestFx test-quality assessments into concrete improvements and Copilot follow-up. Use with grade-tests, test-quality-auditor, test-engineer, and PR test reviews when a test needs improvement or essential evidence is uncertain.
---

# Test quality follow-through

Apply the upstream [grade-tests](../../../.agents/skills/grade-tests/SKILL.md)
rubric unchanged. This adapter governs feedback and follow-through, not scoring.
Read [repository instructions](../../copilot-instructions.md) and preserve the
caller's scope and permissions. Improving guidance is not permission to review
or modify the example PR.

## Investigate before handing off

Resolve doubts with bounded read-only inspection of the named test, helpers,
production contract, and accessible revision-matched evidence. Do not stop at
the first missing artifact. Do not run untrusted PR code, invent expected
values, or classify missing execution proof alone as a defective test.
Missing production context alone does not override a decisive body-level
result; retain the rubric's distinction between Uncertain and N/A / unverified.

## Make the next action explicit

Preserve the rubric's Result and Quality. Supplement its report with a
**Follow-up** field; do not overwrite the upstream skill's Pass/None convention.
Every Failed or Uncertain result, supported improvement, and material evidence
gap needs a disposition:

- **Improve:** state the exact input/assertion/change, independently justified
  expected outcome, and wrong behavior it would catch or diagnostic benefit.
  A Pass result can still have a supported improvement. For a below-A grade,
  explain each meaningful deduction and investigate a way to improve it;
  never silently replace an identified improvement with `None`.
- **Investigate:** name the unresolved question, affected test/contract,
  missing evidence, bounded next action, and the observation that would settle
  it. Uncertain is work to resolve, not a terminal informational label.
- **No change justified:** explain why the lower grade does not expose a
  meaningful weakness, such as one exact assertion fully protecting a narrow
  contract. Do not demand redundant assertions, splits, or churn to earn A.

For example, a wildcard failure-message check needs investigation of which
parts are deterministic. If it can miss the wrong count or missing user text,
propose the exact deterministic message/block with legitimate variable parts
normalized. Do not replace legitimately variable output with a flaky equality.
Counting diagnostics by name can miss duplicated or misplaced diagnostics:
when identity/location is part of the contract, propose the expected
diagnostic-to-source association, not just another total.

## Prefer feedback at the test method

When review publication is authorized, prefer an **inline test-method review
comment** for any supported improvement or unresolved doubt. Anchor on the
changed assertion, case, or method signature; for embedded test source, use
the owning real test method or changed source block and name both. Do not
invent a test-method score for helper/raw-string source.

Distinguish **Improvement** (supported change) from **Investigation**
(unresolved evidence, not an asserted defect). Include the test identity,
evidence/reason, exact next action, and acceptance criterion. Use a complete
apply-ready suggestion only when the edit and expected value are established;
questions and missing evidence need text, not speculative code.

Direct actionable comments to **@copilot**: ask it to make and verify the
supported change, or investigate the specific uncertainty and report the
evidence before changing expectations. For missing execution proof, request
the exact named test/variant, repository-pinned command, executed counts,
exit and decisive assertion; do not request a broad rerun with no criterion.
This is an explicit work request, not proof that Copilot accepted or ran it.

Keep the [code-review publication safeguards](../code-review/SKILL.md):
live open/head checks, valid diff anchors, authorization, deduplication and
comment caps. One representative comment per root cause can name multiple
tests; do not fan out identical concerns. Reuse an applicable existing thread
and cite it instead of posting a duplicate. Read its replies/disposition;
an unresolved concern still needs a next action in the summary, not silence.
If the anchor, thread context, or budget prevents inline feedback, put the
same concrete Copilot request in a **Follow-up** summary with the reason.
Never leave a Failed/Uncertain or supported improvement without an action
or an existing-thread reference and disposition.

## Close the loop within the authorized mode

Read-only auditors return the findings and work requests; they do not fix
tests or claim evidence was executed. The automatic reviewer queues one COMMENT
review with inline requests; it does not request changes or launch a fixer.
Bot-generated mentions are not a guaranteed trigger, and the existing
`address-review` workflow handles changes_requested reviews only. Report
follow-up as requested/queued, never started, fixed, or verified without proof.
The agentic review's safe-output policy must explicitly allow `copilot`
mentions; otherwise the publisher neutralizes the work request as code text.
Keep arbitrary collaborator/context mentions disabled and bot mentions bounded.

For an authorized audit-and-fix task, the invoking test-engineer owns the
checklist: investigate Uncertain items first, implement supported in-scope
improvements, and verify the claimed outcomes with the smallest relevant
tests. Preserve assertions and intended behavior. Apply
[test-value evidence](../testfx-test-value-evidence/SKILL.md) where relevant.
Report each item as verified, no change justified with evidence, or blocked
with the exact missing capability/evidence. Do not stop at the scorecard or
claim completion while an actionable in-scope item remains unresolved.
