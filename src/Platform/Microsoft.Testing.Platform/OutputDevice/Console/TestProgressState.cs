// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Console;

internal class TestProgressState
{
    public TestProgressState(int passed, int failed, int skipped, string assemblyName, string? targetFramework, string? architecture, StopwatchAbstraction stopwatch, string? detail)
    {
        Passed = passed;
        Failed = failed;
        Skipped = skipped;
        AssemblyName = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));
        TargetFramework = targetFramework ?? throw new ArgumentNullException(nameof(targetFramework));
        Architecture = architecture ?? throw new ArgumentNullException(nameof(architecture));
        Stopwatch = stopwatch ?? throw new ArgumentNullException(nameof(stopwatch));
        Detail = detail;
    }

    public int Passed { get; }

    public int Failed { get; }

    public int Skipped { get; }

    public string AssemblyName { get; }

    public string TargetFramework { get; }

    public string Architecture { get; }

    public StopwatchAbstraction Stopwatch { get; }

    public string? Detail { get; }
}
