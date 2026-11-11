// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 0)]

#if NETCOREAPP
Console.Error.WriteLine("Dynamic code supported: " + System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported);
#endif

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddAffectedTestsProvider();

builder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);

#if !NATIVE_AOT
#if ENABLE_CODECOVERAGE
builder.AddCodeCoverageProvider();
#endif
builder.AddAppInsightsTelemetryProvider();
builder.AddHangDumpProvider();
using IDisposable? telemetry = builder.AddTestFxOpenTelemetry();
Console.Error.WriteLine("NATIVE_AOT disabled");
#else
Console.Error.WriteLine("NATIVE_AOT enabled");
#endif

builder.AddCrashDumpProvider(ignoreIfNotSupported: true);
builder.AddTrxReportProvider();
builder.AddJUnitReportProvider();
builder.AddAzureDevOpsProvider();
builder.AddCtrfReportProvider();

using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();
