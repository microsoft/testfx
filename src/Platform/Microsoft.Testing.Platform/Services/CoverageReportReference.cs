// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Services;

/// <summary>
/// A pointer to a rich coverage report artifact, correlated through <see cref="ITestCoverageResult"/>.
/// Deep consumers (HTML/UI generators) parse the referenced artifact for the per-line data the summary
/// intentionally does not carry.
/// </summary>
public sealed class CoverageReportReference
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CoverageReportReference"/> class.
    /// </summary>
    /// <param name="sessionUid">The session this report belongs to.</param>
    /// <param name="path">The path to the report artifact.</param>
    /// <param name="format">The on-disk format of the report.</param>
    /// <param name="producerId">The collector that produced the report; part of the correlation key.</param>
    /// <param name="customFormatName">The custom format name; set only when <paramref name="format"/> is <see cref="CoverageReportFormat.Custom"/>.</param>
    public CoverageReportReference(
        SessionUid sessionUid,
        string path,
        CoverageReportFormat format,
        string producerId,
        string? customFormatName = null)
    {
        CoverageReportHelper.Validate(path, nameof(path), format, producerId, customFormatName);

        SessionUid = sessionUid;
        Path = path;
        Format = format;
        ProducerId = producerId;
        CustomFormatName = customFormatName;
    }

    /// <summary>Gets the session this report belongs to.</summary>
    public SessionUid SessionUid { get; }

    /// <summary>Gets the path to the report artifact.</summary>
    public string Path { get; }

    /// <summary>Gets the on-disk format of the report.</summary>
    public CoverageReportFormat Format { get; }

    /// <summary>Gets the custom format name; set only when <see cref="Format"/> is <see cref="CoverageReportFormat.Custom"/>.</summary>
    public string? CustomFormatName { get; }

    /// <summary>Gets the collector that produced the report; part of the correlation key.</summary>
    public string ProducerId { get; }
}
