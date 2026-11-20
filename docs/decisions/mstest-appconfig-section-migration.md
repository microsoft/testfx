# MSTest app.config section migration

## Problem and contract

MSTest's .NET Framework `TestConfiguration.ConfigurationSection` reads the canonical `microsoft.visualstudio.testtools` section and currently falls back to the Beta2 name `microsoft.visualstudio.qualitytools`. MSTest 5 removes only that fallback. A configuration using the canonical section continues to load data sources; a configuration using only the Beta2 name now gets the existing empty-section default. The `DataSourceAttribute` XML documentation also identifies the canonical section. Data-source support, the canonical section, and other .NET Framework configuration behavior are unchanged. This change does not affect Microsoft.Testing.Platform 3.

The producer is `MSTest.TestFramework` and its .NET Framework assembly; the consuming surface is a .NET Framework test application's `App.config`, read by `TestConfiguration`. The focused package consumer is the generated `net472` application in `DataSourceTests.TestDataSourceFromAppConfig`, which uses the shipping MSTest framework and adapter packages.

## Research and experiments

Public GitHub code-search snapshot, inspected before this change:

- Across 157 candidate configuration files in 96 repositories, no inspected file used the exact `microsoft.visualstudio.qualitytools` section name. Searches also found legacy assembly-name mentions, so candidate files were checked rather than treating raw search counts as section usage.
- After excluding checked-in build outputs, the canonical section appeared in 41 files across 34 repositories; 33 of those files contained `dataSources`.
- Search entry points: [legacy section name](https://github.com/search?q=%22microsoft.visualstudio.qualitytools%22&type=code), [canonical section name](https://github.com/search?q=%22microsoft.visualstudio.testtools%22&type=code).

This is a public indexed-source snapshot, not an adoption census. It cannot establish use in private or unindexed repositories, generated or uncommitted configurations, or guarantee that no application depends on the Beta2 alias.

The existing unit test `ConfigurationSelectionUsesRtmThenBetaThenDefaultInIsolatedDomains` directly demonstrates the contract: an RTM section wins, and an old-name-only configuration currently returns its `"beta"` data source. The regression expectation is being changed to require the empty default. Its AppDomains each use a unique temporary config file.

## Alternatives and decision

Keeping the fallback would preserve compatibility for any remaining Beta2-only configuration, but would also retain an obsolete section alias without evidence of current public use. Adding a separate warning or compatibility switch would enlarge the behavior and migration surface without an established mechanism or requirement. Remove the fallback in MSTest 5, where breaking changes are expected, and direct users to rename both the `<section name="...">` declaration and the matching section element to `microsoft.visualstudio.testtools`.

The canonical app.config consumer remains in scope to guard supported data-source behavior. No app.config or data-source feature is removed.

## Validation and demonstration

| Requirement / risk | Automated evidence | Rendered check / gap |
| --- | --- | --- |
| Beta2-only configuration no longer loads | `ConfigurationSettingsTests.ConfigurationSelectionUsesRtmThenDefaultInIsolatedDomains`: red against the old loader; green after removal. | Configuration behavior is nonvisual. |
| Canonical data sources still work in a shipped consumer | `DataSourceTests.TestDataSourceFromAppConfig`: generated `net472` consumer uses packed MSTest 5.0.0-dev packages; outer test passes and asserts the child summary is 4 passed / 1 failed. | No rendered UI is affected. |
| Other framework/platform surfaces remain unchanged | Debug build covered the framework's `net462`, `net10.0`, and `net11.0` outputs and the unit-test project's `net48`, `net10.0`, and `net11.0` outputs. The changed code is inside `#if NETFRAMEWORK`; MTP is not changed. | Not applicable. |

Test-value evidence is bound to base revision `f9346510267652cffb5013d2b29949035a40fcda` and the uncommitted worktree patch recorded in the handoff.

| Contract / test case | Counterfactual | Without behavior | Correct implementation | Evidence |
| --- | --- | --- | --- | --- |
| Beta2-only config returns the empty default / `ConfigurationSelectionUsesRtmThenDefaultInIsolatedDomains` | Ran the same test and assertion against the existing Beta2 fallback. | Executed: detected; 1 test failed at the intended assertion (`"beta"` actual, `"empty"` expected), process exit 2. | Executed: passed; 1 test passed, 0 failed or skipped, process exit 0. | `artifacts/log/Debug/beta2-red-7ed05aa4591f43649bcbd921c3f3aa63/execution.log`; `artifacts/log/Debug/beta2-green-5723b60649be4adab389a718b5bc5d57/unit-test.log`. |
| Canonical app.config data sources work in the shipping package / `TestDataSourceFromAppConfig` | Not applicable to this supported-path preservation case. | Static: the unit test and old branch establish the legacy behavior; no separate old-package acceptance run. | Executed: outer test passed, 1 selected/executed/passed, 0 failed or skipped. The child was expected to return `AtLeastOneTestFailed`; the outer assertions verified its 4 passed / 1 failed / 0 skipped summary. | `artifacts/log/Release/beta2-acceptance-final/MSTest.Acceptance.IntegrationTests_net12.0_x64.trx`; package hashes below. |

Commands and provenance:

- `.\build.cmd -projects src\MSTest\TestFramework\TestFramework.csproj -c Debug -bl` — final build after correcting the `DataSourceAttribute` XML documentation; exit 0 with 0 warnings/errors for `netstandard2.0`, `net462`, `net10.0`, and `net11.0`. Generated XML documentation for all four targets uses `microsoft.visualstudio.testtools`.
- `.\build.cmd -projects test\UnitTests\TestFramework.UnitTests\TestFramework.UnitTests.csproj -c Debug -bl` — exit 0 before and after the production change. The same runner filter was `Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Extensions.ConfigurationSettingsTests.ConfigurationSelectionUsesRtmThenDefaultInIsolatedDomains`; the run used the built `net48` host and the framework's `net462` asset.
- `.\artifacts\bin\TestFramework.UnitTests\Debug\net48\Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.exe --filter-uid Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Extensions.ConfigurationSettingsTests.ConfigurationSelectionUsesRtmThenDefaultInIsolatedDomains` — baseline exit 2 (one intended assertion failure), then corrected implementation exit 0 (one pass).
- `.\build.cmd -pack -c Release -bl` — exit 0 with 0 warnings/errors and successful package content/metadata validation. The final shipping feed contained `MSTest.TestFramework.5.0.0-dev.nupkg` (SHA-256 `56AFC0BC8D5087804ECD31BEB83D499B8754327180D8B11A45A457D4FF0AD1BB`) and `MSTest.TestAdapter.5.0.0-dev.nupkg` (SHA-256 `00D47FE504474ED9608AB7A21221D634377E3B4F69A2048940116AC29A1BBFC8`).
- `.\.dotnet\dotnet.exe build test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release "-bl:artifacts\log\Release\beta2-acceptance-a867f4129dff44f99272380b146b49a7\build.binlog"` — exit 0. The acceptance host was `net12.0`.
- Discovery listed 1,566 acceptance tests; the structured result identified `TestDataSourceFromAppConfig` as UID `1d1fb7c0-a44e-8b5a-b76d-06578e1e2803`. Filtered discovery found exactly that 1 test.
- With `NUGET_PACKAGES=artifacts\log\Release\beta2-acceptance-final\.packages-verified`, `.\.dotnet\dotnet.exe test --project test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -c Release --no-build --no-ansi --no-progress -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=artifacts\log\Release\beta2-acceptance-final" -- --filter-uid 1d1fb7c0-a44e-8b5a-b76d-06578e1e2803 --show-test-results all` — exit 0. The final TRX reports `TestDataSourceFromAppConfig` passed, 1 total, 0 failed or skipped.
- The acceptance fixture generated and built its `net472` consumer against MSTest 5.0.0-dev from the configured local shipping feed; its outer assertions verified the expected child summary of 4 passed / 1 failed / 0 skipped. The final framework package XML contains the canonical section name and no Beta2 name.

Two command-line attempts were corrected and are not product evidence: forcing `TargetFramework=net48` in the build excluded the referenced `net462` restore target (`NETSDK1005`), so the repository build ran the full framework graph; and passing `--results-directory` both to the MTP test CLI and through `ArtifactsTestResultsDir` created a duplicate option, so the final run used the project property only. The first legacy-filter guess was also rejected by discovery; the recorded GUID came from the structured `--list-tests json` output.

The inner data-source fixture includes one deliberately failing CSV row (`expectedSum` is 1 while the inputs sum to 2); the outer acceptance test expects that child failure and validates the summary. CI has a capable path: Windows `TestDebug` runs every `*UnitTests.exe`, including the exact regression test, and `TestFx.slnx` includes the acceptance project. The Windows Release step runs the full solution when affected tests are disabled and otherwise uses affected-test selection, with a full-solution fallback on failure (scheduled/manual runs remain full). The exact CI selection and execution were not run locally; only the focused Windows acceptance case was exercised.

## Open questions

None for this migration decision. Public search limitations remain as stated above; the migration note is the compatibility path for users not represented by that search.
