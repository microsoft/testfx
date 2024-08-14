// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal class TestProgressState
{
    public TestProgressState(int passed, int failed, int skipped, string assemblyName, string? targetFramework, string? architecture, StopwatchAbstraction stopwatch, string? detail)
    {
        Passed = passed;
        Failed = failed;
        Skipped = skipped;
        AssemblyName = assemblyName;
        TargetFramework = targetFramework;
        Architecture = architecture;
        Stopwatch = stopwatch;
        Detail = detail;
    }

    public int Passed { get; }

    public int Failed { get; }

    public int Skipped { get; }

    public string AssemblyName { get; }

    public string? TargetFramework { get; }

    public string? Architecture { get; }

    public StopwatchAbstraction Stopwatch { get; }

    public string? Detail { get; }
}
