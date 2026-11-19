// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.HtmlReport.Resources;

namespace Microsoft.Testing.Extensions.HtmlReport;

internal sealed class HtmlReportGeneratorCommandLine : ReportGeneratorCommandLineBase
{
    public const string HtmlReportOptionName = "report-html";
    public const string HtmlReportFileNameOptionName = "report-html-filename";

    public HtmlReportGeneratorCommandLine()
        : base(
            // Stable extension UID. Do not change: it feeds telemetry, --info output, and artifact metadata.
            "HtmlReportGeneratorCommandLine",
            ExtensionResources.HtmlReportGeneratorDisplayName,
            ExtensionResources.HtmlReportGeneratorDescription,
            HtmlReportOptionName,
            HtmlReportFileNameOptionName,
            ExtensionResources.HtmlReportOptionDescription,
            ExtensionResources.HtmlReportFileNameOptionDescription,
            ".html",
            ExtensionResources.HtmlReportFileNameMustNotBeEmpty,
            ExtensionResources.HtmlReportFileNameExtensionIsNotHtml,
            ExtensionResources.HtmlReportFileNameRelativePathMustStayUnderResultsDirectory,
            ExtensionResources.HtmlReportFileNameRequiresHtmlReport,
            ExtensionResources.HtmlReportIsNotValidForDiscovery)
    {
    }
}
