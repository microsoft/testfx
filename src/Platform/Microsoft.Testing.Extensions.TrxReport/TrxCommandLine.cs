// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Resources;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class TrxReportGeneratorCommandLine : ReportGeneratorCommandLineBase
{
    public const string TrxReportOptionName = "report-trx";
    public const string TrxReportFileNameOptionName = "report-trx-filename";

    public TrxReportGeneratorCommandLine()
        : base(
            // Stable extension UID. Do not change: it feeds telemetry, --info output, and artifact metadata.
            "TrxReportGeneratorCommandLine",
            ExtensionResources.TrxReportGeneratorDisplayName,
            ExtensionResources.TrxReportGeneratorDescription,
            TrxReportOptionName,
            TrxReportFileNameOptionName,
            ExtensionResources.TrxReportOptionDescription,
            ExtensionResources.TrxReportFileNameOptionDescription,
            ".trx",
            ExtensionResources.TrxReportFileNameMustNotBeEmpty,
            ExtensionResources.TrxReportFileNameExtensionIsNotTrx,
            ExtensionResources.TrxReportFileNameRelativePathMustStayUnderResultsDirectory,
            ExtensionResources.TrxReportFileNameRequiresTrxReport,
            ExtensionResources.TrxReportIsNotValidForDiscovery)
    {
    }
}
