// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Framework;

/// <summary>
/// Keeps track of time and duration of a test.
/// </summary>
internal sealed class TimeSheet
{
    private readonly Stopwatch _stopwatch;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeSheet"/> class.
    /// Creates new instance of this class, starts measuring time in queue.
    /// </summary>
    public TimeSheet(IClock clock)
    {
        _stopwatch = Stopwatch.StartNew();
        _clock = clock;
    }

    /// <summary>
    /// Gets when the test started in UTC. Not-precise, because we just capture the current DateTimeUtc.
    /// </summary>
    public DateTimeOffset StartTime { get; private set; }

    /// <summary>
    /// Gets when the test stopped in UTC. Not-precise, because we just capture the current DateTimeUtc.
    /// </summary>
    public DateTimeOffset StopTime { get; private set; }

    /// <summary>
    /// Gets how long we've spent in queue before being executed. Precise, measured by Stopwatch.
    /// </summary>
    public TimeSpan DurationInQueue { get; private set; }

    /// <summary>
    /// Gets how long we've spent executing the test. Precise, measured by Stopwatch.
    /// </summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>
    /// Record the start of the test, this will capture time spent in queue and start measuring duration of test.
    /// </summary>
    internal void RecordStart()
    {
        StartTime = _clock.UtcNow;
        DurationInQueue = _stopwatch.Elapsed;
        _stopwatch.Restart();
    }

    /// <summary>
    /// Record the end of the test, this will capture time spent executing the test.
    /// </summary>
    internal void RecordStop()
    {
        StopTime = _clock.UtcNow;
        Duration = _stopwatch.Elapsed;
    }
}
