// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

public interface IProperty
{
}

public record KeyValuePairStringProperty(string Key, string Value) : IProperty;

internal sealed record SerializableKeyValuePairStringProperty(string Key, string Value) : KeyValuePairStringProperty(Key, Value);

internal sealed record SerializableNamedKeyValuePairsStringProperty(string Name, KeyValuePair<string, string>[] Pairs) : IProperty;

internal sealed record SerializableNamedArrayStringProperty(string Name, string[] Values) : IProperty;

public abstract record TestNodeStateProperty(string? Explanation) : IProperty
{
    protected TestNodeStateProperty()
        : this(default(string))
    {
    }
}

public sealed record DiscoveredTestNodeStateProperty(string? Explanation = null) : TestNodeStateProperty(Explanation)
{
    public static DiscoveredTestNodeStateProperty CachedInstance { get; } = new DiscoveredTestNodeStateProperty();
}

public sealed record InProgressTestNodeStateProperty(string? Explanation = null) : TestNodeStateProperty(Explanation)
{
    public static InProgressTestNodeStateProperty CachedInstance { get; } = new InProgressTestNodeStateProperty();
}

public sealed record PassedTestNodeStateProperty(string? Explanation = null) : TestNodeStateProperty(Explanation)
{
    public static PassedTestNodeStateProperty CachedInstance { get; } = new PassedTestNodeStateProperty();
}

public sealed record SkippedTestNodeStateProperty(string? Explanation = null) : TestNodeStateProperty(Explanation)
{
    public static SkippedTestNodeStateProperty CachedInstance { get; } = new SkippedTestNodeStateProperty();
}

public sealed record FailedTestNodeStateProperty : TestNodeStateProperty
{
    public FailedTestNodeStateProperty()
        : base()
    {
    }

    public FailedTestNodeStateProperty(string explanation)
        : base(explanation)
    {
    }

    public FailedTestNodeStateProperty(Exception exception, string? explanation = null)
        : base(explanation ?? exception.Message)
    {
        Exception = exception;
    }

    public Exception? Exception { get; }
}

public sealed record ErrorTestNodeStateProperty : TestNodeStateProperty
{
    public ErrorTestNodeStateProperty()
        : base()
    {
    }

    public ErrorTestNodeStateProperty(string explanation)
        : base(explanation)
    {
    }

    public ErrorTestNodeStateProperty(Exception exception, string? explanation = null)
        : base(explanation ?? exception.Message)
    {
        Exception = exception;
    }

    public Exception? Exception { get; }
}

public sealed record TimeoutTestNodeStateProperty : TestNodeStateProperty
{
    public TimeoutTestNodeStateProperty()
        : base()
    {
    }

    public TimeoutTestNodeStateProperty(string explanation)
        : base(explanation)
    {
    }

    public TimeoutTestNodeStateProperty(Exception exception, string? explanation = null)
        : base(explanation ?? exception.Message)
    {
        Exception = exception;
    }

    public Exception? Exception { get; }

    public TimeSpan? Timeout { get; init; }
}

public sealed record CancelledTestNodeStateProperty : TestNodeStateProperty
{
    public CancelledTestNodeStateProperty()
        : base()
    {
    }

    public CancelledTestNodeStateProperty(string explanation)
        : base(explanation)
    {
    }

    public CancelledTestNodeStateProperty(Exception exception, string? explanation = null)
        : base(explanation ?? exception.Message)
    {
        Exception = exception;
    }

    public Exception? Exception { get; }
}

public readonly record struct TimingInfo(DateTimeOffset StartTime, DateTimeOffset EndTime, TimeSpan Duration);

public sealed record StepTimingInfo(string Id, string Description, TimingInfo Timing);

public sealed record TimingProperty : IProperty
{
    public TimingProperty(TimingInfo globalTiming)
        : this(globalTiming, [])
    {
    }

    public TimingProperty(TimingInfo globalTiming, StepTimingInfo[] stepTimings)
    {
        GlobalTiming = globalTiming;
        StepTimings = stepTimings;
    }

    public TimingInfo GlobalTiming { get; }

    public StepTimingInfo[] StepTimings { get; }
}

public record struct LinePosition(int Line, int Column);

public record struct LinePositionSpan(LinePosition Start, LinePosition End);

public abstract record FileLocationProperty(string FilePath, LinePositionSpan LineSpan) : IProperty;

public sealed record TestFileLocationProperty(string FilePath, LinePositionSpan LineSpan) : FileLocationProperty(FilePath, LineSpan);

public sealed record TestMethodIdentifierProperty(string AssemblyFullName, string Namespace, string TypeName, string MethodName, string[] ParameterTypeFullNames, string ReturnTypeFullName) : IProperty;

public sealed record TestMetadataProperty(string Key, string Value) : IProperty;
