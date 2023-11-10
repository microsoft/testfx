// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

public interface IProperty
{
}

public class KeyValuePairStringProperty(string key, string value) : IProperty
{
    public string Key { get; } = key;

    public string Value { get; } = value;
}

internal sealed class SerializableKeyValuePairStringProperty(string key, string value) : KeyValuePairStringProperty(key, value);

internal sealed class SerializableNamedKeyValuePairsStringProperty(string name, KeyValuePair<string, string>[] pairs) : IProperty
{
    public string Name { get; } = name;

    public KeyValuePair<string, string>[] Pairs { get; } = pairs;
}

internal sealed class SerializableNamedArrayStringProperty(string name, string[] values) : IProperty
{
    public string Name { get; } = name;

    public string[] Values { get; } = values;
}

public abstract class TestNodeStateProperty(string? explanation) : IProperty
{
    protected TestNodeStateProperty()
        : this(default)
    {
    }

    public string? Explanation { get; } = explanation;
}

public sealed class DiscoveredTestNodeStateProperty(string? explanation = null) : TestNodeStateProperty(explanation)
{
    public static DiscoveredTestNodeStateProperty CachedInstance { get; } = new DiscoveredTestNodeStateProperty();
}

public sealed class InProgressTestNodeStateProperty(string? explanation = null) : TestNodeStateProperty(explanation)
{
    public static InProgressTestNodeStateProperty CachedInstance { get; } = new InProgressTestNodeStateProperty();
}

public sealed class PassedTestNodeStateProperty(string? explanation = null) : TestNodeStateProperty(explanation)
{
    public static PassedTestNodeStateProperty CachedInstance { get; } = new PassedTestNodeStateProperty();
}

public sealed class SkippedTestNodeStateProperty(string? explanation = null) : TestNodeStateProperty(explanation)
{
    public static SkippedTestNodeStateProperty CachedInstance { get; } = new SkippedTestNodeStateProperty();
}

public sealed class FailedTestNodeStateProperty : TestNodeStateProperty
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

public sealed class ErrorTestNodeStateProperty : TestNodeStateProperty
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

public sealed class TimeoutTestNodeStateProperty : TestNodeStateProperty
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

public sealed class CancelledTestNodeStateProperty : TestNodeStateProperty
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

public readonly struct TimingInfo(DateTimeOffset startTime, DateTimeOffset endTime, TimeSpan duration)
{
    public DateTimeOffset StartTime { get; } = startTime;

    public DateTimeOffset EndTime { get; } = endTime;

    public TimeSpan Duration { get; } = duration;
}

public sealed class StepTimingInfo(string id, string description, TimingInfo timing)
{
    public string Id { get; } = id;

    public string Description { get; } = description;

    public TimingInfo Timing { get; } = timing;
}

public sealed class TimingProperty : IProperty
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

public readonly struct LinePosition(int line, int column)
{
    public int Line { get; } = line;

    public int Column { get; } = column;
}

public readonly struct LinePositionSpan(LinePosition start, LinePosition end)
{
    public LinePosition Start { get; } = start;

    public LinePosition End { get; } = end;
}

public abstract class FileLocationProperty(string filePath, LinePositionSpan lineSpan) : IProperty
{
    public string FilePath { get; } = filePath;

    public LinePositionSpan LineSpan { get; } = lineSpan;
}

public sealed class TestFileLocationProperty(string filePath, LinePositionSpan lineSpan) : FileLocationProperty(filePath, lineSpan);

public sealed class TestMethodIdentifierProperty(string assemblyFullName, string @namespace, string typeName, string methodName, string[] parameterTypeFullNames, string returnTypeFullName) : IProperty
{
    public string AssemblyFullName { get; } = assemblyFullName;

    public string Namespace { get; } = @namespace;

    public string TypeName { get; } = typeName;

    public string MethodName { get; } = methodName;

    public string[] ParameterTypeFullNames { get; } = parameterTypeFullNames;

    public string ReturnTypeFullName { get; } = returnTypeFullName;
}

public sealed class TestMetadataProperty(string key, string value) : IProperty
{
    public string Key { get; } = key;

    public string Value { get; } = value;
}
