# GitHub Copilot Instructions

This is a .NET based repository that contains the MSTest testing framework and Microsoft.Testing.Platform (aka MTP) testing platform. Please follow these guidelines when contributing:

## Repository layout

The codebase ships several distinct (but related) products. Knowing which product a change belongs to is essential because they have different conventions, target frameworks, and public API surfaces:

- `src/Platform/Microsoft.Testing.Platform` — Microsoft.Testing.Platform (MTP), a lightweight, in-process test host that replaces VSTest. Most other folders under `src/Platform/` are MTP extensions (`TrxReport`, `CrashDump`, `HangDump`, `HotReload`, `Retry`, `Telemetry`, `HtmlReport`, `AzureDevOpsReport`, `MSBuild`, `VSTestBridge`, …).
- `src/TestFramework` — MSTest itself: the public `Microsoft.VisualStudio.TestTools.UnitTesting` API (attributes, `Assert`, `TestContext`, …) plus `TestFramework.Extensions`.
- `src/Adapter` — bridges MSTest to test hosts: `MSTest.TestAdapter` (VSTest adapter) and `MSTestAdapter.PlatformServices` (platform-services abstraction shared by both hosts).
- `src/Analyzers` — Roslyn analyzers and code fixes shipped as `MSTest.Analyzers`.
- `src/Package/MSTest.Sdk` — the MSBuild project SDK that wires the pieces together for consumers.
- `test/UnitTests/<Project>.UnitTests` — fast unit tests for each project.
- `test/IntegrationTests/<Project>.IntegrationTests` or `<Package>.Acceptance.IntegrationTests` — file-system / process-level tests; acceptance tests consume the packed NuGets from `artifacts/packages/<Configuration>/Shipping`.
- `test/Utilities/TestFramework.ForTestingMSTest` — the internal `TestContainer`-based framework used to unit-test MSTest itself (any public parameterless method on a `TestContainer` subclass is a test; no `[TestMethod]` needed).
- `test/Utilities/Microsoft.Testing.TestInfrastructure` — shared helpers for acceptance/integration tests (test asset fixtures, runners, etc.).
- `eng/` — Arcade-based build infrastructure. Do not hand-edit `eng/common/`: it is mirrored from `dotnet/arcade` and overwritten by automation.

Solution files: `TestFx.slnx` is the full solution; `MSTest.slnf`, `Microsoft.Testing.Platform.slnf`, and `NonWindowsTests.slnf` are filtered views.

## Build, test, and debug commands

Always use the repo-local toolchain via the build scripts — they restore the pinned .NET SDK from `global.json` into `.dotnet/` (or reuse a matching `DOTNET_INSTALL_DIR`) and prepend that `dotnet` location to `PATH`.

| Task | Windows | Linux/macOS |
| --- | --- | --- |
| Restore + build (Debug) | `.\build.cmd` | `./build.sh` |
| Release build | `.\build.cmd -c Release` | `./build.sh -c Release` |
| Produce NuGet packages | `.\build.cmd -pack` | `./build.sh -pack` |
| Unit tests | `.\build.cmd -test` | `./build.sh -test` |
| Integration + acceptance tests | `.\build.cmd -pack -test -integrationTest` | `./build.sh -pack -test -integrationTest` |
| Open the solution in VS with the right env | `.\open-vs.cmd` | n/a |

Acceptance integration tests (anything under `test/IntegrationTests/*.Acceptance.IntegrationTests`) consume the packed NuGets from `artifacts/packages/<Configuration>/Shipping`, so you **must** run `-pack` (and rerun it after every source change you want to test) before invoking them. Plain unit tests do not need `-pack`.

### Running a single test

Once the desired project has been built, invoke its test host directly. Note that CLI options differ by host: `--filter-uid` is available on both MSTest and MTP-based hosts, while `--treenode-filter` is MTP-only:

```powershell
# Filter by UID — works with both MSTest and MTP-based hosts
dotnet run --project test\UnitTests\MSTest.Analyzers.UnitTests -f net8.0 --no-build -c Debug -- --filter-uid <TestUid>

# Tree-node / wildcard filter — MTP-only (faster to type than a UID)
dotnet run --project test\UnitTests\Microsoft.Testing.Platform.UnitTests -f net8.0 --no-build -- --treenode-filter "/*/*/*/MyTestClass/MyTestMethod"
```

For acceptance tests that drive generated assets, prefer running them through the test explorer or `dotnet test --filter "FullyQualifiedName~MyTest"` on the specific project, after `-pack`.

## Code Standards

You MUST follow all code-formatting and naming conventions defined in [`.editorconfig`](../.editorconfig).

All C# and Visual Basic code files (`*.cs`, `*.csx`, `*.vb`, and `*.vbx`) MUST be encoded as UTF-8 with BOM, as required by `.editorconfig`. When creating or rewriting one of these files, preserve or add the BOM; do not emit BOM-less UTF-8.

In addition to the rules enforced by `.editorconfig`, you SHOULD:

- Favor style and conventions that are consistent with the existing codebase.
- Prefer file-scoped namespace declarations and single-line using directives.
- Ensure that the final return statement of a method is on its own line.
- Use pattern matching and switch expressions wherever possible.
- Use `nameof` instead of string literals when referring to member names.
- Always use `is null` or `is not null` instead of `== null` or `!= null`.
- Trust the C# null annotations and don't add null checks when the type system says a value cannot be null.
- Prefer `?.` if applicable (e.g. `scope?.Dispose()`).
- Use `ObjectDisposedException.ThrowIf` where applicable.
- For dictionary initializers, prefer the indexer syntax `[key] = value` over the `Add`-style `{ { key, value } }` initializer when possible (e.g. `new Dictionary<string, int> { ["a"] = 1 }` rather than `new Dictionary<string, int> { { "a", 1 } }`); note that indexer initializers use the indexer setter (different duplicate-key behavior than `Add`).
- Respect StyleCop.Analyzers rules, in particular:
  - SA1028: Code must not contain trailing whitespace
  - SA1316: Tuple element names should use correct casing
  - SA1518: File is required to end with a single newline character

You MUST minimize adding public API surface area but any newly added public API MUST be declared in the related `PublicAPI.Unshipped.txt` file.

## NuGet package metadata guidelines

Every packable project must carry the metadata that nuget.org renders on the package page. This is enforced at pack time by the `_ValidatePackageMetadata` target in the root [`Directory.Build.targets`](../Directory.Build.targets), which fails the build when a packable project is missing either piece — including packages that are `IsShipping=false`, because those still reach nuget.org.

- Write `<PackageDescription>` so it **leads with what the package does**, and close it with `$(CommonProductDescription)` — the shared product sentence, defaulted per product area in [`src/Directory.Build.props`](../src/Directory.Build.props) (MSTest) and [`src/Platform/Directory.Build.props`](../src/Platform/Directory.Build.props) (Microsoft.Testing.Platform). nuget.org truncates the description in search results, so the package-specific sentence has to come first.
- Wrap the value in `<![CDATA[ ... ]]>` and keep its continuation lines unindented. MSBuild trims a property value but not the indentation of its inner lines, so an indented multi-line description leaks the .csproj whitespace into the published text. Property references such as `$(CommonProductDescription)` are still expanded inside CDATA.
- NEVER leave the description unset. NuGet's pack targets substitute the literal placeholder `Package Description` during evaluation, which is why Arcade's own "PackageDescription must be specified" check cannot catch it and why the repo-level guard exists.
- Add a `PACKAGE.md` next to the project file. It is picked up automatically as `PackageReadmeFile` and packed into the root of the `.nupkg`.

## Localization Guidelines

When making change to resource files, you MUST:

- Add a corresponding entry in the resource file (`.resx`).
- NEVER manually modify `*.xlf` files. Instead, regenerate them by running `dotnet msbuild <project>.csproj /t:UpdateXlf` on the owning project (e.g. `src/Platform/Microsoft.Testing.Platform/Microsoft.Testing.Platform.csproj`, `src/TestFramework/TestFramework/TestFramework.csproj`, or the matching analyzer project). A full repo build also regenerates them but is slower.
- A few resource accessors are hand-maintained — notably `PlatformResources.cs` has an `IS_MTP_UNIT_TESTS` block that must be updated when a unit test needs to read a newly added string.
- `{Locked="…"}` markers in a resource `<comment>` are matched as **substrings, not whole words**. A short locked token therefore also freezes every longer word that contains it, which blocks a legitimately translatable word. For example, `{Locked="const"}` on a message that also contains the English word *constant* locks `const` inside `constant`, so translators cannot localize it. Make each locked token unambiguous:
  - Include the punctuation that surrounds the token in the message — usually the single quotes the message already uses — e.g. write `{Locked="'const'"}` rather than `{Locked="const"}`.
  - Prefer the longest form that identifies the token (`{Locked="Assert.AreEqual"}`, `{Locked="[TestClass]"}`) over a bare fragment.
  - Before adding a marker, re-read the whole message and confirm the locked text does not appear as a substring of another word that should stay translatable.

## Public API guidelines

- Treat adding an overload as a potential source-breaking change, even when it is binary-compatible. Existing calls can become ambiguous when an argument converts to multiple parameter types, especially across `Span<T>`, `ReadOnlySpan<T>`, arrays, generic interfaces such as `IEnumerable<T>`, and overloads with optional parameters.
  - Before adding or changing overloads, enumerate representative existing call shapes and compare all applicable implicit conversions and generic type-inference paths.
  - For `Assert` overload changes, update the manually maintained implicit consumer call shapes in [`AssertSourceCompatibilityTests.cs`](../test/IntegrationTests/MSTest.Acceptance.IntegrationTests/AssertSourceCompatibilityTests.cs). The test compiles them against the packed `MSTest.TestFramework` using C# 12 and automatically requires every public `Assert` method family to have at least one representative scenario.
  - Add equivalent package-consuming compilation coverage for overload changes in other public API types, using the oldest relevant default C# language version and target framework. Repository projects use `LangVersion=preview`, so an ordinary in-repo unit test does not detect overload-resolution regressions that only affect older compilers.
  - During review, do not treat successful compilation under the repository's language version as sufficient evidence of source compatibility.
- Public API for MSTest and Microsoft.Testing.Platform MUST NOT use `init` accessors.
  - Exception: Existing APIs in Microsoft.Testing.Platform, because changing them right now would be a breaking change. However, we MUST NOT introduce **new** APIs using `init` accessors.
  - IMPORTANT: Make sure to apply this rule strictly both during PR review and when working on code changes.
- Every API marked with `[Experimental]` MUST include this sentence in its XML documentation `<remarks>`: `This API is experimental. It may change, break, or be removed at any time without notice.` Documentation tooling does not reliably surface the attribute itself.
  - Add the sentence in a `<para>` when `<remarks>` already contains other text; otherwise, add a new `<remarks>` block.
  - Apply this rule to experimental members as well as types.

## Testing Guidelines

- Tests for MTP and the MSTest analyzers MUST use MSTest.
- Unit tests for MSTest itself MUST use the internal test framework in [`TestFramework.ForTestingMSTest`](../test/Utilities/TestFramework.ForTestingMSTest) (a `TestContainer`-based framework where any public parameterless method is a test).
- The assertion style is project-specific and enforced by each project's `BannedSymbols.txt`. Check it before writing assertions:
  - Most MTP unit-test projects (and `MSTest.Analyzers.UnitTests`, `MSTest.SelfRealExamples.UnitTests`) ban `AwesomeAssertions` and require MSTest `Assert`/`StringAssert`/`CollectionAssert`.
  - The adapter unit-test projects (`MSTestAdapter.UnitTests`, `MSTestAdapter.PlatformServices.UnitTests`) ban MSTest's `Assert` family and require `AwesomeAssertions` (FluentAssertions-style API).
- Acceptance integration tests run with assembly-level method parallelization. Classes that share a single generated mutable test asset across multiple methods must be marked `[DoNotParallelize]` to avoid races on `bin/obj` outputs.
- When asserting on test-host output that contains a rendered test **duration** (e.g. `failed MyTest (040ms)`), NEVER hard-code `\(\d+ms\)`. The duration format grows leading parts (`(1s 040ms)`, `(2m 03s 040ms)`, …) on slower machines (often macOS, sometimes Windows), so a `\d+ms`-only pattern is a classic source of timing flakiness. Use the shared `AcceptanceAssert.DurationPattern` constant (or, where a duration only ever applies to skipped tests, the deterministic `(0ms)`) instead.
- Prefer deterministic output, marker, or rendezvous assertions over wall-clock timing. When an acceptance test must assert an upper bound on `Stopwatch.Elapsed` around a real process launch, use a named limit with a generous allowance for process startup, JIT, and teardown on loaded CI agents, and add a comment explaining its relationship to the configured timeout or expected operation.
- When running acceptance tests, you must first run `./build.sh -pack` on Linux/macOS or `.\build.cmd -pack` on Windows.

## CLI options guidelines

When you add a new CLI option, rename an existing one, or change the description/arguments of an existing one (typically by editing an `ICommandLineOptionsProvider` implementation such as `PlatformCommandLineProvider`, `TerminalTestReporterCommandLineOptionsProvider`, `MSTestExtension`'s options provider, or a `*CommandLineOptionsProvider`), you MUST update the corresponding `--help` and `--info` acceptance test expectations so they keep matching the actual output.

All CLI option and extension descriptions must end with terminal punctuation (normally a period).

The wildcard-match expectations live in:

- [`test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/HelpInfoTests.cs`](../test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/HelpInfoTests.cs) — MTP help/info with no extensions registered.
- [`test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/HelpInfoAllExtensionsTests.cs`](../test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/HelpInfoAllExtensionsTests.cs) — MTP help/info with all platform extensions registered.
- [`test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/MSBuild.KnownExtensionRegistration.cs`](../test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/MSBuild.KnownExtensionRegistration.cs) — MSBuild known-extension registration help assertions.
- [`test/IntegrationTests/MSTest.Acceptance.IntegrationTests/HelpInfoTests.cs`](../test/IntegrationTests/MSTest.Acceptance.IntegrationTests/HelpInfoTests.cs) — MSTest help/info.

Keep options sorted alphabetically as they appear in the existing expectation strings, preserve the indentation, and update both the `--help` and the `--info` blocks where the option surfaces. Run the acceptance tests for these files (after `./build.sh -pack` on Linux/macOS or `.\build.cmd -pack` on Windows) to confirm the patterns still match.

## Agentic workflow guidelines

Agentic workflows live in `.github/workflows/*.md` and `*.agent.md` and are compiled to `*.lock.yml` files via the `gh aw` GitHub CLI extension.

- Always compile in **strict mode**. Strict mode is the default unless a workflow's frontmatter sets `strict: false`, so:
  - NEVER add `strict: false` to a workflow's frontmatter.
  - When in doubt, pass `--strict` explicitly to `gh aw compile` to enforce strict-mode validation across all workflows (action pinning, network config, safe-outputs, no write permissions, no deprecated fields).
- After editing any agentic workflow `.md` source (or its frontmatter), run `gh aw compile <workflow-id>` and commit the regenerated `.lock.yml` in the same change. NEVER hand-edit `.lock.yml` files.
- Always review the `.lock.yml` diff after compiling. A locally installed `gh aw` build can silently rewrite action pins — downgrading `actions/checkout` or replacing an immutable SHA with a mutable tag — even when its `compiler_version` header matches CI ([#10258](https://github.com/microsoft/testfx/issues/10258)). Any unintended change to a `uses:` line means the local toolchain is not aligned; recompile on the pinned `github/gh-aw-actions/setup-cli` toolchain instead. Run `python .github/scripts/check_action_pins.py` to verify before pushing; CI enforces the same audit via `.github/workflows/check-action-pins.yml`.

## TODO comment policy

`TODO` comments without a tracked issue are rejected during review. Every `TODO` MUST reference a GitHub issue, e.g. `// TODO(#1234): Refactor this once the new API is available`. If the note doesn't warrant an issue, rewrite it as a plain comment explaining the rationale.

## GitHub issue creation guidelines

When creating new issues — or triaging existing ones — through **any** surface (manual edits in the GitHub UI, `gh issue create` / `gh issue edit`, the REST or GraphQL API, an agentic workflow, a webhook bot, or a label-sync rule) the issue category MUST be expressed through the repository's native **GitHub Issue Type** field. The legacy `type/bug`, `type/feature`, and `type/task` **labels** are banned and MUST NOT be added by anyone (humans, Copilot, bots, or automation).

- Use the `Bug` issue type for an unexpected problem or regression.
- Use the `Feature` issue type for a new capability or enhancement.
- Use the `Task` issue type for a piece of work that is neither a bug nor a feature (refactor, follow-up, chore, RFC follow-up, …).
- `type/bug`, `type/feature`, and `type/task` labels are **deprecated and forbidden**. They duplicate the Issue Type field and make triage queries inconsistent. Do not add them — set the Issue Type field instead.
- Other `type/*` labels (`type/automation`, `type/tech-debt`, `type/test-gap`, `type/regression`, `type/breaking-change`, `type/rfc`, `type/pr-fix`, `type/qa`, `type/ai-inspected`, `type/announcement`, `type/discussion`, `type/flaky-test`, `type/partner-request`, `type/question`) are **not** covered by native issue types and MUST continue to be used as labels.

How to set the Issue Type from each surface:

- **Issue templates** (`.github/ISSUE_TEMPLATE/*.md`): set `type:` in the frontmatter (already done for `bug-report.md` and `feature-request.md`). New templates that map to a native type MUST include the matching `type:` field and MUST NOT list `type/bug` / `type/feature` / `type/task` under `labels:`.
- **GitHub web UI**: pick the type from the "Type" picker in the right sidebar of the issue editor. Do not add `type/bug`, `type/feature`, or `type/task` from the labels dropdown.
- **`gh` CLI / scripts** (current `gh` releases do not yet expose `--type` on `gh issue create`): create the issue, then set the type via GraphQL, e.g.:

  ```bash
  gh api graphql -f query='mutation($issue:ID!, $type:ID!){ updateIssueIssueType(input:{issueId:$issue, issueTypeId:$type}){ issue { number } } }' -F issue=<issue-node-id> -F type=<type-node-id>
  ```

  The available `issueTypeId` values can be listed once with `gh api graphql -f query='query{ repository(owner:"microsoft",name:"testfx"){ issueTypes(first:20){ nodes{ id name } } } }'`.
- **Agentic workflows (`gh aw`)**: in `safe-outputs.create-issue`, set the issue type using the agent's prompt or `allowed-fields` settings; never list `type/bug`, `type/feature`, or `type/task` under `labels`.

## Pull Request guidelines

- Let other developers discuss their comments to your PRs, unless something sounds like a direct order to you, don't do changes.
- Do the changes when you are specifically tagged or mentioned as copilot.
- If you are unsure, comment with the temperature and sentiment of the comment, so we know how to efficiently address you as a member of the team rather than having to tag you.
- PRs that address a security vulnerability (e.g. a Component Governance (CG) alert or a vulnerable dependency bump) MUST avoid disclosing vulnerability details in public PR metadata. Prefer using a private security process (see [`SECURITY.md`](../SECURITY.md)) until the fix ships; if a public PR is unavoidable, use a generic title (e.g. `Update package X`) and a generic description (e.g. `Fix CG alert`) and do NOT spell out the CVE, exploit, affected versions, or attack details.
