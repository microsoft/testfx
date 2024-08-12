// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddTestFramework(new Microsoft.Testing.Extensions.UnitTests.SourceGeneratedTestNodesBuilder());
#if ENABLE_CODECOVERAGE
builder.AddCodeCoverageProvider();
#endif
builder.AddHangDumpProvider();
builder.AddCrashDumpProvider(ignoreIfNotSupported: true);
builder.AddTrxReportProvider();
builder.AddAppInsightsTelemetryProvider();

// Custom suite tools
CompositeExtensionFactory<SlowestTestsConsumer> slowestTestCompositeServiceFactory
    = new(_ => new SlowestTestsConsumer());
builder.TestHost.AddDataConsumer(slowestTestCompositeServiceFactory);
builder.TestHost.AddTestSessionLifetimeHandle(slowestTestCompositeServiceFactory);
ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();
