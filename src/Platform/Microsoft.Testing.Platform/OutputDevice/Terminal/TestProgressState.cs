// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[Embedded]
internal sealed class TestProgressState
{
    public TestProgressState(long id, string assembly, string? targetFramework, string? architecture, IStopwatch stopwatch, bool isDiscovery)
    {
        Id = id;
        TargetFramework = targetFramework;
        Architecture = architecture;
        Stopwatch = stopwatch;
        Assembly = assembly;
        AssemblyName = Path.GetFileName(assembly);
        IsDiscovery = isDiscovery;
    }

    /// <summary>Gets the assembly path or display name as provided by the caller (used for the summary link).</summary>
    public string Assembly { get; }

    public string AssemblyName { get; }

    public string? TargetFramework { get; }

    public string? Architecture { get; }

    public IStopwatch Stopwatch { get; }

    public int DiscoveredTests { get; internal set; }

    public int FailedTests { get; internal set; }

    public int PassedTests { get; internal set; }

    public int SkippedTests { get; internal set; }

    /// <summary>Gets or sets the number of tests whose final result came from a retry (orchestrator retry runs); rendered as the "/r{N}" segment.</summary>
    public int RetriedFailedTests { get; internal set; }

    public int TotalTests { get; internal set; }

    public TestNodeResultsState? TestNodeResultsState { get; internal set; }

    public int SlotIndex { get; internal set; }

    public long Id { get; internal set; }

    public long Version { get; internal set; }

    public List<string> DiscoveredTestDisplayNames { get; internal set; } = [];

    public bool IsDiscovery { get; }

    /// <summary>Gets or sets a value indicating whether the assembly run completed successfully (set by the orchestrator on completion).</summary>
    public bool Success { get; internal set; }
}
