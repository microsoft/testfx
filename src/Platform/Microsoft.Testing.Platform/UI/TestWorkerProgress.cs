// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UI;

internal class TestWorkerProgress
{
    public TestWorkerProgress(int tests, int passed, int failed, int skipped, string assemblyName, string? targetFramework, string? architecture, StopwatchAbstraction stopwatch, string? detail)
    {
        Tests = tests;
        Passed = passed;
        Failed = failed;
        Skipped = skipped;
        AssemblyName = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));
        TargetFramework = targetFramework ?? throw new ArgumentNullException(nameof(targetFramework));
        Architecture = architecture ?? throw new ArgumentNullException(nameof(architecture));
        Stopwatch = stopwatch ?? throw new ArgumentNullException(nameof(stopwatch));
        Detail = detail;
    }

    public int Tests { get; internal set; }

    public int Passed { get; internal set; }

    public int Failed { get; internal set; }

    public int Skipped { get; internal set; }

    public string AssemblyName { get; internal set; }

    public string TargetFramework { get; internal set; }

    public string Architecture { get; internal set; }

    public StopwatchAbstraction Stopwatch { get; internal set; }

    public string? Detail { get; internal set; }
}
