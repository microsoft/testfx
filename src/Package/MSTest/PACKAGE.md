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

Classic UWP selects `MSTest.Uwp` through its UAP dependency group. Modern UWP
and WinUI cannot be distinguished by a Windows TFM alone: reference
`MSTest.Uwp` or `MSTest.WinUI` directly instead of this metapackage, or use
`MSTest.Sdk` with the appropriate application model. Do not combine them with
the ordinary adapter dependency supplied by this metapackage.

## Documentation

For installation and configuration guidance, see <https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-getting-started>.

For test authoring guidance, see <https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-writing-tests>.
