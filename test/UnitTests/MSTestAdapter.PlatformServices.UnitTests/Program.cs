// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

using TestFramework.ForTestingMSTest;

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
#if NETFRAMEWORK
            case DebugAssertionChildProbe.ClosedOleSchemaMode:
                MSTestAdapter.PlatformServices.UnitTests.Data.TestDataConnectionSqlContinuationTests.RunClosedOleSchemaProbe();
                break;
            case DebugAssertionChildProbe.UnsupportedSqlMockMode:
                MSTestAdapter.PlatformServices.UnitTests.Data.TestDataConnectionSqlContinuationTests.RunUnsupportedSqlMockProbe();
                break;
            case DebugAssertionChildProbe.ClosedDefaultSchemaMode:
                MSTestAdapter.PlatformServices.UnitTests.Data.TestDataConnectionSqlContinuationTests.RunClosedDefaultSchemaProbe();
                break;
#endif
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
