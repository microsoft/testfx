# The `ITestHostProcessLifetimeHandler`

The `ITestHostProcessLifetimeHandler` is an *out-of-process* extension that allows you to observe the test host process from an external standpoint. This ensures that your extension remains unaffected by potential crashes or hangs that could be induced by the code under test. Utilizing this extension point will prompt the testing platform to initiate a new host, as detailed in the [architecture](architecture.md) section.

To register a custom `ITestHostProcessLifetimeHandler`, utilize the following API:

```cs
ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
...
testApplicationBuilder.TestHostControllers.AddProcessLifetimeHandler(serviceProvider =>
    new CustomMonitorTestHost());
...
```

The factory utilizes the [IServiceProvider](iserviceprovider.md) to gain access to the suite of services offered by the testing platform.

> [!IMPORTANT]
> The sequence of registration is significant, as the APIs are called in the order they were registered.

The `ITestHostProcessLifetimeHandler` interface includes the following methods:

```cs
public interface ITestHostProcessLifetimeHandler : ITestHostControllersExtension
{
    Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken);
    Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation);
    Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation);
}

public interface ITestHostProcessInformation
{
    int PID { get; }
    int ExitCode { get; }
    bool HasExitedGracefully { get; }
}
```

The `ITestHostProcessLifetimeHandler` is a type of `ITestHostControllersExtension`, which serves as a base for all *test host controller* extensions. Like all other extension points, it also inherits from [IExtension](iextension.md). Therefore, like any other extension, you can choose to enable or disable it using the `IExtension.IsEnabledAsync` API.

Let's describe the api:

`BeforeTestHostProcessStartAsync`: This method is invoked prior to the testing platform initiating the test hosts.

`OnTestHostProcessStartedAsync`: This method is invoked immediately after the test host starts. This method offers an object that implements the `ITestHostProcessInformation` interface, which provides key details about the test host process result.
> [!IMPORTANT]
> The invocation of this method does not halt the test host's execution. If you need to pause it, you should register an [*in-process*](extensionintro.md) extension such as [`ITestApplicationLifecycleCallbacks`](itestapplicationlifecyclecallbacks.md) and synchronize it with the *out-of-process* extension.

`OnTestHostProcessExitedAsync`: This method is invoked when the test suite execution is complete. This method supplies an object that adheres to the `ITestHostProcessInformation` interface, which conveys crucial details about the outcome of the test host process.

The `ITestHostProcessInformation` interface provides the following details:

* `PID`: The process ID of the test host.
* `ExitCode`: The exit code of the process. This value is only available within the `OnTestHostProcessExitedAsync` method. Attempting to access it within the `OnTestHostProcessStartedAsync` method will result in an exception.
* `HasExitedGracefully`: A boolean value indicating whether the test host has crashed. If true, it signifies that the test host did not exit gracefully.
