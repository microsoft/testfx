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
- .NET 10.0+
- .NET 10.0 Windows.18362+ (WinUI)
- UWP 10.0.16299
- UWP 10.0.17763 with .NET 10+

## Documentation

### Common API assembly ownership

Starting with MSTest 5, `MessageLevel`, `TestRun`, `ITestRunInfo` and `PlannedTest`
are defined in `MSTest.TestFramework.dll`. The .NET Framework-only private
reflection and test-configuration helpers are also defined there. Consumers
compiled against their former `MSTest.TestFramework.Extensions.dll` definitions
must recompile; assembly-qualified configuration names must use the core assembly.
`TestContext`, `DeploymentItemAttribute` and the UI APIs remain in Extensions.
The package layout and supported targets are unchanged.

### Predicate-based collection assertions

Starting with MSTest 5, use `Assert.ContainsOnly` to assert that every collection
element satisfies a predicate. Use `Assert.DoesNotContain` to assert that no
element satisfies a predicate:

```csharp
string[] fragments = ["AB", "A", "BC"];

Assert.ContainsOnly(fragment => fragment.Length < 3, fragments);
Assert.DoesNotContain(fragment => fragment.Length > 2, fragments);
```

Both methods stop at the first violation and report its zero-based index, value,
and predicate expression. They evaluate the predicate once per visited element
and do not enumerate the collection again to construct the failure message.
Predicate and iterator exceptions propagate unchanged. Built-in counterexample
formatting does not enumerate nested collection contents; an explicit value
formatter can provide a representation when needed.

An empty collection passes both assertions. Add `Assert.IsNotEmpty` separately
when at least one element is required. `ContainsAll` remains a different
assertion: it verifies that every expected item is present, with multiplicity,
and permits additional items.

### Expected duration warnings

Use `[ExpectedDuration(2000)]` on a test method to warn when a completed result
takes more than 2,000 milliseconds. The test is not interrupted and its original
outcome, result counts, and exit code are unchanged. The warning is also retained
in that result's test-context output for reports. Use matching framework and
adapter versions that support this attribute.

The budget applies independently to each data row and the final retry attempt.
Superseded attempts and retry delays are not accumulated. Only passed and failed
results are evaluated; skipped, inconclusive, timed-out, aborted, and
infrastructure-error results do not receive an additional budget warning.
Equality is within budget.

Warnings retain four fractional millisecond digits so even a one-tick overrun
is distinguishable from equality. If live warning delivery fails, the warning
and failure details remain in the matching result's test-context output;
completed outcomes and cleanup are preserved.

The comparison uses the reported test duration. Ordinary execution includes
per-test initialization, construction, the test body, cleanup, and disposal,
but excludes assembly/class initialization and cleanup. Custom test-method
executors control their reported result durations.

Keep `[Timeout]` for execution-failure/cancellation limits. Expected durations are
generous diagnostic budgets that should allow for machine load, debugging, and
cold starts; they are not controlled performance benchmarks.

### Deployment items

Starting with MSTest 5, `DeploymentItemAttribute` is not included in the .NET
Standard 2.0 assembly. It remains available for supported .NET Framework and .NET
targets, but is not supported for UWP or WinUI. Test projects that use
`[DeploymentItem]` must target a supported .NET Framework or .NET version rather
than .NET Standard.

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
