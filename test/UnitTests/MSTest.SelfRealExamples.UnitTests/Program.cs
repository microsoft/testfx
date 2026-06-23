// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

[assembly: Parallelize(Scope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope.ClassLevel, Workers = 0)]

ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);

testApplicationBuilder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);
testApplicationBuilder.AddTrxReportProvider();
testApplicationBuilder.AddJUnitReportProvider();
testApplicationBuilder.AddAppInsightsTelemetryProvider();
testApplicationBuilder.AddCrashDumpProvider();
testApplicationBuilder.AddHangDumpProvider();
#if ENABLE_CODECOVERAGE
testApplicationBuilder.AddCodeCoverageProvider();
#endif

testApplicationBuilder.AddAzureDevOpsProvider();
testApplicationBuilder.AddCtrfReportProvider();

// Dogfood the OpenTelemetry extension: subscribe to the Microsoft.Testing.Platform activity source
// and meter so the OpenTelemetryResultHandler pipeline is exercised end-to-end in CI. No exporter
// is registered, so the recorded data is dropped by the SDK at the export stage — this keeps CI
// logs clean while still flowing every test event through the OTel pipeline.
testApplicationBuilder.AddOpenTelemetryProvider(
    tracing => tracing.AddTestingPlatformInstrumentation(),
    metrics => metrics.AddTestingPlatformInstrumentation());

using ITestApplication testApplication = await testApplicationBuilder.BuildAsync();
return await testApplication.RunAsync();
