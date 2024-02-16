// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

#pragma warning disable SA1201 // Elements should appear in the correct order

/// <summary>
/// The interface that every test node property must implement.
/// </summary>
public interface IProperty
{
}

/// <summary>
/// Node property that represents a key-value pair.
/// </summary>
/// <param name="Key">Key name.</param>
/// <param name="Value">Key value.</param>
public record KeyValuePairStringProperty(string Key, string Value) : IProperty;

/// <summary>
/// Base class for test node state properties.
/// </summary>
/// <param name="Explanation">Textual explanation of the node state.</param>
public abstract record TestNodeStateProperty(string? Explanation) : IProperty;

/// <summary>
/// Property that represents test node that has been discovered.
/// </summary>
/// <param name="Explanation">Textual explanation of the node.</param>
public sealed record DiscoveredTestNodeStateProperty(string? Explanation = null) : TestNodeStateProperty(Explanation)
{
    public static DiscoveredTestNodeStateProperty CachedInstance { get; } = new DiscoveredTestNodeStateProperty();
}

/// <summary>
/// Property that represents a test node that is being executed.
/// </summary>
/// <param name="Explanation">Textual explanation of the node.</param>
public sealed record InProgressTestNodeStateProperty(string? Explanation = null) : TestNodeStateProperty(Explanation)
{
    public static InProgressTestNodeStateProperty CachedInstance { get; } = new InProgressTestNodeStateProperty();
}

/// <summary>
/// Property that represents a test node that has been executed and passed.
/// </summary>
/// <param name="Explanation">Textual explanation of the node.</param>
public sealed record PassedTestNodeStateProperty(string? Explanation = null) : TestNodeStateProperty(Explanation)
{
    public static PassedTestNodeStateProperty CachedInstance { get; } = new PassedTestNodeStateProperty();
}

/// <summary>
/// Property that represents a test node that has been skipped.
/// </summary>
/// <param name="Explanation">Textual explanation of the node.</param>
public sealed record SkippedTestNodeStateProperty(string? Explanation = null) : TestNodeStateProperty(Explanation)
{
    public static SkippedTestNodeStateProperty CachedInstance { get; } = new SkippedTestNodeStateProperty();
}

/// <summary>
/// Property that represents a test node that has been failed.
/// </summary>
public sealed record FailedTestNodeStateProperty : TestNodeStateProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FailedTestNodeStateProperty"/> class.
    /// </summary>
    public FailedTestNodeStateProperty()
        : base(default(string))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FailedTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="explanation">Failure explanation.</param>
    public FailedTestNodeStateProperty(string explanation)
        : base(explanation)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FailedTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="exception">Failure exception.</param>
    /// <param name="explanation">Failure explanation.</param>
    public FailedTestNodeStateProperty(Exception exception, string? explanation = null)
        : base(explanation ?? exception.Message)
    {
        Exception = exception;
    }

    /// <summary>
    /// Gets the failure exception.
    /// </summary>
    public Exception? Exception { get; }
}

/// <summary>
/// Property that represents an eventual error in the test node.
/// </summary>
public sealed record ErrorTestNodeStateProperty : TestNodeStateProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorTestNodeStateProperty"/> class.
    /// </summary>
    public ErrorTestNodeStateProperty()
        : base(default(string))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="explanation">Error explanation.</param>
    public ErrorTestNodeStateProperty(string explanation)
        : base(explanation)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="exception">Error exception.</param>
    /// <param name="explanation">Error explanation.</param>
    public ErrorTestNodeStateProperty(Exception exception, string? explanation = null)
        : base(explanation ?? exception.Message)
    {
        Exception = exception;
    }

    /// <summary>
    /// Gets the error exception.
    /// </summary>
    public Exception? Exception { get; }
}

/// <summary>
/// Property that represents an eventual timeout in the test node.
/// </summary>
public sealed record TimeoutTestNodeStateProperty : TestNodeStateProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutTestNodeStateProperty"/> class.
    /// </summary>
    public TimeoutTestNodeStateProperty()
        : base(default(string))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="explanation">Timeout explanation.</param>
    public TimeoutTestNodeStateProperty(string explanation)
        : base(explanation)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="exception">Timeout exception.</param>
    /// <param name="explanation">Timeout explanation.</param>
    public TimeoutTestNodeStateProperty(Exception exception, string? explanation = null)
        : base(explanation ?? exception.Message)
    {
        Exception = exception;
    }

    /// <summary>
    /// Gets get the timeout exception.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets get the timeout Timespan.
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>
/// Property that represents an eventual cancellation of a test node.
/// </summary>
public sealed record CancelledTestNodeStateProperty : TestNodeStateProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CancelledTestNodeStateProperty"/> class.
    /// </summary>
    public CancelledTestNodeStateProperty()
        : base(default(string))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CancelledTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="explanation">Cancellation explanation.</param>
    public CancelledTestNodeStateProperty(string explanation)
        : base(explanation)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CancelledTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="exception">Cancellation exception.</param>
    /// <param name="explanation">Cancellation explanation.</param>
    public CancelledTestNodeStateProperty(Exception exception, string? explanation = null)
        : base(explanation ?? exception.Message)
    {
        Exception = exception;
    }

    /// <summary>
    /// Gets the cancellation exception.
    /// </summary>
    public Exception? Exception { get; }
}

/// <summary>
/// Information about the timing of a test node.
/// </summary>
/// <param name="StartTime">Test start time.</param>
/// <param name="EndTime">Test end time.</param>
/// <param name="Duration">Total test duration.</param>
public readonly record struct TimingInfo(DateTimeOffset StartTime, DateTimeOffset EndTime, TimeSpan Duration);

/// <summary>
/// Information about the timing of a test node step.
/// </summary>
/// <param name="Id">Step identifier.</param>
/// <param name="Description">Step description.</param>
/// <param name="Timing">Step timing info.</param>
public sealed record StepTimingInfo(string Id, string Description, TimingInfo Timing);

/// <summary>
/// Property that represents the timing of a test node.
/// </summary>
public sealed record TimingProperty : IProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimingProperty"/> class.
    /// </summary>
    /// <param name="globalTiming">Timing info.</param>
    public TimingProperty(TimingInfo globalTiming)
        : this(globalTiming, [])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimingProperty"/> class.
    /// </summary>
    /// <param name="globalTiming">Timing info.</param>
    /// <param name="stepTimings">Steps timing.</param>
    public TimingProperty(TimingInfo globalTiming, StepTimingInfo[] stepTimings)
    {
        GlobalTiming = globalTiming;
        StepTimings = stepTimings;
    }

    /// <summary>
    /// Gets the global timing info.
    /// </summary>
    public TimingInfo GlobalTiming { get; }

    /// <summary>
    /// Gets the steps timing info.
    /// </summary>
    public StepTimingInfo[] StepTimings { get; }
}

/// <summary>
/// Line position in a file.
/// </summary>
/// <param name="Line">Line number.</param>
/// <param name="Column">Column number.</param>
public record struct LinePosition(int Line, int Column);

/// <summary>
/// Line position span in a file.
/// </summary>
/// <param name="Start">Start line position.</param>
/// <param name="End">End line position.</param>
public record struct LinePositionSpan(LinePosition Start, LinePosition End);

/// <summary>
/// Base property that represents a file location.
/// </summary>
/// <param name="FilePath">File path.</param>
/// <param name="LineSpan">Line position.</param>
public abstract record FileLocationProperty(string FilePath, LinePositionSpan LineSpan) : IProperty;

/// <summary>
/// Property that represents a file location for a test node.
/// </summary>
/// <param name="FilePath">File path.</param>
/// <param name="LineSpan">Line position.</param>
public sealed record TestFileLocationProperty(string FilePath, LinePositionSpan LineSpan) : FileLocationProperty(FilePath, LineSpan);

/// <summary>
/// Property that uniquely identifies a test method. Values are ECMA-335 compliant.
/// </summary>
/// <param name="AssemblyFullName">Assembly full name.</param>
/// <param name="Namespace">Namespace.</param>
/// <param name="TypeName">Type name.</param>
/// <param name="MethodName">Method name.</param>
/// <param name="ParameterTypeFullNames">Parameter type full name.</param>
/// <param name="ReturnTypeFullName">Return type full name.</param>
public sealed record TestMethodIdentifierProperty(string AssemblyFullName, string Namespace, string TypeName, string MethodName, string[] ParameterTypeFullNames, string ReturnTypeFullName) : IProperty;

/// <summary>
/// Property that represents a generic test metadata property.
/// </summary>
/// <param name="Key">Key name.</param>
/// <param name="Value">Value name.</param>
public sealed record TestMetadataProperty(string Key, string Value) : IProperty;

internal sealed record SerializableKeyValuePairStringProperty(string Key, string Value) : KeyValuePairStringProperty(Key, Value);

internal sealed record SerializableNamedKeyValuePairsStringProperty(string Name, KeyValuePair<string, string>[] Pairs) : IProperty;

internal sealed record SerializableNamedArrayStringProperty(string Name, string[] Values) : IProperty;
