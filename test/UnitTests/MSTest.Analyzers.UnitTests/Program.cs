﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Internal.Framework.Configurations;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.TestInfrastructure;

// DebuggerUtility.AttachVSToCurrentProcess();
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.TestHost.AddTestApplicationLifecycleCallbacks(sp => new GlobalTasks(sp.GetCommandLineOptions()));

builder.AddTestFramework(new TestFrameworkConfiguration(Debugger.IsAttached ? 1 : Environment.ProcessorCount), new MSTest.Analyzers.UnitTests.SourceGeneratedTestNodesBuilder());
#if ENABLE_CODECOVERAGE
builder.AddCodeCoverageProvider();
#endif
builder.AddCrashDumpProvider();
builder.AddTrxReportProvider();

// Custom suite tools
CompositeExtensionFactory<SlowestTestsConsumer> slowestTestCompositeServiceFactory = new(_ => new SlowestTestsConsumer());
builder.TestHost.AddDataConsumer(slowestTestCompositeServiceFactory);
builder.TestHost.AddTestSessionLifetimeHandle(slowestTestCompositeServiceFactory);
ITestApplication app = await builder.BuildAsync();
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

    public async Task AfterRunAsync(int returnValue, CancellationToken cancellationToken)
    {
        // Check if any tests are missing that were supposed to run.
        TestsRunWatchDog.BaselineFile = Path.Combine(AppContext.BaseDirectory, "testsbaseline.txt");
        await TestsRunWatchDog.VerifyAsync(skip: _commandLineOptions.IsServerMode(), fixBaseLine: true);
    }

    public Task BeforeRunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
