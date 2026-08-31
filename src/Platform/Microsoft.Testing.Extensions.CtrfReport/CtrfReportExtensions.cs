// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.CtrfReport;
using Microsoft.Testing.Extensions.CtrfReport.Resources;
using Microsoft.Testing.Platform.Builder;

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
    {
        if (builder is not IArtifactPostProcessingApplicationBuilder artifactPostProcessingBuilder)
        {
            throw new InvalidOperationException(ExtensionResources.InvalidTestApplicationBuilderType);
        }

        ReportProviderRegistration.AddReportProvider<CtrfReportGenerator, CtrfReport.CapturedTestResult>(
            builder,
            ExtensionResources.InvalidTestApplicationBuilderType,
            CtrfReportGeneratorCommandLine.CtrfReportOptionName,
            CtrfReportGenerator.JournalEnvironmentVariableName,
            () => new CtrfReportGeneratorCommandLine(),
            serviceProvider => new CtrfReportGenerator(serviceProvider),
            (serviceProvider, metadata) => new CtrfReportGenerator(serviceProvider, metadata),
            CtrfReportGenerator.DeserializeJournalRecord);

        artifactPostProcessingBuilder.ArtifactPostProcessing.AddArtifactPostProcessor(_ => new CtrfArtifactPostProcessor());
    }
}
