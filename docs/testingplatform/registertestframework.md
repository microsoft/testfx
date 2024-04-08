# Register the testing framework

In this section we will explain how to register the test framework to the testing platform.
You can register only one testing framework per test application builder using the api `TestApplication.RegisterTestFramework` as shown [here](architecture.md)

The API's signature is as follows:

```cs
ITestApplicationBuilder RegisterTestFramework(
    Func<IServiceProvider, ITestFrameworkCapabilities> capabilitiesFactory,
    Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework> adapterFactory);
```

The `RegisterTestFramework` API expects two factories:

1. `Func<IServiceProvider, ITestFrameworkCapabilities>`: This is a lambda function that accepts an object implementing the [`IServiceProvider`](iserviceprovider.md) interface and returns an object implementing the [`ITestFrameworkCapabilities`](capabilities.md) interface. The [`IServiceProvider`](iserviceprovider.md) provides access to platform services such as configurations, loggers, command line arguments, etc.
The [`ITestFrameworkCapabilities`](capabilities.md) interface is used to announce the capabilities supported by the testing framework to the platform and extensions. It allows the platform and extensions to interact correctly by implementing and supporting specific behaviors. For a better understanding of the [concept of capabilities](capabilities.md), refer to the respective section.

1. `Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework>`: This is a lambda function that takes in an [ITestFrameworkCapabilities](capabilities.md) object, which is the instance returned by the `Func<IServiceProvider, ITestFrameworkCapabilities>`, and an [IServiceProvider](iserviceprovider.md) to provide access to platform services once more. The expected return object is one that implements the [ITestFramework](itestframework.md) interface. The ITestFramework serves as the execution engine that discovers and runs tests, and communicates the results back to the testing platform.

The need for the platform to separate the creation of the [`ITestFrameworkCapabilities`](capabilities.md) and the creation of the [ITestFramework](itestframework.md) is an optimization to avoid to create the test framework if anyway the supported capabilities are not sufficient to execute the current testing session.

Below a sample of a test framework registration that returns empty capabilities.

User code:

```cs
internal class TestingFrameworkCapabilities : ITestFrameworkCapabilities
{
    public IReadOnlyCollection<ITestFrameworkCapability> Capabilities => [];
}

internal class TestingFramework : ITestFramework
{
   public TestingFramework(ITestFrameworkCapabilities capabilities, IServiceProvider serviceProvider)
   {
     ...
   }
   ...
}

public static class TestingFrameworkExtensions
{
    public static void AddTestingFramework(this ITestApplicationBuilder builder)
    {
        builder.RegisterTestFramework(
            _ => new TestingFrameworkCapabilities(),
            (capabilities, serviceProvider) => new TestingFramework(capabilities, serviceProvider));
    }
}

...
```

Entry point with the registration:

```cs
var testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
// Register the testing framework
testApplicationBuilder.AddTestingFramework();
using var testApplication = await testApplicationBuilder.BuildAsync();
return await testApplication.RunAsync();
```

> [!NOTE]
> Return empty [ITestFrameworkCapabilities](capabilities.md) should not hinder the execution of the test session. The fundamental features of discovering and running tests should always be ensured. The impact should be limited to extensions that may opt out if the test framework lacks a certain feature.
