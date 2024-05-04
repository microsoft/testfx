# The `ITestSessionLifeTimeHandler`

The `ITestSessionLifeTimeHandler` is an *in-process* extension that enables the execution of code *before* and *after* the test session.

To register a custom `ITestSessionLifeTimeHandler`, utilize the following API:

```cs
ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
...
testApplicationBuilder.TestHost.AddTestSessionLifetimeHandle(serviceProvider => new CustomTestSessionLifeTimeHandler());
...
```

>> [!NOTE]
>> The sequence of registration is significant, as the APIs are called in the order they were registered.

The `ITestSessionLifeTimeHandler` interface includes the following methods:

```cs
public interface ITestSessionLifetimeHandler : ITestHostExtension, IExtension
{
    Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken);
    Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken);
}

public readonly struct SessionUid(string value)
{
    public string Value { get; }
}
```

Like all other extensions, the `ITestSessionLifetimeHandler` inherits from [`IExtension`](iextension.md), allowing you to use the `IsEnabledAsync()` API to enable or disable the extension. It also inherits from `ITestHostExtension`, which categorizes the extension within the *test host* extension group.

Let's describe the api:

`OnTestSessionStartingAsync`: This method is invoked prior to the commencement of the test session and receives the `SessionUid` object, which provides an opaque identifier for the current test session.

`OnTestSessionFinishingAsync`: This method is invoked after the completion of the test session, ensuring that the [testing framework](itestframework.md) has finished executing all tests and has reported all relevant data to the platform. Typically, in this method, the extension employs the [`IMessageBus`](imessagebus.md) to transmit custom assets or data to the shared platform bus. This method can also signal to any custom *out-of-process* extension that the test session has concluded.
