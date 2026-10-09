# Contributing to MSTest

Welcome, and thank you for your interest in contributing! Your contributions to open source, large or small, make projects like this possible.

There are many ways to contribute:

- [Submit issues](https://github.com/Microsoft/testfx/issues) and help verify fixes as they are checked in.
- Review the [source code changes](https://github.com/Microsoft/testfx/pulls).
- Contribute to the documentation.

## Creating Issues

- **DO** use a descriptive title that identifies the issue to be addressed or the requested feature.
- **DO** specify a detailed description of the issue or requested feature.
- **DO** provide the following for bug reports
  - Describe the expected behavior and the actual behavior. If it is not self-evident such as in the case of a crash, explain why the expected behavior is expected.
  - Provide example code that reproduces the issue.
  - Specify any relevant exception messages and stack traces.
- **DO** subscribe to notifications for the created issue in case there are any follow-up questions.

## Submitting Pull Requests

- **DO** ensure submissions pass all Azure DevOps legs and are merge conflict-free.
- **DO** follow the [.editorconfig](http://editorconfig.org/) settings for each directory.
- **DO NOT** submit Pull Requests without having an approved feature request or enhancement.
- **DO NOT** submit large code formatting changes without discussing them with the team first.

### Feature evidence and design decisions

Use a descriptive title that tells users what changes. Category prefixes such as
`[feat]` are not required; the [title research and decision](docs/decisions/feature-delivery-guidance.md#pr-title-research)
explains the tradeoffs. Preserve prefixes required by existing automation.

For new features, keep a concise, versioned decision record in an existing design
document/RFC or `docs/decisions/<feature-name>.md`. Start before implementation and
update it with research sources, approaches actually tried and their outcomes,
alternatives, assumptions, and the reasons for changing or choosing a direction.
Challenge material design decisions, especially when requirements or rationale
are missing; compare the existing approach and simpler alternatives rather than
assuming the first proposal is right.

Whenever practical, show the feature working with screenshots or a short GIF/video
in the PR description, including terminal and report changes. Provide captions
and reproduction commands; use a text transcript or result artifact when clearer,
and explain unavailable or inapplicable media. Do not expose sensitive data.

For regression coverage, keep the tests while removing the production change in
an isolated comparison: they must execute and fail for the intended behavioral
reason without it and pass with it. Include exact commands, source states, exits,
test identities/counts, and the relevant assertion failure. A build failure or
skipped test is not proof. Behavior-preserving changes instead need focused
green-before/green-after evidence. The [feature-delivery skill](.github/skills/feature-delivery/SKILL.md)
describes safe comparisons, new-API limitations, and the decision-record format.
Link this evidence using the [PR template](.github/PULL_REQUEST_TEMPLATE.md);
screenshots do not replace executable tests.

## Coding Style

The MSTest project follows the same rules as the runtime repository [developer guide](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md).
The repository includes [.editorconfig](http://editorconfig.org) files to help enforce this convention.
Contributors should ensure they follow these guidelines when making submissions.

## TODO Comment Policy

To keep technical debt visible and tracked, the project enforces the following policy for `TODO` comments in code:

- **DO** reference a GitHub issue in every `TODO` comment (e.g., `// TODO(#1234): Refactor this once the new API is available`).
- **DO** convert any `TODO` that does not warrant a tracking issue into a regular comment that explains the rationale.
- **DO NOT** leave `TODO` comments without an associated GitHub issue link. These should be caught during code review.

## Developing

Please see our [Dev Guide](./docs/dev-guide.md) which explains how to develop, build, and test.

## AI Coding Tools

The `csharp-expert` and `csharp-refactoring` skills are imported from
[`dotnet/skills`](https://github.com/dotnet/skills/tree/main/plugins/dotnet) through
`gh copilot-curate`. Their supporting references, source commit, and file hashes are
tracked in `.copilot/curate/manifest.lock.yml`. Run `gh copilot-curate update` to
refresh curated skills; the existing weekly workflow also refreshes them.
Repository-specific instructions and the `behavior-preserving-refactor` skill
continue to apply.

The customized [`grade-tests` skill](./.agents/skills/grade-tests/SKILL.md) is
repository-owned, not curator-managed. The `dotnet-test` manifest explicitly
selects upstream-managed skills and agents so weekly updates preserve the local grading
rubric without reporting managed-file drift. Keep that selection up to date when
adopting additional upstream test skills.

Copilot CLI uses [`.github/lsp.json`](./.github/lsp.json) for C# code intelligence.
This configuration is adapted from the upstream
[Roslyn LSP declaration](https://github.com/dotnet/skills/blob/3d38ac343faf65054f7e8d45ca06925273e867e2/plugins/dotnet/lsp.json):
it uses `dotnet dnx`, omits the plugin-only working directory so the server runs
in the current checkout, and sets the CLI's initialization timeout to two minutes.
It uses plain stdio rather than daemon mode so the CLI owns the server's lifetime
and the server can shut down cleanly with its session.
The curator does not import LSP declarations, so this configuration is maintained
separately from the managed skill files.

First bootstrap the pinned SDK with the repository build scripts. Start Copilot
CLI from the repository root with that SDK's directory on `PATH` (`.dotnet` by
default, or the matching `DOTNET_INSTALL_DIR`). The launcher requires .NET 10 or
later and downloads `roslyn-language-server` on first use, so it also needs access
to the package feed. No global tool installation or user-level LSP settings are
required. The language-server package is pinned to `5.13.0-1.26509.1`; change
that version deliberately and repeat startup, navigation, and shutdown checks
before updating the pin.

In an existing Copilot CLI session, run `/skills reload` and `/lsp reload`, then
use `/lsp test csharp` to check server startup. Other hosts may discover the skills
but do not necessarily consume Copilot CLI's LSP configuration.

## Agentic Workflows

This repository ships a large number of AI-powered GitHub Actions workflows authored with [GitHub Agentic Workflows (`gh aw`)](https://github.com/github/gh-aw). The full catalog, conventions, and quick-start commands live in [`.github/workflows/README.md`](./.github/workflows/README.md).

A few rules worth knowing before you touch anything under `.github/workflows/`:

- **DO** treat the `*.md` file as the source of truth. The companion `*.lock.yml` is generated by `gh aw compile`.
- **DO** run `gh aw compile <workflow-id>` after editing an agentic workflow source (or any `shared/*.md` it imports) and commit the regenerated `*.lock.yml` in the same change.
- **DO** keep strict mode enabled. When in doubt, pass `--strict` to `gh aw compile` to enforce action pinning, network policy, safe-output usage, and other security defaults across all workflows.
- **DO NOT** hand-edit `*.lock.yml`, or any generated dependency manifest that `gh aw compile` may emit under `.github/workflows/` (`package.json`, `requirements.txt`, `go.mod`, …). They are all regenerated by `gh aw compile`.
- **DO NOT** set `strict: false` in workflow frontmatter.
