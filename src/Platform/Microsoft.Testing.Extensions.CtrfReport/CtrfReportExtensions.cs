// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.CtrfReport;
using Microsoft.Testing.Extensions.CtrfReport.Resources;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Provides extension methods for adding CTRF (Common Test Report Format) report generation to a test application.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class CtrfReportExtensions
{
    /// <summary>
    /// Adds CTRF (Common Test Report Format) report generation to a test application.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    public static void AddCtrfReportProvider(this ITestApplicationBuilder builder)
        => ReportProviderRegistration.AddReportProvider(
            builder,
            ExtensionResources.InvalidTestApplicationBuilderType,
            () => new CtrfReportGeneratorCommandLine(),
            serviceProvider =>
                new CtrfReportGenerator(
                    serviceProvider.GetConfiguration(),
                    serviceProvider.GetCommandLineOptions(),
                    serviceProvider.GetRequiredService<IFileSystem>(),
                    serviceProvider.GetTestApplicationModuleInfo(),
                    serviceProvider.GetMessageBus(),
                    serviceProvider.GetSystemClock(),
                    serviceProvider.GetEnvironment(),
                    serviceProvider.GetOutputDevice(),
                    serviceProvider.GetTestFramework(),
                    serviceProvider.GetTestApplicationProcessExitCode(),
                    serviceProvider.GetLoggerFactory().CreateLogger<CtrfReportGenerator>()));
}
