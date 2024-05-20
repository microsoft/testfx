# The `IDataConsumer`

The `IDataConsumer` is an *in-process* extension capable of subscribing to and receiving `IData` information that is pushed to the [IMessageBus](imessagebus.md) by the [testing framework](itestframework.md) and its extensions.

*This extension point is crucial as it enables developers to gather and process all the information generated during a test session.*

To register a custom `IDataConsumer`, utilize the following api:

```cs
ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
...
testApplicationBuilder.TestHost.AddDataConsumer(serviceProvider
    => new CustomDataConsumer());
...
```

The factory utilizes the [IServiceProvider](iserviceprovider.md) to gain access to the suite of services offered by the testing platform.

> [!IMPORTANT]
> The sequence of registration is significant, as the APIs are called in the order they were registered.

The `IDataConsumer` interface includes the following methods:

```cs
public interface IDataConsumer : ITestHostExtension
{
    Type[] DataTypesConsumed { get; }
    Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken);
}

public interface IData
{
    string DisplayName { get; }
    string? Description { get; }
}
```

The `IDataConsumer` is a type of `ITestHostExtension`, which serves as a base for all *test host* extensions. Like all other extension points, it also inherits from [IExtension](iextension.md). Therefore, like any other extension, you can choose to enable or disable it using the `IExtension.IsEnabledAsync` API.

`DataTypesConsumed`: This property returns a list of `Type` that this extension plans to consume. It corresponds to `IDataProducer.DataTypesProduced`. Notably, an `IDataConsumer` can subscribe to multiple types originating from different `IDataProducer` instances without any issues.

`ConsumeAsync`: This method is triggered whenever data of a type to which the current consumer is subscribed is pushed onto the [`IMessageBus`](imessagebus.md). It receives the `IDataProducer` to provide details about the data payload's producer, as well as the `IData` payload itself. As you can see, `IData` is a generic placeholder interface that contains general informative data. The ability to push different types of `IData` implies that the consumer needs to *switch* on the type itself to cast it to the correct type and access the specific information.

A sample implementation of a consumer that wants to elaborate the [`TestNodeUpdateMessage`](testnodeupdatemessage.md) produced by a [testing framework](itestframework.md) could be:

```cs
internal class CustomDataConsumer : IDataConsumer, IOutputDeviceDataProducer
{
    public Type[] DataTypesConsumed => new[] { typeof(TestNodeUpdateMessage) };
    ...
    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        var testNodeUpdateMessage = (TestNodeUpdateMessage)value;

        TestNodeStateProperty nodeState = testNodeUpdateMessage.TestNode.Properties.Single<TestNodeStateProperty>();

        switch (nodeState)
        {
            case InProgressTestNodeStateProperty _:
                {
                    ...
                    break;
                }
            case PassedTestNodeStateProperty _:
                {
                    ...
                    break;
                }
            case FailedTestNodeStateProperty failedTestNodeStateProperty:
                {
                    ...
                    break;
                }
            case SkippedTestNodeStateProperty _:
                {
                    ...
                    break;
                }
            ...
        }

        return Task.CompletedTask;
    }
...
}
```

Finally, the api takes a `CancellationToken` which the extension is expected to honor.

> [!IMPORTANT]
> It's crucial to process the payload directly within the `ConsumeAsync` method. The [IMessageBus](imessagebus.md) can manage both synchronous and asynchronous processing, coordinating the execution with the [testing framework](itestframework.md). Although the consumption process is entirely asynchronous and doesn't block the [IMessageBus.Push](imessagebus.md) at the time of writing, this is an implementation detail that may change in the future due to feature requirements. However, we aim to maintain this interface's simplicity and ensure that this method is always called once, eliminating the need for complex synchronization. Additionally, we automatically manage the scalability of the consumers.

> [!WARNING]
> When using `IDataConsumer` in conjunction with [ITestHostProcessLifetimeHandler](itestsessionlifetimehandler.md) within a [composite extension point](compositeextensionfactory.md), **it's crucial to disregard any data received post the execution of [ITestSessionLifetimeHandler.OnTestSessionFinishingAsync](itestsessionlifetimehandler.md)**. The `OnTestSessionFinishingAsync` is the final opportunity to process accumulated data and transmit new information to the [IMessageBus](imessagebus.md), hence, any data consumed beyond this point will not be *utilizable* by the extension.

If your extension requires intensive initialization and you need to use the async/await pattern, you can refer to the [`Async extension initialization and cleanup`](asyncinitcleanup.md). If you need to *share state* between extension points, you can refer to the [`CompositeExtensionFactory<T>`](compositeextensionfactory.md) section.
