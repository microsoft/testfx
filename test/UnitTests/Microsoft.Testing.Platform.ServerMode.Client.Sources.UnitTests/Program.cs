// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Sockets;
using System.Reflection;

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 0)]

if (args is ["--server", "--client-port", string port, "--no-banner"]
    && Environment.GetEnvironmentVariable(MtpServerProcessTests.StandardOutputFloodEnvironmentVariable) == "1")
{
    // Fill the redirected pipe before connecting, without shell overhead or a descendant that can outlive shutdown.
    Console.WriteLine(new string('x', 1024 * 1024));
    using var client = new TcpClient("127.0.0.1", int.Parse(port, CultureInfo.InvariantCulture));
    await Task.Delay(Timeout.InfiniteTimeSpan);
    return 0;
}

// Opt-out telemetry.
Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddAffectedTestsProvider();

builder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);

// Register the same platform extensions every other testfx unit-test app registers. CI runs each test
// module through 'dotnet test --test-modules' with --crashdump/--hangdump/--report-trx/--report-ctrf/
// --report-junit/--report-azdo/--coverage appended by test/Directory.Build.targets. A provider must be
// registered for each of those options, otherwise the module rejects the unknown option and exits 5
// ("zero tests ran"), which is exactly what happened before these registrations were added.
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
