
namespace Microsoft.Testing.Platform.UI;

internal sealed class ProjectStartedEventArgs
{

}

internal sealed class ProjectFinishedEventArgs
{
    public BuildEventContext BuildEventContext { get; internal set; }

    public string ProjectFile { get; internal set; }

    public bool Succeeded { get; internal set; }
}

internal sealed class BuildWarningEventArgs
{

}

internal sealed class BuildErrorEventArgs
{
}
