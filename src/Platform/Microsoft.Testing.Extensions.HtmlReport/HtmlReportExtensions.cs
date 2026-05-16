// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.HtmlReport;
using Microsoft.Testing.Extensions.HtmlReport.Resources;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Provides extension methods for adding HTML report generation to a test application.
/// </summary>
public static class HtmlReportExtensions
{
    /// <summary>
    /// Adds HTML report generation to a test application.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    public static void AddHtmlReportProvider(this ITestApplicationBuilder builder)
    {
        if (builder is not TestApplicationBuilder)
        {
            throw new InvalidOperationException(ExtensionResources.InvalidTestApplicationBuilderType);
        }

        var commandLine = new HtmlReportGeneratorCommandLine();

        var compositeHtmlReportGenerator =
            new CompositeExtensionFactory<HtmlReportGenerator>(serviceProvider =>
                new HtmlReportGenerator(
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
                    serviceProvider.GetLoggerFactory().CreateLogger<HtmlReportGenerator>()));

        builder.TestHost.AddDataConsumer(compositeHtmlReportGenerator);
        builder.TestHost.AddTestSessionLifetimeHandler(compositeHtmlReportGenerator);

        builder.CommandLine.AddProvider(() => commandLine);
    }
}
