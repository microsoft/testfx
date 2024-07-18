namespace Microsoft.Testing.Platform.UI;

internal class TestUpdate
{
    public int Outcome { get; set; }
    public Exception? Error { get; set; }
    public string Name { get; set; }
    public string Assembly { get; internal set; }
    public string TargetFramework { get; internal set; }
    public string Architecture { get; internal set; }
}