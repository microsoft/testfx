// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.JUnitReport.Resources;

namespace Microsoft.Testing.Extensions.JUnitReport;

internal sealed class JUnitReportGeneratorCommandLine : ReportGeneratorCommandLineBase
{
    public const string JUnitReportOptionName = "report-junit";
    public const string JUnitReportFileNameOptionName = "report-junit-filename";

    public JUnitReportGeneratorCommandLine()
        : base(
            // Stable extension UID. Do not change: it feeds telemetry, --info output, and artifact metadata.
            "JUnitReportGeneratorCommandLine",
            ExtensionResources.JUnitReportGeneratorDisplayName,
            ExtensionResources.JUnitReportGeneratorDescription,
            JUnitReportOptionName,
            JUnitReportFileNameOptionName,
            ExtensionResources.JUnitReportOptionDescription,
            ExtensionResources.JUnitReportFileNameOptionDescription,
            ".xml",
            ExtensionResources.JUnitReportFileNameMustNotBeEmpty,
            ExtensionResources.JUnitReportFileNameExtensionIsNotXml,
            ExtensionResources.JUnitReportFileNameRelativePathMustStayUnderResultsDirectory,
            ExtensionResources.JUnitReportFileNameRequiresJUnitReport,
            ExtensionResources.JUnitReportIsNotValidForDiscovery)
    {
    }
}
