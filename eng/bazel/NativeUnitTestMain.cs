// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

global using Microsoft.Testing.Platform.Builder;
global using Microsoft.Testing.Platform.Extensions;
global using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Testing.Extensions;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

#if TESTFX_CLASS_PARALLEL
[assembly: Parallelize(Scope = ExecutionScope.ClassLevel, Workers = 0)]
#else
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 0)]
#endif

string? resultsDirectory = Environment.GetEnvironmentVariable("TEST_UNDECLARED_OUTPUTS_DIR");
if (resultsDirectory is not null && !args.Contains("--results-directory", StringComparer.Ordinal))
{
    args = [.. args, "--results-directory", resultsDirectory];
    if (!args.Contains("--report-trx", StringComparer.Ordinal))
    {
        args = [.. args, "--report-trx", "--report-trx-filename", "tests.trx"];
    }
}

Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
#if TESTFX_ISOLATED_NUGET
string? testTempDirectory = Environment.GetEnvironmentVariable("TEST_TMPDIR");
if (testTempDirectory is not null)
{
    // Roslyn's testing library also caches reference packs under Path.GetTempPath()/test-packages.
    Environment.SetEnvironmentVariable("TEMP", testTempDirectory);
    Environment.SetEnvironmentVariable("TMP", testTempDirectory);
    Environment.SetEnvironmentVariable("TMPDIR", testTempDirectory);
    Environment.SetEnvironmentVariable("NUGET_PACKAGES", Path.Combine(testTempDirectory, "nuget"));
    Environment.SetEnvironmentVariable("NUGET_HTTP_CACHE_PATH", Path.Combine(testTempDirectory, "nuget-http"));
    string home = Directory.CreateDirectory(Path.Combine(testTempDirectory, "home")).FullName;
    Environment.SetEnvironmentVariable("HOME", home);
    Environment.SetEnvironmentVariable("USERPROFILE", home);
    Environment.SetEnvironmentVariable("APPDATA", Directory.CreateDirectory(Path.Combine(home, "roaming")).FullName);
    Environment.SetEnvironmentVariable("LOCALAPPDATA", Directory.CreateDirectory(Path.Combine(home, "local")).FullName);
}
#endif
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddMSTest(() => [Assembly.GetEntryAssembly()!]);
#if TESTFX_CRASHDUMP_IGNORE_UNSUPPORTED
builder.AddCrashDumpProvider(ignoreIfNotSupported: true);
#else
builder.AddCrashDumpProvider();
#endif
if (!OperatingSystem.IsBrowser())
{
    builder.AddHangDumpProvider();
}

builder.AddTrxReportProvider();
builder.AddJUnitReportProvider();
builder.AddAzureDevOpsProvider();
builder.AddCtrfReportProvider();
#if TESTFX_TELEMETRY
builder.AddAppInsightsTelemetryProvider();
#endif
builder.AddOpenTelemetryProvider(
    tracing => tracing.AddTestingPlatformInstrumentation(),
    metrics => metrics.AddTestingPlatformInstrumentation());

using ITestApplication application = await builder.BuildAsync();
return await application.RunAsync();
