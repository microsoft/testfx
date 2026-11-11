// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Sockets;
using System.Reflection;

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 0)]

if (Environment.GetEnvironmentVariable(ClientHelperMutationTests.LateFaultFixtureEnvironmentVariable) is { } operation)
{
    return ClientHelperMutationTests.RunLateFaultFixture(operation);
}

bool isProcessFixture = Environment.GetEnvironmentVariable(MtpServerProcessTests.NaturalExitReleaseFileEnvironmentVariable) is not null
    || Environment.GetEnvironmentVariable(MtpServerProcessTests.StandardOutputFloodEnvironmentVariable) == "1"
    || Environment.GetEnvironmentVariable(MtpServerProcessTests.StartupStandardErrorEnvironmentVariable) == "1";
if (isProcessFixture && args is not ["--server", "--client-port", _, "--no-banner"])
{
    Console.Error.WriteLine("The process fixture requires the complete server-mode arguments.");
    return 17;
}

if (args is ["--server", "--client-port", string port, "--no-banner"])
{
    // A mutation runner may kill the owning test host before its finally block executes.
    // The fixture must not run tests recursively or keep an orphaned child alive indefinitely.
    using var fixtureLifetime = new CancellationTokenSource(TimeSpan.FromMinutes(2));

    // Child fixture for MtpServerProcessTests.ShutdownPreservesNaturalExitCodeAfterDisposingProcess:
    // connect without running tests, then exit naturally with code 23 when the parent releases us.
    if (Environment.GetEnvironmentVariable(MtpServerProcessTests.NaturalExitReleaseFileEnvironmentVariable) is { } releaseFile)
    {
        using var client = new TcpClient("127.0.0.1", int.Parse(port, CultureInfo.InvariantCulture));
        while (!File.Exists(releaseFile))
        {
            await Task.Delay(10, fixtureLifetime.Token);
        }

        return 23;
    }

    if (Environment.GetEnvironmentVariable(MtpServerProcessTests.StandardOutputFloodEnvironmentVariable) == "1")
    {
        // Fill the redirected pipe before connecting, without shell overhead or a descendant that can outlive shutdown.
        Console.WriteLine(new string('x', 1024 * 1024));
        using var client = new TcpClient("127.0.0.1", int.Parse(port, CultureInfo.InvariantCulture));
        await Task.Delay(Timeout.InfiniteTimeSpan, fixtureLifetime.Token);
        return 0;
    }

    if (Environment.GetEnvironmentVariable(MtpServerProcessTests.StartupStandardErrorEnvironmentVariable) == "1")
    {
        // Exceed the pipe capacity with complete lines so the parent must consume stderr before this child
        // can exit. A single muxer diagnostic can instead lose the race against the bounded stderr-drain wait.
        for (int i = 0; i < 8192; i++)
        {
            Console.Error.WriteLine(MtpServerProcessTests.StartupStandardError);
        }

        return 17;
    }
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

using IDisposable? telemetry = builder.AddTestFxOpenTelemetry();

using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();
