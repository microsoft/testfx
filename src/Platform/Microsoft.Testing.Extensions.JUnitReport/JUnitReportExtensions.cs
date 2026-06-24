// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.JUnitReport;
using Microsoft.Testing.Extensions.JUnitReport.Resources;
using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Provides extension methods for adding JUnit XML report generation to a test application.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class JUnitReportExtensions
{
    /// <summary>
    /// Adds JUnit XML report generation to a test application.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    public static void AddJUnitReportProvider(this ITestApplicationBuilder builder)
        => ReportProviderRegistration.AddReportProvider(
            builder,
            ExtensionResources.InvalidTestApplicationBuilderType,
            () => new JUnitReportGeneratorCommandLine(),
            serviceProvider => new JUnitReportGenerator(serviceProvider));
}
