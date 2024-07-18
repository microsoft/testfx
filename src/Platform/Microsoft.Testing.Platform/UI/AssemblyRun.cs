namespace Microsoft.Testing.Platform.UI;

internal sealed class AssemblyRun
{
    public AssemblyRun(int slotIndex, AssemblyRunStartedUpdate assemblyRunStartedUpdate, StopwatchAbstraction stopwatch)
    {
        SlotIndex = slotIndex;
        AssemblyRunStartedUpdate = assemblyRunStartedUpdate;
        Stopwatch = stopwatch;
    }

    public int SlotIndex { get; }
    public AssemblyRunStartedUpdate AssemblyRunStartedUpdate { get; }
    public StopwatchAbstraction Stopwatch { get; }

    public List<string> Attachments { get; } = new();

    public List<IMessage> Messages { get; } = new();
    public int FailedTests { get; internal set; }
    public int PassedTests { get; internal set; }
    public int SkippedTests { get; internal set; }
    public int TotalTests { get; internal set; }
    public int TimedOutTests { get; internal set; }
    public int CancelledTests { get; internal set; }
}
