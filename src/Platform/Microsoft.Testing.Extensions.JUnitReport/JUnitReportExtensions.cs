// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.JUnitReport;
using Microsoft.Testing.Extensions.JUnitReport.Resources;
using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Provides extension methods for adding JUnit XML report generation to a test application.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class JUnitReportExtensions
{
    /// <summary>
    /// Adds JUnit XML report generation to a test application.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    public static void AddJUnitReportProvider(this ITestApplicationBuilder builder)
    {
        if (builder is not IArtifactPostProcessingApplicationBuilder artifactPostProcessingBuilder)
        {
            throw new InvalidOperationException(ExtensionResources.JUnitReportRequiresArtifactPostProcessing);
        }

        ReportProviderRegistration.AddReportProvider<JUnitReportGenerator, JUnitReport.CapturedTestResult>(
            builder,
            ExtensionResources.InvalidTestApplicationBuilderType,
            JUnitReportGeneratorCommandLine.JUnitReportOptionName,
            JUnitReportGenerator.JournalEnvironmentVariableName,
            () => new JUnitReportGeneratorCommandLine(),
            serviceProvider => new JUnitReportGenerator(serviceProvider),
            (serviceProvider, metadata) => new JUnitReportGenerator(serviceProvider, metadata),
            JUnitReportGenerator.DeserializeJournalRecord);

        artifactPostProcessingBuilder.ArtifactPostProcessing.AddArtifactPostProcessor(_ => new JUnitArtifactPostProcessor());
    }
}
