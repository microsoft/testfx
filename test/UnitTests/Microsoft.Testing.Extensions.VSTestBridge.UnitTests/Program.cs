// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Extensions.VSTestBridge.UnitTests;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Services;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.TestHost.AddTestApplicationLifecycleCallbacks(sp => new GlobalTasks(sp.GetCommandLineOptions()));
builder.AddTestFramework(new SourceGeneratedTestNodesBuilder());

#if NETCOREAPP
Console.WriteLine("Dynamic code supported: " + System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported);
#endif

#if !NATIVE_AOT
#if ENABLE_CODECOVERAGE
builder.AddCodeCoverageProvider();
#endif
builder.AddAppInsightsTelemetryProvider();
builder.AddHangDumpProvider();
Console.WriteLine("NATIVE_AOT disabled");
#else
Console.WriteLine("NATIVE_AOT enabled");
#endif

builder.AddCrashDumpProvider(ignoreIfNotSupported: true);
builder.AddTrxReportProvider();

// Custom suite tools
CompositeExtensionFactory<SlowestTestsConsumer> slowestTestCompositeServiceFactory
    = new(_ => new SlowestTestsConsumer());
builder.TestHost.AddDataConsumer(slowestTestCompositeServiceFactory);
builder.TestHost.AddTestSessionLifetimeHandle(slowestTestCompositeServiceFactory);
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

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

    public async Task AfterRunAsync(int returnValue, CancellationToken _)
    {
        // Check if any tests are missing that were supposed to run.
        TestsRunWatchDog.BaselineFile = Path.Combine(AppContext.BaseDirectory, "testsbaseline.txt");
        await TestsRunWatchDog.VerifyAsync(skip: _commandLineOptions.IsServerMode() || true, fixBaseLine: true);
    }

    public Task BeforeRunAsync(CancellationToken _) => Task.CompletedTask;
}
