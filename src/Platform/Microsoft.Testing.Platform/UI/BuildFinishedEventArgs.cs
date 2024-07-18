
namespace Microsoft.Testing.Platform.UI;


internal sealed class BuildFinishedEventArgs
{
    public TimeSpan Timestamp { get; internal set; }
    public bool Succeeded { get; internal set; }
}
