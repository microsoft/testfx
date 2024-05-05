# The `IOutputDevice`

The testing platform encapsulates the idea of an *output device*, allowing the testing framework and extensions to *present* information by transmitting any kind of data to the currently utilized display system.

The most traditional example of an *output device* is the console output.

> [!NOTE]
> While the testing platform is engineered to support custom *output devices*, currently, this extension point is not available.

To transmit data to the *output device*, you must obtain the `IOutputDevice` from the [`IServiceProvider`](iserviceprovider.md).
The API consists of:

```cs
public interface IOutputDevice
{
    Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data);
}

public interface IOutputDeviceDataProducer : IExtension
{
}

public interface IOutputDeviceData
{
}
```

The `IOutputDeviceDataProducer` extends the [`IExtension`](iextension.md) and provides information about the sender to the *output device*.
The `IOutputDeviceData` serves as a placeholder interface. The concept behind `IOutputDevice` is to accommodate more intricate information than just colored text. For instance, it could be a complex object that can be graphically represented.

The testing platform, by default, offers a traditional colored text model for the `IOutputDeviceData` object:

```cs
public class TextOutputDeviceData : IOutputDeviceData
{
    public TextOutputDeviceData(string text)
    public string Text { get; }
}

public sealed class FormattedTextOutputDeviceData : TextOutputDeviceData
{
    public FormattedTextOutputDeviceData(string text)
    public IColor? ForegroundColor { get; init; }
    public IColor? BackgroundColor { get; init; }
}

public sealed class SystemConsoleColor : IColor
{
    public ConsoleColor ConsoleColor { get; init; }
}
```

Here's an example of how you might use the colored text with the *active* output device:

```cs
IServiceProvider serviceProvider = ...get the service provider...
IOutputDevice outputDevice = serviceProvider.GetOutputDevice();
await outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"TestingFramework version '{Version}' running tests with parallelism of {_dopValue}") { ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Green } });
```

Beyond the standard use of colored text, the main advantage of `IOutputDevice` and `IOutputDeviceData` is that the *output device* is entirely independent and unknown to the user. This allows for the development of complex user interfaces. For example, it's entirely feasible to implement a *real-time* web application that displays the progress of tests.
