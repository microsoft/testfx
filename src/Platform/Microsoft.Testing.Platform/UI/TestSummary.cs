namespace Microsoft.Testing.Platform.UI;

internal sealed class TargetFinishedEventArgs
{
    public object BuildEventContext { get; internal set; }
}

internal interface IMessage
{
    MessageSeverity Severity { get; }
    object Message { get; }
}

internal enum MessageSeverity
{
    Error,
    Warning
}

