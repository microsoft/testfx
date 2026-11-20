# MSTest

MSTest is Microsoft supported Test Framework.

This package is a meta package that simplifies referencing all recommended MSTest packages.

## Getting started

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
- .NET 10.0+
- UWP 10.0.16299

UWP and WinUI test applications use the same packages. `MSTest.TestFramework`
adds the UI-thread APIs (`[UITestMethod]`) and `MSTest.TestAdapter` selects its
UI-specific build from the project's `UseWinUI`, `UseUwpTools`, or `UseUwp`
settings, or from a classic UAP target.

## Documentation

For installation and configuration guidance, see <https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-getting-started>.

For test authoring guidance, see <https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-writing-tests>.
