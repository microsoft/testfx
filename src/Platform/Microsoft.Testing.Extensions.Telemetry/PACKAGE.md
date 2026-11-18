# Microsoft.Testing.Extensions.Telemetry

Microsoft.Testing.Extensions.Telemetry is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that collects usage telemetry to help the Microsoft.Testing.Platform team improve the product.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.Telemetry` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.Telemetry
```

## About

This package extends Microsoft.Testing.Platform with:

- **Usage telemetry**: collects usage data to help understand product usage and prioritize improvements
- **Opt-out support**: telemetry can be disabled via the `TESTINGPLATFORM_TELEMETRY_OPTOUT` or `DOTNET_CLI_TELEMETRY_OPTOUT` environment variables
- **Disclosure**: telemetry information is shown on first run, with opt-out guidance

This package is an optional, opt-in extension. To enable telemetry when using Microsoft.Testing.Platform (including when running tests with [MSTest](https://www.nuget.org/packages/MSTest)), you must explicitly reference the `Microsoft.Testing.Extensions.Telemetry` package from your test project or from your own test framework or tooling package.

## AI-agent context

When telemetry is enabled, every usage event includes:

- `is llm`: `true` when the platform's existing environment detector recognizes
  an AI agent, otherwise `false`. Detection is heuristic; `false` does not prove
  a human invocation. Only the boolean is collected, not the marker values.
- `CopilotAgentSessionId`: the nonempty `COPILOT_AGENT_SESSION_ID`, normalized
  to match the .NET SDK's `CopilotAgentSessionId` and Copilot telemetry.
  Canonical UUIDs (versions 1-8 with RFC variant, or the nil UUID) are preserved
  exactly. Other values become lowercase SHA-256 of their original UTF-8 bytes,
  without trimming or case normalization. Missing or empty values produce
  `not_present`; a missing telemetry property means collection support is unknown.

These are independent signals, captured for each event before asynchronous
dispatch. Existing platform session IDs and telemetry opt-outs are unchanged.
This does not add fields to application-configured OpenTelemetry traces.

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-telemetry>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
