// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.Testing.Platform.UI;

internal sealed class SystemStopwatch : StopwatchAbstraction
{
    private readonly Stopwatch _stopwatch = new();

    public override TimeSpan Elapsed => _stopwatch.Elapsed;

    public override void Start() => _stopwatch.Start();

    public override void Stop() => _stopwatch.Stop();

    public static StopwatchAbstraction StartNew()
    {
        SystemStopwatch wallClockStopwatch = new();
        wallClockStopwatch.Start();

        return wallClockStopwatch;
    }
}
