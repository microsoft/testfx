// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions;

[assembly: DoNotParallelize]

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);

#if ENABLE_CODECOVERAGE
builder.AddCodeCoverageProvider();
#endif
builder.AddTrxReportProvider();
builder.AddHangDumpProvider();
builder.AddCrashDumpProvider(ignoreIfNotSupported: true);
builder.AddRetryProvider();
builder.AddAzureDevOpsProvider();

builder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);

ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();
