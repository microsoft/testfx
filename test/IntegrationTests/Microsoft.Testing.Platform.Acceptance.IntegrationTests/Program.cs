// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 0)]

// Opt-out telemetry
Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");

CommandLine.MaxOutstandingCommands = Environment.ProcessorCount;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);

builder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);
#if ENABLE_CODECOVERAGE
builder.AddCodeCoverageProvider();
#endif
builder.AddHangDumpProvider();
builder.AddCrashDumpProvider(ignoreIfNotSupported: true);
builder.AddTrxReportProvider();
builder.AddJUnitReportProvider();
builder.AddRetryProvider();
builder.AddAzureDevOpsProvider();

// Dogfood the OpenTelemetry extension: subscribe to the Microsoft.Testing.Platform activity source
// and meter so the OpenTelemetryResultHandler pipeline is exercised end-to-end in CI. No exporter
// is registered, so the recorded data is dropped by the SDK at the export stage — this keeps CI
// logs clean while still flowing every test event through the OTel pipeline.
builder.AddOpenTelemetryProvider(
    tracing => tracing.AddTestingPlatformInstrumentation(),
    metrics => metrics.AddTestingPlatformInstrumentation());

// Custom suite tools
CompositeExtensionFactory<SlowestTestsConsumer> slowestTestCompositeServiceFactory
    = new(_ => new SlowestTestsConsumer());
builder.TestHost.AddDataConsumer(slowestTestCompositeServiceFactory);
builder.TestHost.AddTestSessionLifetimeHandler(slowestTestCompositeServiceFactory);
using ITestApplication app = await builder.BuildAsync();
int returnValue = await app.RunAsync();
Console.WriteLine($"Process started: {CommandLine.TotalProcessesAttempt}");
return returnValue;
