namespace Microsoft.Testing.Platform.UI;


internal sealed class TestRunStartedUpdate(int workerCount)
{
    public int WorkerCount { get; internal set; } = workerCount;
}
