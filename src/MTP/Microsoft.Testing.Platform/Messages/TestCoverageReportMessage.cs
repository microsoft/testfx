// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// References a coverage report artifact so deep consumers (HTML/UI generators) can parse full-fidelity
/// per-line data that the summary intentionally does not carry.
/// </summary>
public sealed class TestCoverageReportMessage : DataWithSessionUid
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestCoverageReportMessage"/> class.
    /// </summary>
    /// <param name="sessionUid">The session this report belongs to.</param>
    /// <param name="reportPath">The path to the report artifact.</param>
    /// <param name="format">The on-disk format of the report.</param>
    /// <param name="producerId">A stable, non-empty id of the collector that produced the report.</param>
    /// <param name="customFormatName">The custom format name; required when <paramref name="format"/> is <see cref="CoverageReportFormat.Custom"/> and forbidden otherwise.</param>
    public TestCoverageReportMessage(
        SessionUid sessionUid,
        string reportPath,
        CoverageReportFormat format,
        string producerId,
        string? customFormatName = null)
        : base("Test coverage report", "References a coverage report artifact.", sessionUid)
    {
        CoverageReportHelper.Validate(reportPath, nameof(reportPath), format, producerId, customFormatName);

        ReportPath = reportPath;
        Format = format;
        ProducerId = producerId;
        CustomFormatName = customFormatName;
    }

    /// <summary>Gets the path to the report artifact.</summary>
    public string ReportPath { get; }

    /// <summary>Gets the on-disk format of the report.</summary>
    public CoverageReportFormat Format { get; }

    /// <summary>Gets the custom format name; set if and only if <see cref="Format"/> is <see cref="CoverageReportFormat.Custom"/>.</summary>
    public string? CustomFormatName { get; }

    /// <summary>Gets the id of the collector that produced the report (required, correlation key).</summary>
    public string ProducerId { get; }
}
