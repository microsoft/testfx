// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

global using Microsoft.Testing.Platform.Builder;
global using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Testing.Extensions;

#if TESTFX_INTERNAL_TESTS
using TestFramework.ForTestingMSTest;
#endif
#if TESTFX_PLATFORM_SERVICES_PROBES
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;
#endif

#if TESTFX_FOUR_WORKERS
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 4)]
#elif TESTFX_METHOD_PARALLEL
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 0)]
#endif

#if TESTFX_PLATFORM_SERVICES_PROBES
if (Environment.GetEnvironmentVariable(DebugAssertionChildProbe.ModeVariable) is string probeMode)
{
    DebugAssertionChildProbe.ConfigureCrashReporting();
    try
    {
        switch (probeMode)
        {
            case DebugAssertionChildProbe.MalformedReflectionMode:
                await Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TestMethodInfoTests.RunMalformedReflectionProbeAsync();
                break;
            case DebugAssertionChildProbe.NullPdbSourceMode:
                MSTestAdapter.PlatformServices.UnitTests.Utilities.FileUtilityContinuationTests.RunNullPdbSourceProbe();
                break;
            default:
                throw new InvalidOperationException("Unknown owned Debug assertion probe.");
        }

        Console.WriteLine(DebugAssertionChildProbe.ReturnedMarker);
    }
    finally
    {
        Console.WriteLine(DebugAssertionChildProbe.FinallyMarker);
    }

    return 0;
}
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
#if TESTFX_TOOLCHAIN_ON_PATH
string dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT")
    ?? throw new InvalidOperationException("The native sample requires the declared .NET toolchain.");
Environment.SetEnvironmentVariable("PATH", dotnetRoot + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
#endif
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
#if TESTFX_INTERNAL_TESTS
builder.AddInternalTestFramework();
#else
builder.AddMSTest(() => [Assembly.GetExecutingAssembly()]);
#endif
builder.AddTrxReportProvider();
#if TESTFX_REPORTS
builder.AddHtmlReportProvider();
builder.AddJUnitReportProvider();
builder.AddCtrfReportProvider();
#endif
using ITestApplication application = await builder.BuildAsync();
return await application.RunAsync();
