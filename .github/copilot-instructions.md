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

## Localization Guidelines

When making change to resource files, you MUST:

- Add a corresponding entry in the resource file (`.resx`).
- NEVER manually modify `*.xlf` files. Instead, regenerate them by running `dotnet msbuild <project>.csproj /t:UpdateXlf` on the owning project (e.g. `src/Platform/Microsoft.Testing.Platform/Microsoft.Testing.Platform.csproj`, `src/TestFramework/TestFramework/TestFramework.csproj`, or the matching analyzer project). A full repo build also regenerates them but is slower.
- A few resource accessors are hand-maintained — notably `PlatformResources.cs` has an `IS_MTP_UNIT_TESTS` block that must be updated when a unit test needs to read a newly added string.

## Public API guidelines

- Public API for MSTest and Microsoft.Testing.Platform MUST NOT use `init` accessors.
  - Exception: Existing APIs in Microsoft.Testing.Platform, because changing them right now would be a breaking change. However, we MUST NOT introduce **new** APIs using `init` accessors.
  - IMPORTANT: Make sure to apply this rule strictly both during PR review and when working on code changes.

## Testing Guidelines

- Tests for MTP and the MSTest analyzers MUST use MSTest.
- Unit tests for MSTest itself MUST use the internal test framework in [`TestFramework.ForTestingMSTest`](../test/Utilities/TestFramework.ForTestingMSTest) (a `TestContainer`-based framework where any public parameterless method is a test).
- The assertion style is project-specific and enforced by each project's `BannedSymbols.txt`. Check it before writing assertions:
  - Most MTP unit-test projects (and `MSTest.Analyzers.UnitTests`, `MSTest.SelfRealExamples.UnitTests`) ban `AwesomeAssertions` and require MSTest `Assert`/`StringAssert`/`CollectionAssert`.
  - The adapter unit-test projects (`MSTestAdapter.UnitTests`, `MSTestAdapter.PlatformServices.UnitTests`) ban MSTest's `Assert` family and require `AwesomeAssertions` (FluentAssertions-style API).
- Acceptance integration tests run with assembly-level method parallelization. Classes that share a single generated mutable test asset across multiple methods must be marked `[DoNotParallelize]` to avoid races on `bin/obj` outputs.
- When running acceptance tests, you must first run `./build.sh -pack` on Linux/macOS or `.\build.cmd -pack` on Windows.

## CLI options guidelines

When you add a new CLI option, rename an existing one, or change the description/arguments of an existing one (typically by editing an `ICommandLineOptionsProvider` implementation such as `PlatformCommandLineProvider`, `TerminalTestReporterCommandLineOptionsProvider`, `MSTestExtension`'s options provider, or a `*CommandLineOptionsProvider`), you MUST update the corresponding `--help` and `--info` acceptance test expectations so they keep matching the actual output.

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
- PRs that address a security vulnerability (e.g. a Component Governance (CG) alert or a vulnerable dependency bump) MUST avoid disclosing vulnerability details in public PR metadata. Prefer using a private security process (see `SECURITY.md`) until the fix ships; if a public PR is unavoidable, use a generic title (e.g. `Update package X`) and a generic description (e.g. `Fix CG alert`) and do NOT spell out the CVE, exploit, affected versions, or attack details.
