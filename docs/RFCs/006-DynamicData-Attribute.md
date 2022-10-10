# RFC 006- DynamicData Attribute for Data Driven Tests

## Summary
This details the MSTest V2 framework attribute "DynamicData" for data driven tests where test data can be declared as properties or in methods and can be shared between more than one test cases.

## Motivation
Often times, data driven tests use shared test data that can be declared as properties or in methods. User can use `DataRow` for declaring inline data, but it can't be shared. Test framework should provide feature so that test data can be declared as property or in method and can be easily used by multiple tests.

## Detailed Design

### Requirements
1. Test data can be declared as properties or in methods and can be reused by multiple test cases.

### Proposed solution
Here is a solution that meets the above requirements:

A static property or a static method having test data should be declared as below:
```csharp
[TestClass]
public class UnitTests
{
    static IEnumerable<object[]> ReusableTestDataProperty
    {
        get
        {
            return new[]
            { 
                new object[] {1, 2, 3},
                new object[] {4, 5, 6}
            };
        }
    }

    static IEnumerable<object[]> ReusableTestDataMethod()
    {
        return new[]
        {
            new object[] {1, 2, 3},
            new object[] {4, 5, 6}
        }; 
    }

    // Property ReusableTestDataProperty can be used as data source for test data with data driven test case.
    [TestMethod]
    [DynamicData("ReusableTestDataProperty")]
    public void DynamicDataTestMethod1(int a, int b, int c)
    {
        Assert.AreEqual(1, a % 3);
        Assert.AreEqual(2, b % 3);
        Assert.AreEqual(0, c % 3);
    }

    // Method ReusableTestDataMethod can be used as data source for test data with data driven test case.
    [TestMethod]
    [DynamicData("ReusableTestDataMethod", DynamicDataSourceType.Method)]
    public void DynamicDataTestMethod2(int a, int b, int c)
    {
        Assert.AreEqual(1, a % 3);
        Assert.AreEqual(2, b % 3);
        Assert.AreEqual(0, c % 3);
    }
}
```

In case, the property or method exists in a class other than the test class, an additional `Type` argument should be passed to `DynamicData` constructor.

```csharp
[DynamicData("ReusableTestDataProperty", typeOf(UnitTests))]

[DynamicData("ReusableTestDataMethod", typeOf(UnitTests), DynamicDataSourceType.Method)]
```
Please note that Enum `DynamicDataSourceType` is used to specify whether test data source is a property or method.
Data source is considered as property by default.

Optionally, to provide a custom name for each data driven test case, `DynamicDataDisplayName` can be used to reference a public static method declared as below:

```csharp
public static string GetCustomDynamicDataDisplayName(MethodInfo methodInfo, object[] data)
{
    return string.Format("DynamicDataTestMethod {0} with {1} parameters", methodInfo.Name, data.Length);
}

// Method GetCustomDynamicDataDisplayName can be used to provide a custom test name for test data with data driven test case.
[DynamicData("ReusableTestDataProperty", DynamicDataDisplayName = "GetCustomDynamicDataDisplayName")]
```

`DynamicDataDisplayNameDeclaringType` should be used in cases where the dynamic data display name method exists in a class other than the test class 

```csharp
[DynamicData("ReusableTestDataMethod", DynamicDataDisplayName = "GetCustomDynamicDataDisplayName", DynamicDataDisplayNameDeclaringType = typeOf(UnitTests))]
```

### Benefits of using DynamicData attribute
1. More than one tests can use the same test data, if required.
2. Changes in the shared test data can be scoped to single place.
