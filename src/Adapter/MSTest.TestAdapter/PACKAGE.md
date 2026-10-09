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

## F# modules

F# modules can contain test functions without a `[<TestClass>]` attribute:

```fsharp
namespace MyTests

open Microsoft.VisualStudio.TestTools.UnitTesting

[<TestCategory("Integration"); DoNotParallelize>]
module CalculatorTests =
    [<TestMethod>]
    let ``addition works`` () =
        Assert.AreEqual(4, 2 + 2)
```

Attributes that support classes, such as `TestCategory`, `Ignore`, `TestProperty`,
`Retry`, and `DoNotParallelize`, also support F# module declarations. Nested modules
are separate test containers; outer-module attributes do not apply to them.
An explicit or custom `[<TestClass>]` attribute is optional and is honored when present.
C#, Visual Basic, and F# test classes still require `[TestClass]`.
Upgrading the adapter can discover annotated F# module functions that older
adapter versions ignored.

Module functions support the same test data and return types as class-based tests:
`void`, `Task`, and non-generic `ValueTask` at the CLR level. F# `Async<unit>`
workflows must be converted to a supported task return type.

Module lifecycle functions use `[<ClassInitialize>]` and `[<ClassCleanup>]` for
once-per-module fixtures, and `[<TestInitialize>]` and `[<TestCleanup>]` for
per-test fixtures. Their parameters and return types follow the existing fixture
rules, but per-test module fixtures are static. Assembly and global fixtures can
also be declared in modules without `[<TestClass>]`.

There is no per-test module instance, constructor injection, or instance disposal.
Use `TestContext.Current` to access the current test context. Mutable module state
is shared by tests and retries; use the existing parallelization controls when needed.
Module support uses runtime reflection, not the C# source generator.

## Runsettings on Microsoft.Testing.Platform

When using the MSTest runner on Microsoft.Testing.Platform, `.runsettings` files can
configure MSTest through the `MSTest` section (or its `MSTestV2` alias), test parameters
through `TestRunParameters`, and supported settings through `RunConfiguration`.

VSTest `ISettingsProvider` extensions are not invoked. Other top-level sections, such
as `Playwright`, produce a warning and are not applied by MSTest. Use the extension's
native Microsoft.Testing.Platform configuration when available. VSTest loggers,
data collectors, and unsupported `RunConfiguration` entries retain their own warnings.

## Documentation

For installation and configuration guidance, see <https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-getting-started>.

For information about running tests, see <https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-running-tests>.
