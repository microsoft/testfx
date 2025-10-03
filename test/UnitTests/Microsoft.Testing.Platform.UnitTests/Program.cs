// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 0)]

// Opt-out telemetry
Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");

// DebuggerUtility.AttachVSToCurrentProcess();
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);

builder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);

#if ENABLE_CODECOVERAGE
builder.AddCodeCoverageProvider();
#endif
builder.AddCrashDumpProvider();
builder.AddHangDumpProvider();
builder.AddTrxReportProvider();
builder.AddAzureDevOpsProvider();

// Custom suite tools
CompositeExtensionFactory<SlowestTestsConsumer> slowestTestCompositeServiceFactory = new(_ => new SlowestTestsConsumer());
builder.TestHost.AddDataConsumer(slowestTestCompositeServiceFactory);
builder.TestHost.AddTestSessionLifetimeHandle(slowestTestCompositeServiceFactory);
ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();
