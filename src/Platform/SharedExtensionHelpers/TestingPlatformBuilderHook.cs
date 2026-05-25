// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;

#if TESTINGPLATFORMBUILDERHOOK_AZUREDEVOPSREPORT
namespace Microsoft.Testing.Extensions.AzureDevOpsReport;
#elif TESTINGPLATFORMBUILDERHOOK_AZUREFOUNDRY
namespace Microsoft.Testing.Extensions.AzureFoundry;
#elif TESTINGPLATFORMBUILDERHOOK_CRASHDUMP
namespace Microsoft.Testing.Extensions.CrashDump;
#elif TESTINGPLATFORMBUILDERHOOK_HANGDUMP
namespace Microsoft.Testing.Extensions.HangDump;
#elif TESTINGPLATFORMBUILDERHOOK_HOTRELOAD
namespace Microsoft.Testing.Extensions.HotReload;
#elif TESTINGPLATFORMBUILDERHOOK_HTMLREPORT
namespace Microsoft.Testing.Extensions.HtmlReport;
#elif TESTINGPLATFORMBUILDERHOOK_MSBUILD
namespace Microsoft.Testing.Platform.MSBuild;
#elif TESTINGPLATFORMBUILDERHOOK_RETRY
namespace Microsoft.Testing.Extensions.Retry;
#elif TESTINGPLATFORMBUILDERHOOK_TELEMETRY
namespace Microsoft.Testing.Extensions.Telemetry;
#elif TESTINGPLATFORMBUILDERHOOK_TRXREPORT
namespace Microsoft.Testing.Extensions.TrxReport;
#else
#error "No valid TESTINGPLATFORMBUILDERHOOK_* compilation symbol defined. Check Directory.Build.props for supported symbols."
#endif

/// <summary>
/// Shared builder hook implementation used by platform extension packages to register
/// extension-specific services with the Testing Platform Builder.
/// The effective namespace, attributes, and registration call are selected with project-specific
/// compilation symbols configured in src/Platform/Directory.Build.props.
/// </summary>
public static class TestingPlatformBuilderHook
{
    /// <summary>
    /// Registers the platform extension determined by the project-specific compilation symbol
    /// with the Testing Platform Builder.
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder.</param>
    /// <param name="_">The command line arguments.</param>
#if TESTINGPLATFORMBUILDERHOOK_HANGDUMP
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
#elif TESTINGPLATFORMBUILDERHOOK_MSBUILD || TESTINGPLATFORMBUILDERHOOK_RETRY
    [UnsupportedOSPlatform("browser")]
#endif
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] _)
#if TESTINGPLATFORMBUILDERHOOK_AZUREDEVOPSREPORT
        => testApplicationBuilder.AddAzureDevOpsProvider();
#elif TESTINGPLATFORMBUILDERHOOK_AZUREFOUNDRY
        => testApplicationBuilder.AddAzureOpenAIChatClientProvider();
#elif TESTINGPLATFORMBUILDERHOOK_CRASHDUMP
        => testApplicationBuilder.AddCrashDumpProvider(ignoreIfNotSupported: true);
#elif TESTINGPLATFORMBUILDERHOOK_HANGDUMP
        => testApplicationBuilder.AddHangDumpProvider();
#elif TESTINGPLATFORMBUILDERHOOK_HOTRELOAD
        => testApplicationBuilder.AddHotReloadProvider();
#elif TESTINGPLATFORMBUILDERHOOK_HTMLREPORT
        => testApplicationBuilder.AddHtmlReportProvider();
#elif TESTINGPLATFORMBUILDERHOOK_MSBUILD
        => testApplicationBuilder.AddMSBuild();
#elif TESTINGPLATFORMBUILDERHOOK_RETRY
        => testApplicationBuilder.AddRetryProvider();
#elif TESTINGPLATFORMBUILDERHOOK_TELEMETRY
        => testApplicationBuilder.AddAppInsightsTelemetryProvider();
#elif TESTINGPLATFORMBUILDERHOOK_TRXREPORT
        => testApplicationBuilder.AddTrxReportProvider();
#else
#error "No valid TESTINGPLATFORMBUILDERHOOK_* compilation symbol defined. Check Directory.Build.props for supported symbols."
#endif
}
