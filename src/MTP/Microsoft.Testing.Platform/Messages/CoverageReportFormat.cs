// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>The on-disk format of a coverage report artifact. Append-only.</summary>
public enum CoverageReportFormat
{
    /// <summary>An unknown / unspecified format.</summary>
    Unknown = 0,

    /// <summary>Cobertura XML.</summary>
    Cobertura = 1,

    /// <summary>OpenCover XML.</summary>
    OpenCover = 2,

    /// <summary>lcov text.</summary>
    Lcov = 3,

    /// <summary>Microsoft.CodeCoverage XML.</summary>
    CoverageXml = 4,

    /// <summary>A collector-specific format identified by <see cref="TestCoverageReportMessage.CustomFormatName"/>.</summary>
    Custom = 255,
}
