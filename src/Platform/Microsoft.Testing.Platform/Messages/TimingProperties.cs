// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

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
        builder.Append(Duration);
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

        for (int i = 0; i < StepTimings.Length; i++)
        {
            builder.Append(StepTimings[i].ToString());
            if (i < StepTimings.Length - 1)
            {
                builder.Append(", ");
            }
        }

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
