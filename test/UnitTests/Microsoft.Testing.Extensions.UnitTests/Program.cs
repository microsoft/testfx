// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 0)]

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);

builder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);
#if ENABLE_CODECOVERAGE
builder.AddCodeCoverageProvider();
#endif
builder.AddHangDumpProvider();
builder.AddCrashDumpProvider(ignoreIfNotSupported: true);
builder.AddTrxReportProvider();
builder.AddJUnitReportProvider();
builder.AddAppInsightsTelemetryProvider();
builder.AddAzureDevOpsProvider();
builder.AddCtrfReportProvider();

// Dogfood the OpenTelemetry extension: subscribe to the Microsoft.Testing.Platform activity source
// and meter so the OpenTelemetryResultHandler pipeline is exercised end-to-end in CI. No exporter
// is registered, so the recorded data is dropped by the SDK at the export stage — this keeps CI
// logs clean while still flowing every test event through the OTel pipeline.
builder.AddOpenTelemetryProvider(
    tracing => tracing.AddTestingPlatformInstrumentation(),
    metrics => metrics.AddTestingPlatformInstrumentation());

ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();
