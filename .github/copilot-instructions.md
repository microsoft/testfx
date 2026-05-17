# GitHub Copilot Instructions

This is a .NET based repository that contains the MSTest testing framework and Microsoft.Testing.Platform (aka MTP) testing platform. Please follow these guidelines when contributing:

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
- Respect StyleCop.Analyzers rules, in particular:
  - SA1028: Code must not contain trailing whitespace
  - SA1316: Tuple element names should use correct casing
  - SA1518: File is required to end with a single newline character

You MUST minimize adding public API surface area but any newly added public API MUST be declared in the related `PublicAPI.Unshipped.txt` file.

## Localization Guidelines

When making change to resource files, you MUST:

- Add a corresponding entry in the resource file (`.resx`).
- NEVER manually modify `*.xlf` files. Instead, build the project to automatically generate the corresponding `*.xlf` files.

## Public API guidelines

- Public API for MSTest and Microsoft.Testing.Platform MUST NOT use `init` accessors.
  - Exception: Existing APIs in Microsoft.Testing.Platform, because changing them right now would be a breaking change. However, we MUST NOT introduce **new** APIs using `init` accessors.
  - IMPORTANT: Make sure to apply this rule strictly both during PR review and when working on code changes.

## Testing Guidelines

- Tests for MTP and MSTest analyzers MUST use MSTest.
- Unit tests for MSTest MUST use the internal test framework defined in [`TestFramework.ForTestingMSTest`](../test/Utilities/TestFramework.ForTestingMSTest).
- All assertions must be written using FluentAssertions style of assertion.
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

## Pull Request guidelines

- Let other developers discuss their comments to your PRs, unless something sounds like a direct order to you, don't do changes.
- Do the changes when you are specifically tagged or mentioned as copilot.
- If you are unsure, comment with the temperature and sentiment of the comment, so we know how to efficiently address you as a member of the team rather than having to tag you.
