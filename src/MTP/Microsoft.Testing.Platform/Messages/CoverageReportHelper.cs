// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

internal static class CoverageReportHelper
{
    public static void Validate(
        string path,
        string pathParameterName,
        CoverageReportFormat format,
        string producerId,
        string? customFormatName)
    {
        if (RoslynString.IsNullOrEmpty(path))
        {
            throw new ArgumentException("A report path is required.", pathParameterName);
        }

        CoverageMetricHelper.ValidateProducerId(producerId);

        if (format == CoverageReportFormat.Custom && RoslynString.IsNullOrWhiteSpace(customFormatName))
        {
            throw new ArgumentException("A custom format name is required when format is Custom.", nameof(customFormatName));
        }

        if (format != CoverageReportFormat.Custom && customFormatName is not null)
        {
            throw new ArgumentException("A custom format name is only valid when format is Custom.", nameof(customFormatName));
        }
    }
}
