# RFC 005 - Framework Extensibility for Custom Test Data Source

- [x] Approved in principle
- [x] Under discussion
- [x] Implementation
- [x] Shipped

## Summary

This details the MSTest V2 framework extensibility for specifying custom data source for data driven tests.

## Motivation

Often times, custom data sources are required for data driven tests. User should be able to leverage test framework extensibility to provide custom data sources for test execution.

## Detailed Design

### Requirements

1. A custom data source can be used by multiple test cases.
2. A test case can have multiple data sources.

### Proposed solution

Here is a solution for using custom data source in data driven tests.

The test framework should define an interface class `ITestDataSource` which can be extended to get data from custom data source.

```csharp
public interface ITestDataSource
{
    /// <summary>
    /// Gets the test data from custom data source.
    /// </summary>
    IEnumerable<object[]> GetData(MethodInfo methodInfo);

    /// <summary>
    /// Display name to be displayed for test corresponding to data row.
    /// </summary>
    string GetDisplayName(MethodInfo methodInfo, object[] data);
}
```

Here is how the test methods are decorated with concrete implementation of `ITestDataSource`:

```csharp
public class CustomTestDataSourceAttribute : Attribute, ITestDataSource
{
    public IEnumerable<object[]> GetData(MethodInfo methodInfo)
    {
        return new[]
        {
            new object[] {1, 2, 3},
            new object[] {4, 5, 6}
        };
    }

    public string GetDisplayName(MethodInfo methodInfo, object[] data)
    {
        if (data != null)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0} ({1})", methodInfo.Name, string.Join(",", data));
        }

        return null;
    } 
}
```

```csharp
[TestMethod]
[CustomTestDataSource]
public void TestMethod1(int a, int b, int c)
{
    Assert.AreEqual(1, a % 3);
    Assert.AreEqual(2, b % 3);
    Assert.AreEqual(0, c % 3);
}
```

In a similar way, multiple test methods can be decorated with same data source.
A test method can also be decorated with multiple data sources.

Users can customize the display name of tests in test results by overriding `GetDisplayName()` method.

```csharp
public override string GetDisplayName(MethodInfo methodInfo, object[] data)
{
    return string.Format(CultureInfo.CurrentCulture, "MyFavMSTestV2Test ({0})", string.Join(",", data));
}
```

The display name of tests in the above example would appear as :

```shell
MyFavMSTestV2Test (1,2,3)
MyFavMSTestV2Test (4,5,6)
```

### Discovery of `ITestDataSource` attributes

The MSTest v2 framework, on discovering a `TestMethod`, probes additional attributes. On finding attributes inheriting from `ITestDataSource`, the framework invokes `GetData()` to fetch test data and iteratively invokes the test method with the test data as arguments.

### Benefits of using `ITestDataSource`

1. Users can extend `ITestDataSource` to support custom data sources.
2. Multiple tests can reuse the test data defined in the same data source.
3. A test case can use multiple test data sources.

### Remarks

When implementing a custom `ITestDataSource` (attribute), the `GetData()` method should not return an empty sequence, otherwise the test(s) using this data source attribute will always fail.

## Capabilities Added Since v2

Since this RFC was first written, the `ITestDataSource` extension point has grown additional capabilities. These are available to any custom data source (not just MSTest's built-in `DynamicData`/`DataRow`).

### Ignoring an entire data source — `ITestDataSourceIgnoreCapability`

A data source can implement `ITestDataSourceIgnoreCapability` to declare that all of its rows should be ignored (skipped). Setting `IgnoreMessage` to a non-null value ignores every test case produced by the source and surfaces the message as the skip reason.

```csharp
public class CustomTestDataSourceAttribute : Attribute, ITestDataSource, ITestDataSourceIgnoreCapability
{
    public string? IgnoreMessage { get; set; }

    public IEnumerable<object[]> GetData(MethodInfo methodInfo) =>
        new[]
        {
            new object[] { 1, 2, 3 },
            new object[] { 4, 5, 6 },
        };

    public string? GetDisplayName(MethodInfo methodInfo, object?[]? data)
        => data != null ? $"{methodInfo.Name} ({string.Join(",", data)})" : null;
}
```

### Per-row metadata — `TestDataRow<T>`

To attach metadata to an individual row (rather than the whole source), return a single-element `object[]` whose only item is a `TestDataRow<T>` instance. MSTest unwraps `TestDataRow<T>.Value` to obtain the actual test method arguments and applies the row's metadata:

- `DisplayName` — overrides the display name for that row.
- `IgnoreMessage` — when non-null, that single row is reported as skipped with the provided message.
- `TestCategories` — assigns test categories to the generated test case.

`T` can be a tuple when the test method takes more than one parameter.

```csharp
public class CustomTestDataSourceAttribute : Attribute, ITestDataSource
{
    public IEnumerable<object[]> GetData(MethodInfo methodInfo) =>
        new[]
        {
            new object[] { new TestDataRow<(int, int, int)>((1, 2, 3)) { DisplayName = "first row" } },
            new object[] { new TestDataRow<(int, int, int)>((4, 5, 6)) { IgnoreMessage = "not ready yet" } },
            new object[] { new TestDataRow<(int, int, int)>((7, 8, 9)) { TestCategories = new[] { "custom-category" } } },
        };

    public string? GetDisplayName(MethodInfo methodInfo, object?[]? data) => null;
}
```

### Controlling fold/unfold — `TestDataSourceUnfoldingStrategy`

By default, MSTest *unfolds* a parameterized test so each data row becomes its own test case. The `[TestMethod]` attribute exposes an `UnfoldingStrategy` property to override this for a method that uses any data source (including a custom one):

- `TestDataSourceUnfoldingStrategy.Unfold` — each row is a separate test case (the usual behavior).
- `TestDataSourceUnfoldingStrategy.Fold` — all rows are treated as a single test case.
- `TestDataSourceUnfoldingStrategy.Auto` — defer to the assembly-level `TestDataSourceOptionsAttribute`, defaulting to unfold.

```csharp
[TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
[CustomTestDataSource]
public void FoldedTest(int a, int b, int c)
{
}
```

The default strategy can be set for the whole assembly with `[assembly: TestDataSourceOptions(TestDataSourceUnfoldingStrategy.Fold)]`.

## Unresolved questions

None.
