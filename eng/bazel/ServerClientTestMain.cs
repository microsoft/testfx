// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

global using Microsoft.Testing.Platform.Builder;
global using Microsoft.Testing.Platform.Extensions;
global using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Net.Sockets;

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 0)]

// Keep the existing self-host fixtures ahead of Bazel's results-directory arguments.
if (args is ["--server", "--client-port", string port, "--no-banner"])
{
    if (Environment.GetEnvironmentVariable(MtpServerProcessTests.NaturalExitReleaseFileEnvironmentVariable) is { } releaseFile)
    {
        using var client = new TcpClient("127.0.0.1", int.Parse(port, CultureInfo.InvariantCulture));
        while (!File.Exists(releaseFile))
        {
            await Task.Delay(10);
        }

        return 23;
    }

    if (Environment.GetEnvironmentVariable(MtpServerProcessTests.StandardOutputFloodEnvironmentVariable) == "1")
    {
        Console.WriteLine(new string('x', 1024 * 1024));
        using var client = new TcpClient("127.0.0.1", int.Parse(port, CultureInfo.InvariantCulture));
        await Task.Delay(Timeout.InfiniteTimeSpan);
        return 0;
    }

    if (Environment.GetEnvironmentVariable(MtpServerProcessTests.StartupStandardErrorEnvironmentVariable) == "1")
    {
        for (int i = 0; i < 8192; i++)
        {
            Console.Error.WriteLine(MtpServerProcessTests.StartupStandardError);
        }

        return 17;
    }
}

string? resultsDirectory = Environment.GetEnvironmentVariable("TEST_UNDECLARED_OUTPUTS_DIR");
if (resultsDirectory is not null
    && !args.Contains("--server", StringComparer.Ordinal)
    && !args.Contains("--results-directory", StringComparer.Ordinal))
{
    args = [.. args, "--results-directory", resultsDirectory];
    if (!args.Contains("--report-trx", StringComparer.Ordinal))
    {
        args = [.. args, "--report-trx", "--report-trx-filename", "tests.trx"];
    }
}

Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);
builder.AddCrashDumpProvider();
if (!OperatingSystem.IsBrowser())
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

using ITestApplication application = await builder.BuildAsync();
return await application.RunAsync();
