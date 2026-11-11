// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions;

using TestFramework.ForTestingMSTest;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddAffectedTestsProvider();

#if ENABLE_CODECOVERAGE
builder.AddCodeCoverageProvider();
#endif
builder.AddTrxReportProvider();
builder.AddJUnitReportProvider();
builder.AddHangDumpProvider();
builder.AddCrashDumpProvider(ignoreIfNotSupported: true);
builder.AddRetryProvider();
builder.AddAzureDevOpsProvider();
builder.AddCtrfReportProvider();

using IDisposable? telemetry = builder.AddTestFxOpenTelemetry();

builder.AddInternalTestFramework();

using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();
