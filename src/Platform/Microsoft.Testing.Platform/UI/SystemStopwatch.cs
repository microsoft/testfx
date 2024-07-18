
using System.Diagnostics;

namespace Microsoft.Testing.Platform.UI;

internal sealed class SystemStopwatch : StopwatchAbstraction
{
    private readonly Stopwatch _stopwatch = new();

    public override double ElapsedSeconds => _stopwatch.Elapsed.TotalSeconds;

    public override void Start() => _stopwatch.Start();

    public override void Stop() => _stopwatch.Stop();

    public static StopwatchAbstraction StartNew()
    {
        SystemStopwatch wallClockStopwatch = new();
        wallClockStopwatch.Start();

        return wallClockStopwatch;
    }
}
