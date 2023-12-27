// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MSTest.Performance.Runner.Runner;
using MSTest.Performance.Runner.Scenarios;

using DotnetMuxer = MSTest.Performance.Runner.Runners.DotnetMuxer;

namespace MSTest.Performance.Runner;

internal class EntryPoint
{
    public static int Main(string[] args)
    {
        Console.WriteLine("Microsoft (R) MSTest Performance Profiler Command Line Tool");

        Dictionary<string, object> parametersBag = new();

        Pipeline
            .FirstStep(() => new Scenario1(numberOfClass: 100, methodsPerClass: 100, tfm: "net8.0", executionScope: ExecutionScope.MethodLevel), parametersBag)
            .NextStep(() => new DotnetMuxer())
            .NextStep(() => new PerfviewRunner("/BufferSizeMB:1024 /StackCompression /NoNGenRundown /Merge:False /Zip:False", "Scenario1_PerfView.zip"))
            .NextStep(() => new MoveFiles("*.zip", Path.Combine(Directory.GetCurrentDirectory(), "Results")))
            .NextStep(() => new CleanupDisposable());

        // C:\Program Files\Microsoft Visual Studio\2022\Preview\Team Tools\DiagnosticsHub\Collector\AgentConfigs
        Pipeline
            .FirstStep(() => new Scenario1(numberOfClass: 100, methodsPerClass: 100, tfm: "net8.0", executionScope: ExecutionScope.MethodLevel), parametersBag)
            .NextStep(() => new DotnetMuxer())
            .NextStep(() => new VSDiagnostics("DotNetObjectAllocLow.json", "Scenario1_DotNetObjectAllocBase.zip"))
            .NextStep(() => new MoveFiles("*.zip", Path.Combine(Directory.GetCurrentDirectory(), "Results")))
            .NextStep(() => new CleanupDisposable());

        Pipeline
            .FirstStep(() => new Scenario1(numberOfClass: 100, methodsPerClass: 100, tfm: "net8.0", executionScope: ExecutionScope.MethodLevel), parametersBag)
            .NextStep(() => new DotnetMuxer())
            .NextStep(() => new VSDiagnostics("CpuUsageHigh.json", "Scenario1_CpuUsageLow.zip"))
            .NextStep(() => new MoveFiles("*.zip", Path.Combine(Directory.GetCurrentDirectory(), "Results")))
            .NextStep(() => new CleanupDisposable());

        return 0;
    }
}
