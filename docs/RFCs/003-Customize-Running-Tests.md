# RFC 003 - Framework Extensibility for Custom Test Execution

- [x] Approved in principle
- [x] Under discussion
- [x] Implementation
- [x] Shipped

## Summary

This document deals with how test runs can be customized using the MSTest V2 Framework extensibility.

## Motivation

The default workflow for running tests in MSTest V2 involves creating an instance of a TestClass and invoking a TestMethod in it. There are multiple instances where this workflow is required to be tweaked so specific tests are runnable. Some tests require to be run on a UI Thread, some others need to be parameterized. This requires that the Test Framework provide extensibility points so that test authors have the ability to run their tests differently.

## Detailed Design

The execution flow can broadly be extended at two levels:

1. Test Method level
2. Test Class level

The sections below details how one can customize execution at these two points.

### Test Method level

Customizing test method level execution is simple - Extend the `TestMethodAttribute`. The `TestMethodAttribute` has the following signature:

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TestMethodAttribute : Attribute
{
    /// <summary>
    /// Executes a test method.
    /// </summary>
    /// <param name="testMethod">The test method to execute.</param>
    /// <returns>An array of TestResult objects that represent the outcome(s) of the test.</returns>
    /// <remarks>Extensions can override this method to customize running a test method.</remarks>
    public virtual TestResult[] Execute(ITestMethod testMethod) { }
}
```

Extension writers would only need to override the `Execute` method to gain control on how a test is run. The `ITestMethod` instance allows one to get more context of the method under execution. The test method can be executed using `ITestMethod.Invoke()` or by just calling `base.Execute()` on the TestMethodAttribute to go back through the default flow of the Framework.

```csharp
/// <summary>
/// TestMethod for execution.
/// </summary>
public interface ITestMethod
{
    /// <summary>
    /// Gets the name of test method.
    /// </summary>
    string TestMethodName { get; }

    /// <summary>
    /// Gets the name of test class.
    /// </summary>
    string TestClassName { get; }

    /// <summary>
    /// Gets the return type of test method.
    /// </summary>
    Type ReturnType { get; }

    /// <summary>
    /// Gets the parameters of test method.
    /// </summary>
    ParameterInfo[] ParameterTypes { get; }

    /// <summary>
    /// Gets the methodInfo for test method. 
    /// </summary>
    /// <remarks>
    /// This is just to retrieve additional information about the method. 
    /// Do not directly invoke the method using MethodInfo. Use ITestMethod.Invoke instead.
    /// </remarks>
    MethodInfo MethodInfo { get; }

    /// <summary>
    /// Invokes the test method.
    /// </summary>
    /// <param name="arguments">
    /// Arguments to pass to test method. (E.g. For data driven)
    /// </param>
    /// <returns>
    /// Result of test method invocation.
    /// </returns>
    /// <remarks>
    /// This call handles asynchronous test methods as well.
    /// </remarks>
    TestResult Invoke(object[] arguments);

    /// <summary>
    /// Get all attributes of the test method.
    /// </summary>
    /// <param name="inherit">
    /// Whether attribute defined in parent class is valid.
    /// </param>
    /// <returns>
    /// All attributes.
    /// </returns>
    Attribute[] GetAllAttributes(bool inherit);

    /// <summary>
    /// Get attribute of specific type.
    /// </summary>
    /// <typeparam name="AttributeType"> System.Attribute type. </typeparam>
    /// <param name="inherit">
    /// Whether attribute defined in parent class is valid.
    /// </param>
    /// <returns>
    /// The attributes of the specified type.
    /// </returns>
    AttributeType[] GetAttributes<AttributeType>(bool inherit)
        where AttributeType : Attribute;
}
```

From a test authors perspective, the test method would now be adorned with the type that extends `TestMethodAttribute` to light up the extended functionality.

Let us take a very simple example to apply this extensibility on - the task is to validate the stability of a test scenario, that is ensure that the test for that scenario passes always when run 'n' number of times.
We start by declaring an `IterativeTestMethodAttribute` that extends `TestMethodAttribute`. We then override `TestMethodAttribute.Execute()` to run the test 'n' number of times.

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class IterativeTestMethodAttribute : TestMethodAttribute
{
    private int stabilityThreshold;

    public IterativeTestMethodAttribute(int stabilityThreshold)
    {
        this.stabilityThreshold = stabilityThreshold;
    }

    public override TestResult[] Execute(ITestMethod testMethod) 
    {
        var results = new List<TestResult>();
        for(int count = 0; count < this.stabilityThreshold; count++)
        {
            var currentResults = base.Execute(testMethod);
            results.AddRange(currentResults);
        }

        return results.ToArray();
    }
}
```

From a test authors perspective, the test method would now be adorned with a `IterativeTestMethodAttribute` instead.

```csharp
[TestClass]
public class LongRunningScenarios()
{
    [IterativeTestMethod(5)]
    public void LongRunningTest()
    {

    }
}
```

### Test Class level

Scaling up the test method level extensibility gets one to a position of customizing execution of all test methods under a unit, which in this case is a TestClass. One can do so by extending the `TestClassAttribute`.

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class TestClassAttribute : Attribute
{
    /// <summary>
    /// Gets a test method attribute that enables running this test.
    /// </summary>
    /// <param name="testMethodAttribute">The test method attribute instance defined on this method.</param>
    /// <returns>The <see cref="TestMethodAttribute"/> to be used to run this test.</returns>
    /// <remarks>Extensions can override this method to customize how all methods in a class are run.</remarks>
    public virtual TestMethodAttribute GetTestMethodAttribute(TestMethodAttribute testMethodAttribute) { }
}
```

Overriding `GetTestMethodAttribute()` allows extensions to provide a custom `TestMethodAttribute` that specifies how a specific method is run as detailed in the Test Method level extensibility section above.

From a test authors perspective, the test class would now be adorned with the type that extends `TestClassAttribute` to light up the extended functionality.

To explain this better, lets go back to the example of running a test method 'n' number of times to determine the stability of a scenario. The task now is scaled up to ensure all test methods in a unit are stable.
We start by declaring an `IterativeTestClassAttribute` that extends `TestClassAttribute`. We then extend `GetTestMethodAttribute()` to return an `IterativeTestMethodAttribute`.

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class IterativeTestClassAttribute : TestClassAttribute
{
    private int stabilityThreshold;

    public IterativeTestClassAttribute(int stabilityThreshold)
    {
        this.stabilityThreshold = stabilityThreshold;
    }

    public override TestMethodAttribute GetTestMethodAttribute(TestMethodAttribute testMethodAttribute)
    {
        if (testMethodAttribute is IterativeTestMethodAttribute)
            return testMethodAttribute;

        return new IterativeTestMethodAttribute(this.stabilityThreshold);
    }
}
```

The Test Method level extensibility workflow then kicks in when running all test methods in the class ensuring that each method is run 'n' number of times. A point to note from the code sample is that one can have a method level value for 'n' that overrides the class level value. This is possible because the `GetTestMethodAttribute` conditionally returns a new `IterativeTestMethodAttribute` only if the attribute is not already of that type. So if a method is already adorned with an `IterativeTestMethodAttribute` then the stabilityThreshold on the method take precedence over the class. Thus, one can choose how each individual method in the unit is executed.

From a test authors perspective, the test class would now be adorned with a `IterativeTestClassAttribute` instead.

```csharp
[IterativeTestClass(10)]
public class LongRunningScenarios()
{
    [TestMethod]
    public void TestConnection()
    {

    }

    [IterativeTestMethod(5)]
    public void LongRunningTest()
    {

    }
}
```

## Unresolved questions

1. There can only be one extension that is in control of the execution flow in this model. Should this change to allow the execution flow through multiple extensions? How would that look like?
2. Would a similar model work for extensions that want to hook into Initialize/Cleanup functionality?
