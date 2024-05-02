# The `IServiceProvider`

The testing platform offers valuable services to both the testing framework and extension points. These services cater to common needs such as accessing the configuration, parsing and retrieving command-line arguments, obtaining the logging factory, and accessing the logging system, among others. `IServiceProvider` implements the [service locator pattern](https://en.wikipedia.org/wiki/Service_locator_pattern) for the testing platform.

The `IServiceProvider` is derived directly from the base class library.

```cs
namespace System
{
    public interface IServiceProvider
    {
        object? GetService(Type serviceType);
    }
}
```

The testing platform offers handy extension methods to access well-known service objects. All these methods are housed in a static class within the `Microsoft.Testing.Platform.Services` namespace.

```cs
public static class ServiceProviderExtensions
{
    public static TService GetRequiredService<TService>(this IServiceProvider provider)
    public static TService? GetService<TService>(this IServiceProvider provider)
    public static IMessageBus GetMessageBus(this IServiceProvider serviceProvider)
    public static IConfiguration GetConfiguration(this IServiceProvider serviceProvider)
    public static ICommandLineOptions GetCommandLineOptions(this IServiceProvider serviceProvider)
    public static ILoggerFactory GetLoggerFactory(this IServiceProvider serviceProvider)
    public static IOutputDevice GetOutputDevice(this IServiceProvider serviceProvider)
    ...and more
}
```

Most of the registration factories exposed by extension points, which can be registered using the `ITestApplicationBuilder` during the setup of the testing application, provide access to the `IServiceProvider`.

For example, we encountered it earlier when discussing [registering the testing framework](itestframework.md).

```cs
ITestApplicationBuilder RegisterTestFramework(
    Func<IServiceProvider, ITestFrameworkCapabilities> capabilitiesFactory,
    Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework> adapterFactory);
```

As observed above, both the `capabilitiesFactory` and the `adapterFactory` supply the `IServiceProvider` as a parameter.
