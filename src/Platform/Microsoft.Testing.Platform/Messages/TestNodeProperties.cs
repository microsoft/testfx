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
public abstract class TestNodeStateProperty : IProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="explanation">Textual explanation of the node state.</param>
    protected TestNodeStateProperty(string? explanation)
        => Explanation = explanation;

    /// <summary>
    /// Gets the textual explanation of the node state.
    /// </summary>
    public string? Explanation { get; }

    private protected virtual void PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Explanation)} = ");
        builder.Append(Explanation);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(TestNodeStateProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }
}

/// <summary>
/// Property that represents test node that has been discovered.
/// </summary>
public sealed class DiscoveredTestNodeStateProperty : TestNodeStateProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveredTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="explanation">Textual explanation of the node.</param>
    public DiscoveredTestNodeStateProperty(string? explanation = null)
        : base(explanation)
    {
    }

    /// <summary>
    /// Gets cached instance of the <see cref="DiscoveredTestNodeStateProperty"/>.
    /// </summary>
    public static DiscoveredTestNodeStateProperty CachedInstance { get; } = new DiscoveredTestNodeStateProperty();

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(DiscoveredTestNodeStateProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }
}

/// <summary>
/// Property that represents a test node that is being executed.
/// </summary>
public sealed class InProgressTestNodeStateProperty : TestNodeStateProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InProgressTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="explanation">Textual explanation of the node.</param>
    public InProgressTestNodeStateProperty(string? explanation = null)
        : base(explanation)
    {
    }

    /// <summary>
    /// Gets cached instance of the <see cref="InProgressTestNodeStateProperty"/>.
    /// </summary>
    public static InProgressTestNodeStateProperty CachedInstance { get; } = new InProgressTestNodeStateProperty();

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(InProgressTestNodeStateProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }
}

/// <summary>
/// Property that represents a test node that has been executed and passed.
/// </summary>
public sealed class PassedTestNodeStateProperty : TestNodeStateProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PassedTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="explanation">Textual explanation of the node.</param>
    public PassedTestNodeStateProperty(string? explanation = null)
        : base(explanation)
    {
    }

    /// <summary>
    /// Gets the cached instance of the <see cref="PassedTestNodeStateProperty"/> property.
    /// </summary>
    public static PassedTestNodeStateProperty CachedInstance { get; } = new PassedTestNodeStateProperty();

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(PassedTestNodeStateProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }
}

/// <summary>
/// Property that represents a test node that has been skipped.
/// </summary>
public sealed class SkippedTestNodeStateProperty : TestNodeStateProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SkippedTestNodeStateProperty"/> class.
    /// </summary>
    /// <param name="explanation">Textual explanation of the node.</param>
    public SkippedTestNodeStateProperty(string? explanation = null)
        : base(explanation)
    {
    }

    /// <summary>
    /// Gets the cached instance of the <see cref="SkippedTestNodeStateProperty"/> property.
    /// </summary>
    public static SkippedTestNodeStateProperty CachedInstance { get; } = new SkippedTestNodeStateProperty();

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(SkippedTestNodeStateProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }
}

/// <summary>
/// Property that represents a test node that has been failed.
/// </summary>
public sealed class FailedTestNodeStateProperty : TestNodeStateProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FailedTestNodeStateProperty"/> class.
    /// </summary>
    public FailedTestNodeStateProperty()
        : base(default)
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

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(FailedTestNodeStateProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }

    private protected override void PrintMembers(StringBuilder builder)
    {
        base.PrintMembers(builder);
        builder.Append(", ");
        builder.Append($"{nameof(Exception)} = ");
        builder.Append(Exception);
    }
}

/// <summary>
/// Property that represents an eventual error in the test node.
/// </summary>
public sealed class ErrorTestNodeStateProperty : TestNodeStateProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorTestNodeStateProperty"/> class.
    /// </summary>
    public ErrorTestNodeStateProperty()
        : base(default)
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

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(ErrorTestNodeStateProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }

    private protected override void PrintMembers(StringBuilder builder)
    {
        base.PrintMembers(builder);
        builder.Append(", ");
        builder.Append($"{nameof(Exception)} = ");
        builder.Append(Exception);
    }
}

/// <summary>
/// Property that represents an eventual timeout in the test node.
/// </summary>
public sealed class TimeoutTestNodeStateProperty : TestNodeStateProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutTestNodeStateProperty"/> class.
    /// </summary>
    public TimeoutTestNodeStateProperty()
        : base(default)
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

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(TimeoutTestNodeStateProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }

    private protected override void PrintMembers(StringBuilder builder)
    {
        base.PrintMembers(builder);
        builder.Append($", {nameof(Exception)} = ");
        builder.Append(Exception);
        builder.Append($", {nameof(Timeout)} = ");
        builder.Append(Timeout);
    }
}

/// <summary>
/// Property that represents an eventual cancellation of a test node.
/// </summary>
public sealed class CancelledTestNodeStateProperty : TestNodeStateProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CancelledTestNodeStateProperty"/> class.
    /// </summary>
    public CancelledTestNodeStateProperty()
        : base(default)
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

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(CancelledTestNodeStateProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }

    private protected override void PrintMembers(StringBuilder builder)
    {
        base.PrintMembers(builder);
        builder.Append(", ");
        builder.Append($"{nameof(Exception)} = ");
        builder.Append(Exception);
    }
}

/// <summary>
/// Information about the timing of a test node.
/// </summary>
public readonly struct TimingInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimingInfo"/> struct.
    /// </summary>
    /// <param name="startTime">Test start time.</param>
    /// <param name="endTime">Test end time.</param>
    /// <param name="duration">Total test duration.</param>
    public TimingInfo(DateTimeOffset startTime, DateTimeOffset endTime, TimeSpan duration)
    {
        StartTime = startTime;
        EndTime = endTime;
        Duration = duration;
    }

    /// <summary>
    /// Gets the test start time.
    /// </summary>
    public DateTimeOffset StartTime { get; }

    /// <summary>
    /// Gets the test end time.
    /// </summary>
    public DateTimeOffset EndTime { get; }

    /// <summary>
    /// Gets the total test duration.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append("TimingInfo { ");
        builder.Append("StartTime = ");
        builder.Append(StartTime);
        builder.Append(", EndTime = ");
        builder.Append(EndTime);
        builder.Append(", Duration = ");
        builder.Append(Duration.ToString());
        builder.Append(" }");
        return builder.ToString();
    }
}

/// <summary>
/// Information about the timing of a test node step.
/// </summary>
public sealed class StepTimingInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StepTimingInfo"/> class.
    /// </summary>
    /// <param name="id">Step identifier.</param>
    /// <param name="description">Step description.</param>
    /// <param name="timing">Step timing info.</param>
    public StepTimingInfo(string id, string description, TimingInfo timing)
    {
        Id = id;
        Description = description;
        Timing = timing;
    }

    /// <summary>
    /// Gets the step identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the step description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the step timing info.
    /// </summary>
    public TimingInfo Timing { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append("StepTimingInfo");
        builder.Append(" { ");
        builder.Append("Id = ");
        builder.Append(Id);
        builder.Append(", Description = ");
        builder.Append(Description);
        builder.Append(", Timing = ");
        builder.Append(Timing);
        builder.Append(" }");
        return builder.ToString();
    }
}

/// <summary>
/// Property that represents the timing of a test node.
/// </summary>
public sealed class TimingProperty : IProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimingProperty"/> class with only global timing.
    /// </summary>
    /// <param name="globalTiming">The global timing information.</param>
    public TimingProperty(TimingInfo globalTiming)
        : this(globalTiming, [])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimingProperty"/> class with global and step timings.
    /// </summary>
    /// <param name="globalTiming">The global timing information.</param>
    /// <param name="stepTimings">The step timing information.</param>
    public TimingProperty(TimingInfo globalTiming, StepTimingInfo[] stepTimings)
    {
        GlobalTiming = globalTiming;
        StepTimings = stepTimings;
    }

    /// <summary>
    /// Gets the global timing information.
    /// </summary>
    public TimingInfo GlobalTiming { get; }

    /// <summary>
    /// Gets the step timing information.
    /// </summary>
    public StepTimingInfo[] StepTimings { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(TestNodeStateProperty));
        builder.Append(" { ");
        builder.Append("GlobalTiming = ");
        builder.Append(GlobalTiming);
        builder.Append(", StepTimings = [");
        builder.AppendJoin(", ", StepTimings.Select(x => x.ToString()));
        builder.Append(']');
        builder.Append(" }");
        return builder.ToString();
    }
}

/// <summary>
/// Line position in a file.
/// </summary>
public readonly struct LinePosition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LinePosition"/> struct.
    /// </summary>
    /// <param name="line">Line number.</param>
    /// <param name="column">Column number.</param>
    public LinePosition(int line, int column)
    {
        Line = line;
        Column = column;
    }

    /// <summary>
    /// Gets the line number.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the column number.
    /// </summary>
    public int Column { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append("LinePosition");
        builder.Append(" { ");
        builder.Append("Line = ");
        builder.Append(Line);
        builder.Append(", Column = ");
        builder.Append(Column);
        builder.Append(' ');
        builder.Append('}');
        return builder.ToString();
    }
}

/// <summary>
/// Line position span in a file.
/// </summary>
public readonly struct LinePositionSpan
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LinePositionSpan"/> struct.
    /// </summary>
    /// <param name="start">Start line position.</param>
    /// <param name="end">End line position.</param>
    public LinePositionSpan(LinePosition start, LinePosition end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Gets the start line position.
    /// </summary>
    public LinePosition Start { get; }

    /// <summary>
    /// Gets the end line position.
    /// </summary>
    public LinePosition End { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append("LinePositionSpan");
        builder.Append(" { ");
        builder.Append("Start = ");
        builder.Append(Start);
        builder.Append(", End = ");
        builder.Append(End);
        builder.Append(' ');
        builder.Append('}');
        return builder.ToString();
    }
}

/// <summary>
/// Base property that represents a file location.
/// </summary>
public abstract class FileLocationProperty : IProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileLocationProperty"/> class.
    /// </summary>
    /// <param name="filePath">File path.</param>
    /// <param name="lineSpan">Line position.</param>
    protected FileLocationProperty(string filePath, LinePositionSpan lineSpan)
    {
        FilePath = filePath;
        LineSpan = lineSpan;
    }

    /// <summary>
    /// Gets the file path.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the line position span.
    /// </summary>
    public LinePositionSpan LineSpan { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append("FileLocationProperty");
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }

    private protected virtual void PrintMembers(StringBuilder builder)
    {
        builder.Append("FilePath = ");
        builder.Append(FilePath);
        builder.Append(", LineSpan = ");
        builder.Append(LineSpan);
    }
}

/// <summary>
/// Property that represents a file location for a test node.
/// </summary>
public sealed class TestFileLocationProperty : FileLocationProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestFileLocationProperty"/> class.
    /// </summary>
    /// <param name="filePath">File path.</param>
    /// <param name="lineSpan">Line position.</param>
    public TestFileLocationProperty(string filePath, LinePositionSpan lineSpan)
        : base(filePath, lineSpan)
    {
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append("TestFileLocationProperty");
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }
}

/// <summary>
/// Property that uniquely identifies a test method. Values are ECMA-335 compliant.
/// </summary>
public sealed class TestMethodIdentifierProperty : IProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestMethodIdentifierProperty"/> class.
    /// </summary>
    /// <param name="assemblyFullName">Assembly full name.</param>
    /// <param name="namespace">Namespace.</param>
    /// <param name="typeName">Type name in metadata format, not including the namespace. Generics are represented by backtick followed by arity. Nested types are represented by <c>+</c>.</param>
    /// <param name="methodName">Method name in metadata format. This is simply the method name, it doesn't include backtick followed by arity.</param>
    /// <param name="methodArity">The number of generic parameters of the method.</param>
    /// <param name="parameterTypeFullNames">Parameter type full names in metadata format.</param>
    /// <param name="returnTypeFullName">Return type full name in metadata format.</param>
    public TestMethodIdentifierProperty(
        string assemblyFullName,
        string @namespace,
        string typeName,
        string methodName,
        int methodArity,
        string[] parameterTypeFullNames,
        string returnTypeFullName)
    {
        AssemblyFullName = assemblyFullName;
        Namespace = @namespace;
        TypeName = typeName;
        MethodName = methodName;
        MethodArity = methodArity;
        ParameterTypeFullNames = parameterTypeFullNames;
        ReturnTypeFullName = returnTypeFullName;
    }

    /// <summary>
    /// Gets the assembly full name.
    /// </summary>
    public string AssemblyFullName { get; }

    /// <summary>
    /// Gets the namespace.
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// Gets the type name in metadata format, not including the namespace.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the method name in metadata format.
    /// </summary>
    public string MethodName { get; }

    /// <summary>
    /// Gets the number of generic parameters of the method.
    /// </summary>
    public int MethodArity { get; }

    /// <summary>
    /// Gets the parameter type full names in metadata format.
    /// </summary>
    public string[] ParameterTypeFullNames { get; }

    /// <summary>
    /// Gets the return type full name in metadata format.
    /// </summary>
    public string ReturnTypeFullName { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append("TestMethodIdentifierProperty");
        builder.Append(" { ");
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
        builder.Append(" }");
        return builder.ToString();
    }
}

/// <summary>
/// Property that represents a generic test metadata property in the shape of a key-value pair associated with a <see cref="TestNode"/>.
/// </summary>
public sealed class TestMetadataProperty : IProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestMetadataProperty"/> class with a key and value.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    public TestMetadataProperty(string key, string value)
    {
        Key = key;
        Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestMetadataProperty"/> class with a key and an empty value.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    public TestMetadataProperty(string key)
        : this(key, string.Empty)
    {
    }

    /// <summary>
    /// Gets the metadata key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the metadata value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append("TestMetadataProperty");
        builder.Append(" { ");
        builder.Append("Key = ");
        builder.Append(Key);
        builder.Append(", Value = ");
        builder.Append(Value);
        builder.Append(" }");
        return builder.ToString();
    }
}

/// <summary>
/// Property that represents standard output to associate with a test node.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public class StandardOutputProperty : IProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StandardOutputProperty"/> class.
    /// </summary>
    /// <param name="standardOutput">The standard output.</param>
    public StandardOutputProperty(string standardOutput)
        => StandardOutput = standardOutput;

    /// <summary>
    /// Gets the standard output.
    /// </summary>
    public string StandardOutput { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append("StandardOutputProperty");
        builder.Append(" { ");
        builder.Append("StandardOutput = ");
        builder.Append(StandardOutput);
        builder.Append(" }");
        return builder.ToString();
    }
}

/// <summary>
/// Property that represents standard error to associate with a test node.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public class StandardErrorProperty : IProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StandardErrorProperty"/> class.
    /// </summary>
    /// <param name="standardError">The standard error.</param>
    public StandardErrorProperty(string standardError)
        => StandardError = standardError;

    /// <summary>
    /// Gets the standard error.
    /// </summary>
    public string StandardError { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append("StandardErrorProperty");
        builder.Append(" { ");
        builder.Append("StandardError = ");
        builder.Append(StandardError);
        builder.Append(" }");
        return builder.ToString();
    }
}

/// <summary>
/// Property that represents multiple artifacts/attachments to associate with a test node.
/// </summary>
public sealed class FileArtifactProperty : IProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileArtifactProperty"/> class.
    /// </summary>
    /// <param name="fileInfo">The file information.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="description">The description.</param>
    public FileArtifactProperty(FileInfo fileInfo, string displayName, string? description = null)
    {
        FileInfo = fileInfo;
        DisplayName = displayName;
        Description = description;
    }

    /// <summary>
    /// Gets the file information.
    /// </summary>
    public FileInfo FileInfo { get; }

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the description.
    /// </summary>
    public string? Description { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append("FileArtifactProperty");
        builder.Append(" { ");
        builder.Append("FileInfo = ");
        builder.Append(FileInfo);
        builder.Append(", DisplayName = ");
        builder.Append(DisplayName);
        builder.Append(", Description = ");
        builder.Append(Description);
        builder.Append(" }");
        return builder.ToString();
    }
}

internal sealed class SerializableKeyValuePairStringProperty : IProperty
{
    public SerializableKeyValuePairStringProperty(string key, string value)
    {
        Key = key;
        Value = value;
    }

    public string Key { get; }

    public string Value { get; }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append("SerializableKeyValuePairStringProperty");
        builder.Append(" { ");
        builder.Append("Key = ");
        builder.Append((object)Key);
        builder.Append(", Value = ");
        builder.Append((object)Value);
        builder.Append(" }");
        return builder.ToString();
    }
}
