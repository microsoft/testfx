// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Internal.Framework.Configurations;

using MSTest.Acceptance.IntegrationTests;

// Opt-out telemetry
Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");

CommandLine.MaxOutstandingCommands = Environment.ProcessorCount;
DotnetCli.DoNotRetry = Debugger.IsAttached;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);

builder.AddTestFramework(
   new TestFrameworkConfiguration(Debugger.IsAttached ? 1 : Environment.ProcessorCount),
   new SourceGeneratedTestNodesBuilder());
#if ENABLE_CODECOVERAGE
builder.AddCodeCoverageProvider();
#endif
builder.AddHangDumpProvider();
builder.AddCrashDumpProvider(ignoreIfNotSupported: true);
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
