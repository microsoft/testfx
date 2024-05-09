# Well known `TestNodeUpdateMessage.TestNode` properties

As detailed in the [requests section](irequest.md), the testing platform identifies specific properties added to the `TestNodeUpdateMessage` to determine the status of a `TestNode` (e.g., successful, failed, skipped, etc.). This allows the runtime to accurately display a list of failed tests with their corresponding information in the console, and to set the appropriate exit code for the test process.

In this segment, we'll elucidate the various well-known `IProperty` options and their respective implications.

If you're looking for a comprehensive list of well-known properties, you can find it [here](https://github.com/microsoft/testfx/blob/main/src/Platform/Microsoft.Testing.Platform/Messages/TestNodeProperties.cs). If you notice that a property description is missing, please don't hesitate to file an issue.

We can divide the properties in:

1. *Generic information*: Properties that can be included in any kind of request.
1. *Discovery information*: Properties that are supplied during a `DiscoverTestExecutionRequest` discovery request.
1. *Execution information*: Properties that are supplied during a test execution request `RunTestExecutionRequest`.

Certain properties are **required**, while others are optional. The mandatory properties are required to provide basic testing functionality, such as reporting failed tests and indicating whether the entire test session was successful or not.

Optional properties, on the other hand, enhance the testing experience by providing additional information. They are particularly useful in IDE scenarios (like VS, VSCode, etc.), console runs, or when supporting specific extensions that require more detailed information to function correctly. However, these optional properties do not affect the execution of the tests.

> [!NOTE]
> Extensions are tasked with alerting and managing exceptions when they require specific information to operate correctly. If an extension lacks the necessary information, it should not cause the test execution to fail, but rather, it should simply opt-out.

## Generic information

```cs
public record KeyValuePairStringProperty(string Key, string Value) : IProperty;
```

The `KeyValuePairStringProperty` stands for a general key/value pair data.

```cs
public record struct LinePosition(int Line, int Column);
public record struct LinePositionSpan(LinePosition Start, LinePosition End);
public abstract record FileLocationProperty(string FilePath, LinePositionSpan LineSpan) : IProperty;
public sealed record TestFileLocationProperty(string FilePath, LinePositionSpan LineSpan) : FileLocationProperty(FilePath, LineSpan);
```

`TestFileLocationProperty` is used to pinpoint the location of the test within the source file. This is particularly useful when the initiator is an IDE like Visual Studio or Visual Studio Code.

```cs
public sealed record TestMethodIdentifierProperty(string AssemblyFullName, string Namespace, string TypeName, string MethodName, string[] ParameterTypeFullNames, string ReturnTypeFullName)
```

`TestMethodIdentifierProperty` is a unique identifier for a test method, adhering to the ECMA-335 standard.

> [!NOTE]
> The data needed to create this property can be conveniently obtained using the .NET reflection feature, using types from the `System.Reflection` namespace.

```cs
public sealed record TestMetadataProperty(string Key, string Value)
```

`TestMetadataProperty` is utilized to convey the characteristics or *traits* of a `TestNode`.

## Discovery information

```cs
public sealed record DiscoveredTestNodeStateProperty(string? Explanation = null)
{
    public static DiscoveredTestNodeStateProperty CachedInstance { get; }
}
```

The `DiscoveredTestNodeStateProperty` indicates that this TestNode has been discovered. It is utilized when a `DiscoverTestExecutionRequest` is sent to the test framework.
Take note of the handy cached value offered by the `CachedInstance` property.
This property is **required**.

## Execution information

```cs
public sealed record InProgressTestNodeStateProperty(string? Explanation = null)
{
    public static InProgressTestNodeStateProperty CachedInstance { get; }
}
```

The `InProgressTestNodeStateProperty` informs the testing platform that the `TestNode` has been scheduled for execution and is currently in progress.
Take note of the handy cached value offered by the `CachedInstance` property.

```cs
public readonly record struct TimingInfo(DateTimeOffset StartTime, DateTimeOffset EndTime, TimeSpan Duration);
public sealed record StepTimingInfo(string Id, string Description, TimingInfo Timing);

public sealed record TimingProperty : IProperty
{
    public TimingProperty(TimingInfo globalTiming)
    public TimingProperty(TimingInfo globalTiming, StepTimingInfo[] stepTimings)
    public TimingInfo GlobalTiming { get; }
    public StepTimingInfo[] StepTimings { get; }
}
```

The `TimingProperty` is utilized to relay timing details about the `TestNode` execution. It also allows for the timing of individual execution steps via `StepTimingInfo`. This is particularly useful when your test concept is divided into multiple phases such as initialization, execution, and cleanup.

### Execution information - test outcome information

***One and only one*** of the following properties is **required** per `TestNode` and communicates the result of the `TestNode` to the testing platform.

```cs
public sealed record PassedTestNodeStateProperty(string? Explanation = null)
{
    public static PassedTestNodeStateProperty CachedInstance { get; }
}
```

`PassedTestNodeStateProperty` informs the testing platform that this `TestNode` is passed.
Take note of the handy cached value offered by the `CachedInstance` property.

```cs
public sealed record SkippedTestNodeStateProperty(string? Explanation = null)
{
    public static SkippedTestNodeStateProperty CachedInstance { get; }
}
```

`SkippedTestNodeStateProperty` informs the testing platform that this `TestNode` was skipped.
Take note of the handy cached value offered by the `CachedInstance` property.

```cs
public sealed record FailedTestNodeStateProperty
{
    public FailedTestNodeStateProperty(string explanation)
    public FailedTestNodeStateProperty(Exception exception, string? explanation = null)
    public Exception? Exception { get; }
}
```

`FailedTestNodeStateProperty` informs the testing platform that this `TestNode` is failed after an assertion.

```cs
public sealed record ErrorTestNodeStateProperty
{
    public ErrorTestNodeStateProperty(string explanation)
    public ErrorTestNodeStateProperty(Exception exception, string? explanation = null)
    public Exception? Exception { get; }
}
```

`ErrorTestNodeStateProperty` informs the testing platform that this `TestNode` has failed. This type of failure is different from the `FailedTestNodeStateProperty`, which is used for assertion failures. For example, you can report issues like test initialization errors with `ErrorTestNodeStateProperty`.

```cs
public sealed record TimeoutTestNodeStateProperty
{
    public TimeoutTestNodeStateProperty(string explanation)
    public TimeoutTestNodeStateProperty(Exception exception, string? explanation = null)
    public Exception? Exception { get; }
    public TimeSpan? Timeout { get; init; }
}
```

`TimeoutTestNodeStateProperty` informs the testing platform that this `TestNode` is failed for a timeout reason. You can report the timeout using the `Timeout` property.

```cs
public sealed record CancelledTestNodeStateProperty
{
    public CancelledTestNodeStateProperty(string explanation)
    public CancelledTestNodeStateProperty(Exception exception, string? explanation = null)
    public Exception? Exception { get; }
}
```

`CancelledTestNodeStateProperty` informs the testing platform that this `TestNode` has failed due to cancellation.
