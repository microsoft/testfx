// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.CtrfReport.Resources;

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed class CtrfReportGeneratorCommandLine : ReportGeneratorCommandLineBase
{
    public const string CtrfReportOptionName = "report-ctrf";
    public const string CtrfReportFileNameOptionName = "report-ctrf-filename";

    public CtrfReportGeneratorCommandLine()
        : base(
            // Stable extension UID. Do not change: it feeds telemetry, --info output, and artifact metadata.
            "CtrfReportGeneratorCommandLine",
            ExtensionResources.CtrfReportGeneratorDisplayName,
            ExtensionResources.CtrfReportGeneratorDescription,
            CtrfReportOptionName,
            CtrfReportFileNameOptionName,
            ExtensionResources.CtrfReportOptionDescription,
            ExtensionResources.CtrfReportFileNameOptionDescription,
            ".json",
            ExtensionResources.CtrfReportFileNameMustNotBeEmpty,
            ExtensionResources.CtrfReportFileNameExtensionIsNotJson,
            ExtensionResources.CtrfReportFileNameRelativePathMustStayUnderResultsDirectory,
            ExtensionResources.CtrfReportFileNameRequiresCtrfReport,
            ExtensionResources.CtrfReportIsNotValidForDiscovery)
    {
    }
}
