// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Internal.Framework.Configurations;

// Opt-out telemetry
Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");

// DebuggerUtility.AttachVSToCurrentProcess();
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);

builder.AddTestFramework(new TestFrameworkConfiguration(Debugger.IsAttached ? 1 : Environment.ProcessorCount), new Microsoft.Testing.Platform.UnitTests.SourceGeneratedTestNodesBuilder());
#if ENABLE_CODECOVERAGE
builder.AddCodeCoverageProvider();
#endif
builder.AddCrashDumpProvider();
builder.AddHangDumpProvider();
builder.AddTrxReportProvider();

// Custom suite tools
CompositeExtensionFactory<SlowestTestsConsumer> slowestTestCompositeServiceFactory = new(_ => new SlowestTestsConsumer());
builder.TestHost.AddDataConsumer(slowestTestCompositeServiceFactory);
builder.TestHost.AddTestSessionLifetimeHandle(slowestTestCompositeServiceFactory);
ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();
