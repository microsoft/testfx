# MSTest.TestFramework

MSTest is Microsoft supported Test Framework.

This package includes the libraries for writing tests with MSTest. To ensure discovery and execution of your tests, install the MSTest.TestAdapter package.

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
- .NET 8.0+ (WinUI)
- .NET 6.0 Windows.18362+
- UWP 10.0.16299
- UWP 10.0.17763 with .NET 9

## Documentation

For installation and configuration guidance, see <https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-getting-started>.

For test authoring guidance, see <https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-writing-tests>.
