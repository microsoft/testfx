# MSTest result-directory isolation: research and decisions

## Problem and contract

Issue [#12598](https://github.com/microsoft/testfx/issues/12598) reports that on modern .NET, when a test run has no `DeploymentItem`, `ResultsDirectory`, `TestRunResultsDirectory`, and `TestResultsDirectory` all fall back to the test assembly directory. A test that writes a result using one of these properties can overwrite an input or a test binary there.

The v5 behavior will create the existing per-run deployment layout when deployment is enabled and there are no deployment items:

- `TestRunDirectory` is the run-specific deployment root.
- `ResultsDirectory` and `TestResultsDirectory` resolve to `In`.
- `TestRunResultsDirectory` resolves to `In\<MachineName>`.
- `DeploymentDirectory` remains the test assembly directory on modern .NET.
- Runs with deployment items keep their existing behavior. .NET Framework keeps its existing behavior, including when there are no deployment items.
- `mstest:deployment:enabled=false` continues to disable deployment. A dedicated setting, `CreateDeploymentDirectoriesWithoutDeploymentItems`, defaults to `true`; setting it to `false` restores the v4 no-deployment-items behavior on modern .NET.
- The setting is exposed through runsettings as `<CreateDeploymentDirectoriesWithoutDeploymentItems>false</CreateDeploymentDirectoriesWithoutDeploymentItems>` and through `testconfig.json` as `mstest:deployment:createDirectoriesWithoutDeploymentItems: false`.
- The existing `DeleteDeploymentDirectoryAfterTestRunIsComplete` policy continues to apply. Result files intended for host collection must be registered with `TestContext.AddResultFile`; the host must retain/copy those attachments before successful-run deployment cleanup.

The producer is `MSTestAdapter.PlatformServices`, shared by the MSTest VSTest adapter and native MTP runner. Validation used `MSTest.TestAdapter` / `MSTest.TestFramework` `5.0.0-dev`, MTP `3.0.0-dev`, and the repository SDK pinned by `global.json` to `12.0.100-alpha.1.26570.125`. The generated package consumer targets .NET Framework 4.6.2, .NET 10, and .NET 11; its outer acceptance host runs on .NET 12. The packed acceptance coverage exercises the native MTP runner. VSTest host execution is not covered by that E2E test; its setting/parser and deployment-service behavior is covered at the unit/service boundary. Windows app-model hosts that do not support deployment remain outside this behavior.

## Research and experiments

- 2026-11-24, source inspection at `c55d0fd50a2276cd3aa9634180449c4e639c080e`: `TestDeployment.Deploy` returned before creating directories on modern .NET when there were no deployment items; `GetDeploymentInformation` then mapped all run/result/deployment properties to the source assembly directory. `DeploymentUtilityBase.CreateDeploymentDirectories` already creates the per-run `TestRunDirectories` layout. `TestContext` documents the shared path relationships.
- 2026-11-24, isolated comparison of MSTest 2.2.10 and 4.5.3: wrote `INPUT` to a test assembly's `example-report.txt`, then wrote `OUTPUT` to the same filename using `TestContext.TestResultsDirectory`. The v2.2.10 run preserved the input; the v4.5.3 run overwrote it. Both test methods passed, so the file contents, not the test exit, were the distinguishing observation. The source and TRX artifacts are retained in the session's task artifact directory, not in the repository.
- 2026-11-24, public GitHub source search: matching snippets included v4 consumers in Azure/bicep, CsvTools, and DeploySharp that write outputs using these result properties. Most reviewed patterns write outputs and do not establish an incompatibility; the search is not an exhaustive affected-consumer census and does not inspect private code or CI collection paths.
- 2026-11-25, inspected existing `DeploymentEnabled` configuration: using it as the compatibility switch would also disable real deployment items, so it cannot safely express this narrow opt-out. The deployment service is shared by VSTest and MTP. Existing successful-run cleanup deletes the deployment root when `DeleteDeploymentDirectoryAfterTestRunIsComplete` is enabled; existing TRX acceptance coverage verifies that registered result attachments survive this cleanup.
- 2026-11-25, Release package validation: `.\build.cmd -pack -c Release -bl` succeeded with zero warnings and errors. The generated `MSTestTrxReport` consumer resolved the packed `5.0.0-dev` MSTest and `3.0.0-dev` MTP packages; `project.assets.json` SHA-512 values and copied .NET 10 runtime assemblies matched the package payloads.
- 2026-11-25, behavior-removal comparison: at base `c55d0fd50a2276cd3aa9634180449c4e639c080e`, `TestDeployment.cs` was left unchanged while the new settings/test support remained. The green and comparison `TrxReportTests.cs` files are byte-identical (SHA-256 `5A58A9F10C8F19194194FC35A2A7FF188162DF5878095C47F648EA08A56C6918`). The two modern .NET default-path assertions failed at `Assert.IsFalse(Path.IsPathRooted(relativePath))`; the unchanged .NET Framework case passed.
- 2026-11-25, CI trace: `TestFx.slnx` includes `MSTest.Acceptance.IntegrationTests`. The Windows Release path runs the full solution on main and collection runs; PRs use the affected-test selector. No CI run for this worktree was inspected, so the exact PR selection of these new dynamic test cases remains unverified.

## Alternatives and decision

- Keep v4 behavior by default and add an opt-in: rejected for v5 because the issue is a real write-collision/data-loss bug and the next major is the agreed compatibility boundary.
- Use `DeploymentEnabled=false` as the opt-out: rejected because it disables deployment items as well as result-directory creation.
- Change `DeploymentDirectory`: rejected; it remains the location of test assemblies on modern .NET and existing consumers may use it for inputs.
- Chosen: create the run-specific layout by default only for modern .NET runs without deployment items, and provide `CreateDeploymentDirectoriesWithoutDeploymentItems=false` in both `.runsettings` and `testconfig.json` for v4-compatible behavior. Keep the existing layout for runs with deployment items and .NET Framework, and preserve the existing cleanup setting and attachment-registration contract.

Reconsider if v4-compatible consumers require an additional opt-out control or if actual host collection is shown to depend on the old assembly-directory location. The attachment-cleanup concern was exercised by the packed acceptance test below.

## Validation and demonstration

Validation used an uncommitted implementation based on `c55d0fd50a2276cd3aa9634180449c4e639c080e`. The green source includes the `TestDeployment.cs` behavior change; the isolated comparison preserves the same test and settings sources but leaves that production file unchanged. Both states were packed in Release before package-consuming acceptance execution.

| Requirement / risk | Automated E2E evidence | Rendered check / gap |
| --- | --- | --- |
| Modern .NET default and v4-compatible opt-out | Packed MTP consumer test `TrxReport_TestContextDirectories_DefaultAndCompatibilitySetting` ran for net10.0 and net11.0. Default paths were asserted under the unique configured run root and not under the assembly directory; opt-out paths were asserted to preserve the assembly-directory fallback. | Nonvisual filesystem contract. |
| .NET Framework and unchanged deployment behavior | The same test ran on net462 and passed, preserving the existing behavior. | No visual surface applies. |
| Result attachment survives cleanup | Packed test `TrxReport_ResultFilesUnderTestResultsDirectory_SurviveDeploymentCleanup` ran on net462, net10.0, and net11.0; it inspected the TRX attachment and copied payload after the child host exited. | Actual TRX and attachment bytes were checked; no visual surface applies. |
| Configuration and service behavior | `MSTestAdapterSettingsTests` and `TestDeploymentTests` passed in the Release/net11.0 PlatformServices unit-test run (2,235 passed, 0 failed, 0 skipped). | This includes XML and configuration parsing; it does not replace VSTest-host E2E execution. |
| VSTest host and external UI | The packed consumer above runs under MTP; no packed VSTest-host run or hosted UI was exercised. | VSTest E2E and IDE/hosted rendering remain unverified; the changed contract is filesystem path selection, not rendered output. |

**Test-value evidence:** the focused Release acceptance selection executed all six planned outer cases, with three data rows (`net462`, `net10.0`, `net11.0`) for each test. The green run passed 6/6, failed 0, skipped 0, outer process exit 0. Its exact invocation is retained in `artifacts\TestResults\Release\issue12598-final-commands\execution-command.txt`; the verifier output, fresh outer TRX, execution log, and binlog are in that directory. The isolated behavior-removal run used the same three default-path identities and assertions: 3 executed, 2 failed, 1 passed, 0 skipped, outer exit 2. Both modern .NET failures were the intended path-containment assertion; net462 passed. Its exact command and red-phase verification are retained in the counterfactual worktree under `artifacts\TestResults\Release\issue12598-counterfactual-final-command\`.

The green package SHA-256 values are recorded in `artifacts\TestResults\Release\issue12598-final-evidence\package-provenance.json`: `MSTest.5.0.0-dev.nupkg` `DF3D5FA25BFA4253368C943788EC70F902C6F9689D43EF242DEFCE6ACFDFD023`; `MSTest.TestAdapter.5.0.0-dev.nupkg` `51FCE1093C1D19E4D0B46A41A1D50B752A6BA9E52EDB6B60E1001DA1D0D87F46`; `MSTest.TestFramework.5.0.0-dev.nupkg` `A92D7B5FACE810D9ADB85F938F1E6AD702DACE3956AD572BD2ED5132996D8393`; `Microsoft.Testing.Platform.3.0.0-dev.nupkg` `62EFE1B82E831EB8D1F0B1AAE2761F77EC5428D18462E11FA8374DF84D0BA00C`. The counterfactual manifest at `..\issue12598-counterfactual\artifacts\TestResults\Release\issue12598-counterfactual-provenance\package-provenance.json` records its separate package hashes, resolved SHA-512 matches, copied runtime-asset matches, and red TRX. These evidence files are local, untracked build artifacts and have not been published.

The full Windows Release pipeline includes the acceptance project through `TestFx.slnx`; `SkipTests` defaults to false. On PRs, however, the Windows Release step uses `--affected-tests`, and a successful zero-test selection does not trigger the full-suite fallback. The exact PR selection and executed result identities were not available, so CI execution for this change is **unproven**. Main Release/full validation includes the project, but no CI result for this worktree was inspected. The app-model job's category/FQN filters do not select these tests.

## Open questions

- Decide whether to add a packed VSTest-host acceptance case before release; the shared service and settings are covered by unit tests, but that host-specific consumer path was not exercised end to end.
- Before release, inspect the actual CI TRX and logs for the Windows PR affected-test run and confirm these exact dynamic test identities executed. The current PR selector may report success with zero selected tests.
- Keep one MSTest maintainer responsible for default/opt-out parity across VSTest and MTP and for any release-branch servicing decision.
