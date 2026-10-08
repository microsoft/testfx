---
name: testfx-localization-validation
description: Validate generated TestFx localization catalogs, resource IDs, placeholders and locked tokens. Use for RESX/XLF changes, OneLoc PRs, localization build failures, duplicate targets or units, and orphaned translation entries.
---

# TestFx localization validation

Run from the repository root with Python 3.12 or newer; no third-party packages
or .NET build are needed for the guard:

```powershell
python .github\scripts\test_check_localization.py
python .github\scripts\check_localization.py
```

On Linux/macOS use `/` in command paths. The standalone
`check-localization.yml` workflow runs the same commands on localization PRs.
The guard is read-only. Exit codes are `0` for clean, `1` for catalog findings,
and `2` for invocation/discovery errors (including no catalogs).

## Runtime choice

Python is a measured exception to the preference for PowerShell when there is
no performance, feature or security impact. On Windows with Python 3.12.10 and
PowerShell 7.6.6, nine interleaved warm-cache runs (including process startup)
gave these medians:

| Workload | Complete Python guard | PowerShell feasibility probe |
| --- | ---: | ---: |
| All 364 catalogs / 19,695 units | 1.830 s | 3.686 s |
| Clean 13-locale fixture | 0.272 s | 0.825 s |
| Empty runtime startup | 0.063 s | 0.468 s |

The dependency-free probe used .NET XML readers, cached neutral resources and
ordinal IDs. It implemented only a subset of validation, **not feature parity**;
it already exceeded the complete guard's runtime. Python is retained rather than
accepting that regression or weakening the checks. These measurements are not a
claim about every possible PowerShell implementation or other operating systems.
A future port must demonstrate no performance regression and pass the committed
regression suite, including case-sensitive IDs/tokens and literal/escaped
placeholder semantics. Explicit .NET XML-reader security settings and DTD
handling were independently exercised in the feasibility probe, not by the
Python regression suite. A port must separately verify those settings and
compare accepted XML-input profiles; passing the suite alone does not prove
XML security parity.

## Rules and limits

- TestFx uses XLIFF 1.2 with the OASIS namespace and a relative `file original`
  pointing to the neutral RESX. Resolve it relative to the **catalog**, not the
  project or repository. Do not confuse locale `cs` (Czech) with a C# file.
- The [XLIFF 1.2 transitional schema](https://docs.oasis-open.org/xliff/v1.2/os/xliff-core-1.2-transitional.xsd)
  requires one direct `source` and permits **zero or one** direct `target` per
  `trans-unit`. Do not require a target to exist. Targets inside `alt-trans`
  are alternatives, not duplicates. Groups are legal. Unit IDs are unique
  within a `file` across `trans-unit` and `bin-unit`, not globally across files.
- Generated units must correspond to neutral string resource IDs, and their
  source text must match the neutral value exactly. When the neutral comment
  is nonempty, a note must match it exactly.
  Match [XliffTasks' RESX exclusions](https://github.com/dotnet/xliff-tasks/blob/main/src/Microsoft.DotNet.XliffTasks/Model/ResxDocument.cs):
  typed/binary entries, designer names (`>>...`, `*.LayoutSettings`), whole-value
  `{Locked}` comments, and empty/whitespace values do not require units.
  Additional notes are legal.
  An absent target or empty unfinished target is allowed; unfinished
  translations are not evidence of a structural defect.
- Numeric composite-format argument **indices** must be preserved; reordering,
  repetition, alignment and format specifier changes are not automatically
  translation defects. Escaped braces are supported. Named templates such as
  `{tfm}` and literal JSON are not assumed to be .NET composite formats.
  When source text is not unambiguously a composite format, manually check any
  embedded numeric placeholders. This is not a complete `String.Format` parser.
- Every `{Locked="..."}` token must occur verbatim in the neutral value and
  in a nonempty/completed target. Matching is case-sensitive and substring-based.
  The guard checks presence, not grammar or a word's translatability.
  Manually check for collisions with other words in the same value: `const`
  also matches *constant*. Prefer the longest unambiguous invariant token.
  Lock punctuation only when invariant or necessary to disambiguate.
- This is a focused guard, **not a full XSD validator or translation-quality
  review**. Inline XLIFF codes require producer-specific rendering and are
  reported for manual handling instead of silently flattened. Do not invent
  new bans on otherwise legal XLIFF solely to make the checker pass.

## Diagnose and repair at the authoritative source

Catalog-derived findings are grouped by neutral file, resource ID and defect,
with every affected catalog path listed. Findings about the neutral RESX itself
(XML/root/ID/value/locked-source errors) are emitted once for that file without
listing its referencing catalogs. Treat one bad resource copied across 13
locales as one root cause, not 13 independent review requests.

The default command audits the entire current checkout, including tracked and
non-ignored new catalogs. A finding is **not automatically introduced by the
PR**. Compare the same check on the PR base before attributing older drift to
the author. Do not silently repair old entries, suppress failures with a broad
allowlist, or assume removing an orphan from one locale repairs all catalogs.
The initial repository baseline was clean; no grandfathered ID list is needed.

For developer-owned values, IDs or comments, edit the neutral RESX only.
Regenerate with the **owning project's** `UpdateXlf` target, using the pinned
repository SDK initialized by the build scripts. For example, on Windows:

```powershell
.\build.cmd
.\.dotnet\dotnet.exe msbuild src\Platform\Microsoft.Testing.Platform\Microsoft.Testing.Platform.csproj /t:UpdateXlf -bl:artifacts\log\localization-update.binlog
python .github\scripts\check_localization.py
```

Use the corresponding project for adapter, framework, analyzer or extension
resources. If `DOTNET_INSTALL_DIR` supplies the repo's pinned SDK, use that
`dotnet` instead. Regeneration is authoritative for source text, IDs and notes;
it does not prove that a translation is linguistically correct. Inspect the
generated diff and run the affected project's normal build when assessing a
localization build failure.

**Never hand-edit generated XLF.** For duplicate targets, bad translations,
translated placeholders or locked tokens in a OneLoc submission, route the
grouped diagnostics to the OneLoc producer/maintainers and request a corrected
generated submission. Regenerate developer-owned source metadata as appropriate;
do not fabricate translated text or assume `UpdateXlf` repairs upstream
translation quality. Re-run the guard on the corrected output.
