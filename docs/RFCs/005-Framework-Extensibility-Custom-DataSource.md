# RFC 005- Framework Extensibility for Custom Test Data Source

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
```
MyFavMSTestV2Test (1,2,3)
MyFavMSTestV2Test (4,5,6)
```

###  Discovery of `ITestDataSource` attributes
The MSTest v2 framework, on discovering a `TestMethod`, probes additional attributes. On finding attributes inheriting from `ITestDataSource`, the framework invokes `GetData()` to fetch test data and iteratively invokes the test method with the test data as arguments.

### Benefits of using `ITestDataSource`
1. Users can extend `ITestDataSource` to support custom data sources.
2. Multiple tests can reuse the test data defined in the same data source.
3. A test case can use multiple test data sources.

### Remarks
When implementing a custom `ITestDataSource` (attribute), the `GetData()` method should not return an empty sequence, otherwise the test(s) using this data source attribute will always fail.
