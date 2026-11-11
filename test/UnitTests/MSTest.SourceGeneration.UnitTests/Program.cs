// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions;

[assembly: Parallelize(Scope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope.MethodLevel, Workers = 4)]

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddAffectedTestsProvider();

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

using IDisposable? telemetry = builder.AddTestFxOpenTelemetry();

using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();
