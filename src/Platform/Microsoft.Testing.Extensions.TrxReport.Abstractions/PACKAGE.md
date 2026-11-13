# Microsoft.Testing.Extensions.TrxReport.Abstractions

Microsoft.Testing.Extensions.TrxReport.Abstractions provides the interfaces and data objects for extensions that need to interoperate with TRX report functionality in [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform).

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.TrxReport.Abstractions` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.TrxReport.Abstractions
```

## Usage

Reference this package from a testing-platform extension that needs to contribute TRX-specific properties or inspect `ITrxReportCapability`. Test projects that only need to generate a TRX report should use `Microsoft.Testing.Extensions.TrxReport` instead.

## Required and optional properties

A test framework that supports TRX reporting adds properties to its final test-node updates. The only required property is the final state:

- **Required**: one final state, such as `PassedTestNodeStateProperty` or `FailedTestNodeStateProperty`. Updates without a final state are not reported.
- **Recommended**: `TrxFullyQualifiedTypeNameProperty` or `TestMethodIdentifierProperty`, so tests are grouped under their real class. Without either, the class name is `DefaultClassName` and the method name is the test node's display name.
- **Optional**: `TimingProperty`, `TrxTestDefinitionName`, `TrxExceptionProperty`, `TrxMessagesProperty`, `TrxCategoriesProperty`, `TestMetadataProperty`, `TrxWorkItemsProperty`, and `FileArtifactProperty`.
- **Duplicates**: providing more than one instance of a property makes report generation fail, except for `TestMetadataProperty` and `FileArtifactProperty`, which can have multiple instances.

The `ITrxReportCapability` API documentation describes what each property contributes to the report.

## About

This package provides:

- **TRX abstractions**: interfaces and data types for extensions that need to contribute to or consume TRX report data
- **Extensibility**: allows third-party extensions to enrich TRX reports with additional information

> **Note**: This package provides only the abstractions. To generate TRX reports, install [Microsoft.Testing.Extensions.TrxReport](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport).
>
> For most test projects, you should install `Microsoft.Testing.Extensions.TrxReport` instead of referencing this package directly.

## Related packages

- [Microsoft.Testing.Extensions.TrxReport](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport): TRX report generation extension

## Documentation

For this extension, see <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-extensions-test-reports>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
