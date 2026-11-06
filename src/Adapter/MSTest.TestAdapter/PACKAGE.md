# MSTest.TestAdapter

MSTest is Microsoft supported Test Framework.

This package includes the adapter logic to discover and run tests. For access to the testing framework, install the MSTest.TestFramework package.

## Getting started

After referencing both `MSTest.TestAdapter` and `MSTest.TestFramework`, define tests with `[TestClass]` and `[TestMethod]`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class CalculatorTests
{
    [TestMethod]
    public void AddReturnsExpectedResult()
        => Assert.AreEqual(4, 2 + 2);
}
```

Supported platforms:

- .NET 4.6.2+
- .NET 8.0+
- .NET 8.0 Windows.18362+ (WinUI)
- UWP 10.0.16299
- UWP 10.0.17763 with .NET 9+

## Runsettings on Microsoft.Testing.Platform

When using the MSTest runner on Microsoft.Testing.Platform, `.runsettings` files can
configure MSTest through the `MSTest` section (or its `MSTestV2` alias), test parameters
through `TestRunParameters`, and supported settings through `RunConfiguration`.

VSTest `ISettingsProvider` extensions are not invoked. Other top-level sections, such
as `Playwright`, produce a warning and are not applied by MSTest. Use the extension's
native Microsoft.Testing.Platform configuration when available. VSTest loggers,
data collectors, and unsupported `RunConfiguration` entries retain their own warnings.

## Result file attachments

When running on Microsoft.Testing.Platform, `TestContext.AddResultFile` registers
an existing file as a test attachment. MSTest preserves successfully copied
attachments in visible `MSTest-<random-id>` folders under the configured results
directory so the published paths remain readable after the test host exits.
Original files are not moved or deleted by this copying step.

These copies are final test results: apply your results-directory retention policy
to remove them when they are no longer needed. TRX reporting can create an
additional copy in its own attachment layout; follow the report's references
rather than selecting a file by name from the entire results directory.
Retry tooling can also publish additional copies from the final attempt.
If an attachment cannot be copied, MSTest reports its original path so consumers
can surface their usual missing-file or copy-failure diagnostics.

## Documentation

For installation and configuration guidance, see <https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-getting-started>.

For information about running tests, see <https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-running-tests>.
