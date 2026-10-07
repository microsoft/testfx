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

### Naming data-driven test cases

Use `ArgumentsDisplayName` to label a data row while retaining the test method's
display name:

```csharp
[TestMethod(DisplayName = "Parse")]
[DataRow("", ArgumentsDisplayName = "empty input")]
public void ParseReturnsNull(string input)
{
    // Displayed as: Parse (empty input)
}
```

For dynamic data or a custom `ITestDataSource`, use the same property on
`TestDataRow<T>`, for example
`new TestDataRow<string>("") { ArgumentsDisplayName = "empty input" }`.
Use matching `MSTest.TestFramework` and `MSTest.TestAdapter` versions that support
this property; older adapters do not apply `TestDataRow<T>` argument labels.

`DisplayName` still replaces the entire test case name and takes precedence over
`ArgumentsDisplayName`. A null, empty, or whitespace-only argument label leaves
the existing naming behavior unchanged. Other labels are displayed verbatim, not
interpreted as templates. On `TestDataRow<T>`, an argument label takes precedence
over a data source's custom display-name callback.

For installation and configuration guidance, see <https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-getting-started>.

For test authoring guidance, see <https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-writing-tests>.
