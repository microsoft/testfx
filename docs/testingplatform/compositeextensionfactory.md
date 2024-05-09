# The CompositeExtensionFactory\<T\>

As outlined in the [extensions](extensionintro.md) section, the testing platform enables you to implement interfaces to incorporate custom extensions both in and out of process.

Each interface addresses a particular feature, and according to .NET design, you implement this interface in a specific object. You can register the extension itself using the specific registration API `AddXXX` from the `TestHost` or `TestHostController` object from the `ITestApplicationBuilder` as detailed in the corresponding sections.

However, if you need to *share state* between two extensions, the fact that you can implement and register different objects implementing different interfaces makes sharing a challenging task. Without any assistance, you would need a way to pass one extension to the other to share information, which complicates the design.

Hence, the testing platform provides a sophisticated method to implement multiple extension points using the same type, making data sharing a straightforward task. All you need to do is utilize the `CompositeExtensionFactory<T>`, which can then be registered using the same API as you would for a single interface implementation.

For instance, consider a type that implements both `ITestSessionLifetimeHandler` and `IDataConsumer`. This is a common scenario because you often want to gather information from the [testing framework](itestframework.md) and then, when the testing session concludes, you'll dispatch your artifact using the [`IMessageBus`](imessagebus.md) within the `ITestSessionLifetimeHandler.OnTestSessionFinishingAsync`.

What you should do is to normally implement the interfaces:

```cs
internal class CustomExtension : ITestSessionLifetimeHandler, IDataConsumer, ...
{
   ...
}
```

Once you've created the `CompositeExtensionFactory<CustomExtension>` for your type, you can register it with both the `IDataConsumer` and `ITestSessionLifetimeHandler` APIs, which offer an overload for the `CompositeExtensionFactory<T>`:

```cs
ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
...
CompositeExtensionFactory<CustomExtension> compositeExtensionFactory = new(serviceProvider => new CustomExtension());
testApplicationBuilder.TestHost.AddTestSessionLifetimeHandle(compositeExtensionFactory);
testApplicationBuilder.TestHost.AddDataConsumer(compositeExtensionFactory);
...
```

The factory constructor employs the [IServiceProvider](iserviceprovider.md) to access the services provided by the testing platform.

The testing platform will be responsible for managing the lifecycle of the composite extension.

It's important to note that due to the testing platform's support for both *in-process* and *out-of-process* extensions, you can't combine any extension point arbitrarily. The creation and utilization of extensions are contingent on the host type, meaning you can only group *in-process* (TestHost) and *out-of-process* (TestHostController) extensions together.

The following combinations are possible:

* For `ITestApplicationBuilder.TestHost`, you can combine `IDataConsumer` and `ITestSessionLifetimeHandler`.
* For `ITestApplicationBuilder.TestHostControllers`, you can combine `ITestHostEnvironmentVariableProvider` and `ITestHostProcessLifetimeHandler`.
