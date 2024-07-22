// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UI;

internal sealed class AssemblyRun
{
    public AssemblyRun(int slotIndex, string assembly, string? targetFramework, string? architecture, StopwatchAbstraction stopwatch)
    {
        SlotIndex = slotIndex;
        Assembly = assembly;
        TargetFramework = targetFramework;
        Architecture = architecture;
        Stopwatch = stopwatch;
    }

    public int SlotIndex { get; }

    public string Assembly { get; }

    public string? TargetFramework { get; }

    public string? Architecture { get; }

    public StopwatchAbstraction Stopwatch { get; }

    public List<string> Attachments { get; } = new();

    public List<IMessage> Messages { get; } = new();

    public int FailedTests { get; internal set; }

    public int PassedTests { get; internal set; }

    public int SkippedTests { get; internal set; }

    public int TotalTests { get; internal set; }

    public int TimedOutTests { get; internal set; }

    public int CancelledTests { get; internal set; }

    internal void AddError(string text)
        => Messages.Add(new ErrorMessage(text));

    internal void AddWarning(string text)
        => Messages.Add(new WarningMessage(text));
}
