// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// The interface that every test node property must implement.
/// </summary>
public interface IProperty;

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
    /// <summary>
    /// Gets cached instance of the <see cref="DiscoveredTestNodeStateProperty"/>.
    /// </summary>
    public static DiscoveredTestNodeStateProperty CachedInstance { get; } = new DiscoveredTestNodeStateProperty();
}

/// <summary>
/// Property that represents a test node that is being executed.
/// </summary>
/// <param name="Explanation">Textual explanation of the node.</param>
public sealed record InProgressTestNodeStateProperty(string? Explanation = null) : TestNodeStateProperty(Explanation)
{
    /// <summary>
    /// Gets cached instance of the <see cref="InProgressTestNodeStateProperty"/>.
    /// </summary>
    public static InProgressTestNodeStateProperty CachedInstance { get; } = new InProgressTestNodeStateProperty();
}

/// <summary>
/// Property that represents a test node that has been executed and passed.
/// </summary>
/// <param name="Explanation">Textual explanation of the node.</param>
public sealed record PassedTestNodeStateProperty(string? Explanation = null) : TestNodeStateProperty(Explanation)
{
    /// <summary>
    /// Gets the cached instance of the <see cref="PassedTestNodeStateProperty"/> property.
    /// </summary>
    public static PassedTestNodeStateProperty CachedInstance { get; } = new PassedTestNodeStateProperty();
}

/// <summary>
/// Property that represents a test node that has been skipped.
/// </summary>
/// <param name="Explanation">Textual explanation of the node.</param>
public sealed record SkippedTestNodeStateProperty(string? Explanation = null) : TestNodeStateProperty(Explanation)
{
    /// <summary>
    /// Gets the cached instance of the <see cref="SkippedTestNodeStateProperty"/> property.
    /// </summary>
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
        : base(explanation ?? exception.Message) => Exception = exception;

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
        : base(explanation ?? exception.Message) => Exception = exception;

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
        : base(explanation ?? exception.Message) => Exception = exception;

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
        : base(explanation ?? exception.Message) => Exception = exception;

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

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "https://github.com/dotnet/roslyn/issues/52421")]
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("GlobalTiming = ");
        builder.Append(GlobalTiming);
        builder.Append(", StepTimings = [");
        builder.AppendJoin(", ", StepTimings.Select(x => x.ToString()));
        builder.Append(']');
        return true;
    }
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
/// <param name="TypeName">Type name in metadata format, not including the namespace. Generics are represented by backtick followed by arity. Nested types are represented by <c>+</c>.</param>
/// <param name="MethodName">Method name in metadata format. This is simply the method name, it doesn't include backtick followed by arity.</param>
/// <param name="MethodArity">The number of generic parameters of the method.</param>
/// <param name="ParameterTypeFullNames">Parameter type full names in metadata format.</param>
/// <param name="ReturnTypeFullName">Return type full name in metadata format.</param>
public sealed record TestMethodIdentifierProperty(string AssemblyFullName, string Namespace, string TypeName, string MethodName, int MethodArity, string[] ParameterTypeFullNames, string ReturnTypeFullName) : IProperty
{
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "https://github.com/dotnet/roslyn/issues/52421")]
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("AssemblyFullName = ");
        builder.Append(AssemblyFullName);
        builder.Append(", Namespace = ");
        builder.Append(Namespace);
        builder.Append(", TypeName = ");
        builder.Append(TypeName);
        builder.Append(", MethodName = ");
        builder.Append(MethodName);
        builder.Append(", MethodArity = ");
        builder.Append(MethodArity);
        builder.Append(", ParameterTypeFullNames = [");
        builder.AppendJoin(", ", ParameterTypeFullNames);
        builder.Append("], ReturnTypeFullName = ");
        builder.Append(ReturnTypeFullName);
        return true;
    }
}

/// <summary>
/// Initializes a new instance of the <see cref="TestMetadataProperty"/> class.
/// Property that represents a generic test metadata property in the shape of a key-value pair associated with a <see cref="TestNode"/>.
/// </summary>
/// <param name="Key">The metadata key.</param>
/// <param name="Value">The metadata value.</param>
public sealed record TestMetadataProperty(string Key, string Value) : IProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestMetadataProperty"/> class.
    /// Property that represents a generic test metadata property in the shape of a value associated with a <see cref="TestNode"/>.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    public TestMetadataProperty(string key)
        : this(key, string.Empty)
    {
    }
}

/// <summary>
/// Property that represents standard output to associate with a test node.
/// </summary>
/// <param name="StandardOutput">The standard output.</param>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public record StandardOutputProperty(string StandardOutput) : IProperty;

/// <summary>
/// Property that represents standard error to associate with a test node.
/// </summary>
/// <param name="StandardError">The standard error.</param>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public record StandardErrorProperty(string StandardError) : IProperty;

/// <summary>
/// Property that represents multiple artifacts/attachments to associate with a test node.
/// </summary>
/// <param name="FileInfo">The file information.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Description">The description.</param>
public record FileArtifactProperty(FileInfo FileInfo, string DisplayName, string? Description = null) : IProperty;

internal sealed record SerializableKeyValuePairStringProperty(string Key, string Value) : IProperty;
