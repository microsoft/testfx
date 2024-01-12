// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Framework.Configurations;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.CommandLine;
#if ENABLE_CODECOVERAGE
using Microsoft.Testing.Platform.Extensions.CodeCoverage;
#endif
using Microsoft.Testing.Platform.Extensions.TestHost;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.TestHost.AddTestApplicationLifecycleCallbacks(sp => new GlobalTasks(sp.GetCommandLineOptions()));

builder.AddTestFramework(new TestFrameworkConfiguration(Debugger.IsAttached ? 1 : Environment.ProcessorCount), new SourceGeneratedTestNodesBuilder());
#if ENABLE_CODECOVERAGE
builder.AddCodeCoverage();
#endif
builder.AddCrashDumpProvider();
builder.AddTrxReportProvider();

// Custom suite tools
CompositeExtensionFactory<SlowestTestsConsumer> slowestTestCompositeServiceFactory
    = new(_ => new SlowestTestsConsumer());
builder.TestHost.AddDataConsumer(slowestTestCompositeServiceFactory);
builder.TestHost.AddTestSessionLifetimeHandle(slowestTestCompositeServiceFactory);
using ITestApplication app = await builder.BuildAsync();
int returnValue = await app.RunAsync();
Console.WriteLine($"Process started: {CommandLine.TotalProcessesAttempt}");
return returnValue;

internal sealed class GlobalTasks : ITestApplicationLifecycleCallbacks
{
    private readonly ICommandLineOptions _commandLineOptions;

    public GlobalTasks(ICommandLineOptions commandLineOptions)
    {
        _commandLineOptions = commandLineOptions;
    }

    public string Uid => nameof(GlobalTasks);

    public string Version => "1.0.0";

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task AfterRunAsync(int returnValue, CancellationToken cancellationToken)
    {
        // Remove net462 tests from baseline on non-Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string[] allLines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "testsbaseline.txt"));
            using FileStream fs = File.OpenWrite(Path.Combine(AppContext.BaseDirectory, "testsbaseline.notwin.txt"));
            using StreamWriter sw = new(fs);
            foreach (string line in allLines.Where(x => !x.Contains("net462")))
            {
                await sw.WriteLineAsync(line);
            }
        }

        // Verify run tests are matching expected baseline
        TestsRunWatchDog.BaselineFile = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(AppContext.BaseDirectory, "testsbaseline.txt")
            : Path.Combine(AppContext.BaseDirectory, "testsbaseline.notwin.txt");

        await TestsRunWatchDog.Verify(skip: _commandLineOptions.IsServerMode(), fixBaseLine: true);
    }

    public Task BeforeRunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
