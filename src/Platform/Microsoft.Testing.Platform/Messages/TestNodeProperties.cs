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
public abstract class TestNodeStateProperty : IProperty, IEquatable<TestNodeStateProperty>
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

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as TestNodeStateProperty);

    /// <inheritdoc />
    public bool Equals(TestNodeStateProperty? other)
        => other is not null && Explanation == other.Explanation;

    /// <inheritdoc />
    public override int GetHashCode()
        => Explanation?.GetHashCode() ?? 0;
}

/// <summary>
/// Property that represents test node that has been discovered.
/// </summary>
public sealed class DiscoveredTestNodeStateProperty : TestNodeStateProperty, IEquatable<DiscoveredTestNodeStateProperty>
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

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as DiscoveredTestNodeStateProperty);

    /// <inheritdoc />
    public bool Equals(DiscoveredTestNodeStateProperty? other)
        => other is not null && Explanation == other.Explanation;

    /// <inheritdoc />
    public override int GetHashCode()
        => Explanation?.GetHashCode() ?? 0;
}

/// <summary>
/// Property that represents a test node that is being executed.
/// </summary>
public sealed class InProgressTestNodeStateProperty : TestNodeStateProperty, IEquatable<InProgressTestNodeStateProperty>
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

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as InProgressTestNodeStateProperty);

    /// <inheritdoc />
    public bool Equals(InProgressTestNodeStateProperty? other)
        => other is not null && Explanation == other.Explanation;

    /// <inheritdoc />
    public override int GetHashCode()
        => Explanation?.GetHashCode() ?? 0;
}

/// <summary>
/// Property that represents a test node that has been executed and passed.
/// </summary>
public sealed class PassedTestNodeStateProperty : TestNodeStateProperty, IEquatable<PassedTestNodeStateProperty>
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

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as PassedTestNodeStateProperty);

    /// <inheritdoc />
    public bool Equals(PassedTestNodeStateProperty? other)
        => other is not null && Explanation == other.Explanation;

    /// <inheritdoc />
    public override int GetHashCode()
        => Explanation?.GetHashCode() ?? 0;
}

/// <summary>
/// Property that represents a test node that has been skipped.
/// </summary>
public sealed class SkippedTestNodeStateProperty : TestNodeStateProperty, IEquatable<SkippedTestNodeStateProperty>
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

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as SkippedTestNodeStateProperty);

    /// <inheritdoc />
    public bool Equals(SkippedTestNodeStateProperty? other)
        => other is not null && Explanation == other.Explanation;

    /// <inheritdoc />
    public override int GetHashCode()
        => Explanation?.GetHashCode() ?? 0;
}

/// <summary>
/// Property that represents a test node that has been failed.
/// </summary>
public sealed class FailedTestNodeStateProperty : TestNodeStateProperty, IEquatable<FailedTestNodeStateProperty>
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

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as FailedTestNodeStateProperty);

    /// <inheritdoc />
    public bool Equals(FailedTestNodeStateProperty? other)
        => other is not null && Explanation == other.Explanation && Equals(Exception, other.Exception);

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(Explanation, Exception);
}

/// <summary>
/// Property that represents an eventual error in the test node.
/// </summary>
public sealed class ErrorTestNodeStateProperty : TestNodeStateProperty, IEquatable<ErrorTestNodeStateProperty>
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

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as ErrorTestNodeStateProperty);

    /// <inheritdoc />
    public bool Equals(ErrorTestNodeStateProperty? other)
        => other is not null && Explanation == other.Explanation && Equals(Exception, other.Exception);

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(Explanation, Exception);
}

/// <summary>
/// Property that represents an eventual timeout in the test node.
/// </summary>
public sealed class TimeoutTestNodeStateProperty : TestNodeStateProperty, IEquatable<TimeoutTestNodeStateProperty>
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

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as TimeoutTestNodeStateProperty);

    /// <inheritdoc />
    public bool Equals(TimeoutTestNodeStateProperty? other)
        => other is not null && Explanation == other.Explanation && Equals(Exception, other.Exception) && Timeout == other.Timeout;

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(Explanation, Exception, Timeout);
}

/// <summary>
/// Property that represents an eventual cancellation of a test node.
/// </summary>
public sealed class CancelledTestNodeStateProperty : TestNodeStateProperty, IEquatable<CancelledTestNodeStateProperty>
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

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as CancelledTestNodeStateProperty);

    /// <inheritdoc />
    public bool Equals(CancelledTestNodeStateProperty? other)
        => other is not null && Explanation == other.Explanation && Equals(Exception, other.Exception);

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(Explanation, Exception);
}

/// <summary>
/// Information about the timing of a test node.
/// </summary>
public readonly struct TimingInfo : IEquatable<TimingInfo>
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
        builder.Append($"{nameof(TimingInfo)} {{ ");
        builder.Append($"{nameof(StartTime)} = ");
        builder.Append(StartTime);
        builder.Append($", {nameof(EndTime)} = ");
        builder.Append(EndTime);
        builder.Append($", {nameof(Duration)} = ");
        builder.Append(Duration.ToString());
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is TimingInfo other && Equals(other);

    /// <inheritdoc />
    public bool Equals(TimingInfo other)
        => StartTime.Equals(other.StartTime) && EndTime.Equals(other.EndTime) && Duration.Equals(other.Duration);

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(StartTime, EndTime, Duration);

    /// <summary>
    /// Implementation of the non-equality operator.
    /// </summary>
    public static bool operator !=(TimingInfo left, TimingInfo right)
        => !(left == right);

    /// <summary>
    /// Implementation of the equality operator.
    /// </summary>
    public static bool operator ==(TimingInfo left, TimingInfo right)
        => left.Equals(right);
}

/// <summary>
/// Information about the timing of a test node step.
/// </summary>
public sealed class StepTimingInfo : IEquatable<StepTimingInfo>
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
        builder.Append(nameof(StepTimingInfo));
        builder.Append(" { ");
        builder.Append($"{nameof(Id)} = ");
        builder.Append(Id);
        builder.Append($", {nameof(Description)} = ");
        builder.Append(Description);
        builder.Append($", {nameof(Timing)} = ");
        builder.Append(Timing);
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as StepTimingInfo);

    /// <inheritdoc />
    public bool Equals(StepTimingInfo? other)
        => other is not null && Id == other.Id && Description == other.Description && Timing.Equals(other.Timing);

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(Id, Description, Timing);
}

/// <summary>
/// Property that represents the timing of a test node.
/// </summary>
public sealed class TimingProperty : IProperty, IEquatable<TimingProperty>
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
        builder.Append(nameof(TimingProperty));
        builder.Append(" { ");
        builder.Append($"{nameof(GlobalTiming)} = ");
        builder.Append(GlobalTiming);
        builder.Append($", {nameof(StepTimings)} = [");
        builder.AppendJoin(", ", StepTimings.Select(x => x.ToString()));
        builder.Append(']');
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as TimingProperty);

    /// <inheritdoc />
    public bool Equals(TimingProperty? other)
        => other is not null && GlobalTiming.Equals(other.GlobalTiming) && StepTimings.SequenceEqual(other.StepTimings);

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(GlobalTiming, StructuralComparisons.StructuralEqualityComparer.GetHashCode(StepTimings));
}

/// <summary>
/// Line position in a file.
/// </summary>
public readonly struct LinePosition : IEquatable<LinePosition>
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
        builder.Append(nameof(LinePosition));
        builder.Append(" { ");
        builder.Append($"{nameof(Line)} = ");
        builder.Append(Line);
        builder.Append($", {nameof(Column)} = ");
        builder.Append(Column);
        builder.Append(' ');
        builder.Append('}');
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is LinePosition other && Equals(other);

    /// <inheritdoc />
    public bool Equals(LinePosition other)
        => Line == other.Line && Column == other.Column;

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(Line, Column);

    /// <summary>
    /// Implementation of the non-equality operator.
    /// </summary>
    public static bool operator !=(LinePosition left, LinePosition right)
        => !(left == right);

    /// <summary>
    /// Implementation of the equality operator.
    /// </summary>
    public static bool operator ==(LinePosition left, LinePosition right)
        => left.Equals(right);
}

/// <summary>
/// Line position span in a file.
/// </summary>
public readonly struct LinePositionSpan : IEquatable<LinePositionSpan>
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
        builder.Append(nameof(LinePositionSpan));
        builder.Append(" { ");
        builder.Append($"{nameof(Start)} = ");
        builder.Append(Start);
        builder.Append($", {nameof(End)} = ");
        builder.Append(End);
        builder.Append(' ');
        builder.Append('}');
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is LinePositionSpan other && Equals(other);

    /// <inheritdoc />
    public bool Equals(LinePositionSpan other)
        => Start.Equals(other.Start) && End.Equals(other.End);

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(Start, End);

    /// <summary>
    /// Implementation of the non-equality operator.
    /// </summary>
    public static bool operator !=(LinePositionSpan left, LinePositionSpan right)
        => !(left == right);

    /// <summary>
    /// Implementation of the equality operator.
    /// </summary>
    public static bool operator ==(LinePositionSpan left, LinePositionSpan right)
        => left.Equals(right);
}

/// <summary>
/// Base property that represents a file location.
/// </summary>
public abstract class FileLocationProperty : IProperty, IEquatable<FileLocationProperty>
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
        builder.Append(nameof(FileLocationProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }

    private protected virtual void PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(FilePath)} = ");
        builder.Append(FilePath);
        builder.Append($", {nameof(LineSpan)} = ");
        builder.Append(LineSpan);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as FileLocationProperty);

    /// <inheritdoc />
    public bool Equals(FileLocationProperty? other)
        => other is not null && FilePath == other.FilePath && LineSpan.Equals(other.LineSpan);

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(FilePath, LineSpan);
}

/// <summary>
/// Property that represents a file location for a test node.
/// </summary>
public sealed class TestFileLocationProperty : FileLocationProperty, IEquatable<TestFileLocationProperty>
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
        builder.Append(nameof(TestFileLocationProperty));
        builder.Append(" { ");
        PrintMembers(builder);
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as TestFileLocationProperty);

    /// <inheritdoc />
    public bool Equals(TestFileLocationProperty? other) => other is not null && FilePath == other.FilePath && LineSpan.Equals(other.LineSpan);

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(FilePath, LineSpan);
}

/// <summary>
/// Property that uniquely identifies a test method. Values are ECMA-335 compliant.
/// </summary>
public sealed class TestMethodIdentifierProperty : IProperty, IEquatable<TestMethodIdentifierProperty>
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
        builder.Append(nameof(TestMethodIdentifierProperty));
        builder.Append(" { ");
        builder.Append($"{nameof(AssemblyFullName)} = ");
        builder.Append(AssemblyFullName);
        builder.Append($", {nameof(Namespace)} = ");
        builder.Append(Namespace);
        builder.Append($", {nameof(TypeName)} = ");
        builder.Append(TypeName);
        builder.Append($", {nameof(MethodName)} = ");
        builder.Append(MethodName);
        builder.Append($", {nameof(MethodArity)} = ");
        builder.Append(MethodArity);
        builder.Append($", {nameof(ParameterTypeFullNames)} = [");
        builder.AppendJoin(", ", ParameterTypeFullNames);
        builder.Append($"], {nameof(ReturnTypeFullName)} = ");
        builder.Append(ReturnTypeFullName);
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as TestMethodIdentifierProperty);

    /// <inheritdoc />
    public bool Equals(TestMethodIdentifierProperty? other)
        => other is not null && AssemblyFullName == other.AssemblyFullName && Namespace == other.Namespace && TypeName == other.TypeName && MethodName == other.MethodName && MethodArity == other.MethodArity && ParameterTypeFullNames.SequenceEqual(other.ParameterTypeFullNames) && ReturnTypeFullName == other.ReturnTypeFullName;

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(AssemblyFullName, Namespace, TypeName, MethodName, MethodArity, StructuralComparisons.StructuralEqualityComparer.GetHashCode(ParameterTypeFullNames), ReturnTypeFullName);
}

/// <summary>
/// Property that represents a generic test metadata property in the shape of a key-value pair associated with a <see cref="TestNode"/>.
/// </summary>
public sealed class TestMetadataProperty : IProperty, IEquatable<TestMetadataProperty>
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
        builder.Append(nameof(TestMetadataProperty));
        builder.Append(" { ");
        builder.Append($"{nameof(Key)} = ");
        builder.Append(Key);
        builder.Append($", {nameof(Value)} = ");
        builder.Append(Value);
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as TestMetadataProperty);

    /// <inheritdoc />
    public bool Equals(TestMetadataProperty? other)
        => other is not null && Key == other.Key && Value == other.Value;

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(Key, Value);
}

/// <summary>
/// Property that represents standard output to associate with a test node.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public class StandardOutputProperty : IProperty, IEquatable<StandardOutputProperty>
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
        builder.Append(nameof(StandardOutputProperty));
        builder.Append(" { ");
        builder.Append($"{nameof(StandardOutput)} = ");
        builder.Append(StandardOutput);
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as StandardOutputProperty);

    /// <inheritdoc />
    public bool Equals(StandardOutputProperty? other)
        => other is not null && StandardOutput == other.StandardOutput;

    /// <inheritdoc />
    public override int GetHashCode()
        => StandardOutput?.GetHashCode() ?? 0;
}

/// <summary>
/// Property that represents standard error to associate with a test node.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public class StandardErrorProperty : IProperty, IEquatable<StandardErrorProperty>
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
        builder.Append(nameof(StandardErrorProperty));
        builder.Append(" { ");
        builder.Append($"{nameof(StandardError)} = ");
        builder.Append(StandardError);
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as StandardErrorProperty);

    /// <inheritdoc />
    public bool Equals(StandardErrorProperty? other)
        => other is not null && StandardError == other.StandardError;

    /// <inheritdoc />
    public override int GetHashCode()
        => StandardError?.GetHashCode() ?? 0;
}

/// <summary>
/// Property that represents multiple artifacts/attachments to associate with a test node.
/// </summary>
public sealed class FileArtifactProperty : IProperty, IEquatable<FileArtifactProperty>
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
        builder.Append(nameof(FileArtifactProperty));
        builder.Append(" { ");
        builder.Append($"{nameof(FileInfo)} = ");
        builder.Append(FileInfo);
        builder.Append($", {nameof(DisplayName)} = ");
        builder.Append(DisplayName);
        builder.Append($", {nameof(Description)} = ");
        builder.Append(Description);
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as FileArtifactProperty);

    /// <inheritdoc />
    public bool Equals(FileArtifactProperty? other)
        => other is not null && Equals(FileInfo, other.FileInfo) && DisplayName == other.DisplayName && Description == other.Description;

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(FileInfo, DisplayName, Description);
}

internal sealed class SerializableKeyValuePairStringProperty : IProperty, IEquatable<SerializableKeyValuePairStringProperty>
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
        builder.Append(nameof(SerializableKeyValuePairStringProperty));
        builder.Append(" { ");
        builder.Append($"{nameof(Key)} = ");
        builder.Append(Key);
        builder.Append($", {nameof(Value)} = ");
        builder.Append(Value);
        builder.Append(" }");
        return builder.ToString();
    }

    public override bool Equals(object? obj)
        => Equals(obj as SerializableKeyValuePairStringProperty);

    public bool Equals(SerializableKeyValuePairStringProperty? other)
        => other is not null && Key == other.Key && Value == other.Value;

    public override int GetHashCode()
        => RoslynHashCode.Combine(Key, Value);
}
