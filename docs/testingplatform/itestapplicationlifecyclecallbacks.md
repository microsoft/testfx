# The `ITestApplicationLifecycleCallbacks`

The `ITestApplicationLifecycleCallbacks` is an *in-process* extension that enables the execution of code before everything, it's like to have access to the first line of the ipotetical *main* of the *test host*.

To register a custom `ITestApplicationLifecycleCallbacks`, utilize the following api:

```cs
ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
...
testApplicationBuilder.TestHost.AddTestApplicationLifecycleCallbacks(serviceProvider
    => new CustomTestApplicationLifecycleCallbacks());
...
```

The factory utilizes the [IServiceProvider](iserviceprovider.md) to gain access to the suite of services offered by the testing platform.

>> [!IMPORTANT]
>> The sequence of registration is significant, as the APIs are called in the order they were registered.

The `ITestApplicationLifecycleCallbacks` interface includes the following methods:

```cs
public interface ITestApplicationLifecycleCallbacks : ITestHostExtension
{
    Task BeforeRunAsync(CancellationToken cancellationToken);
    Task AfterRunAsync(int exitCode, CancellationToken cancellation);
}

public interface ITestHostExtension : IExtension
{
}
```

The `ITestApplicationLifecycleCallbacks` is a type of `ITestHostExtension`, which serves as a base for all *test host* extensions. Like all other extension points, it also inherits from [IExtension](iextension.md). Therefore, like any other extension, you can choose to enable or disable it using the `IExtension.IsEnabledAsync` API.

`BeforeRunAsync`: This method serves as the initial point of contact for the *test host* and is the first opportunity for an *in-process* extension to execute a feature. It's typically used to establish a connection with any corresponding *out-of-process* extensions if a feature is designed to operate across both environments.

*For example, the built-in hang dump feature is composed of both *in-process* and *out-of-process* extensions, and this method is used to exchange information with the *out-of-process* component of the extension.*

`AfterRunAsync`: This method is the final call before exiting the [`int ITestApplication.RunAsync()`](architecture.md) and it provides the [`exit code`](exitcodes.md). It should be used solely for cleanup tasks and to notify any corresponding *out-of-process* extension that the *test host* is about to terminate.

Finally, both APIs take a `CancellationToken` which the extension is expected to honor.
