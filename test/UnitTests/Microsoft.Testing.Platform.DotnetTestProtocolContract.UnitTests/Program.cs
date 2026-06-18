// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 0)]

// Opt-out telemetry
Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);

builder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);

#if ENABLE_CODECOVERAGE
builder.AddCodeCoverageProvider();
#endif
builder.AddCrashDumpProvider();

#if !NETFRAMEWORK
if (!OperatingSystem.IsBrowser())
#endif
{
    builder.AddHangDumpProvider();
}

builder.AddTrxReportProvider();
builder.AddJUnitReportProvider();
builder.AddAzureDevOpsProvider();
builder.AddCtrfReportProvider();

builder.AddOpenTelemetryProvider(
    tracing => tracing.AddTestingPlatformInstrumentation(),
    metrics => metrics.AddTestingPlatformInstrumentation());

ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();
