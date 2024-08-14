﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal sealed class TestModule
{
    public TestModule(int slotIndex, string assembly, string? targetFramework, string? architecture, IStopwatch stopwatch)
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

    public IStopwatch Stopwatch { get; }

    public List<string> Attachments { get; } = new();

    public List<IProgressMessage> Messages { get; } = new();

    public int FailedTests { get; internal set; }

    public int PassedTests { get; internal set; }

    public int SkippedTests { get; internal set; }

    public int TotalTests { get; internal set; }

    public int TimedOutTests { get; internal set; }

    public int CanceledTests { get; internal set; }

    internal void AddError(string text)
        => Messages.Add(new ErrorMessage(text));

    internal void AddWarning(string text)
        => Messages.Add(new WarningMessage(text));
}