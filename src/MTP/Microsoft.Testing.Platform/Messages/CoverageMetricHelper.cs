// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>Shared validation for coverage metrics.</summary>
internal static class CoverageMetricHelper
{
    public static void ValidateCounts(long coveredCount, long coverableCount)
    {
        if (coverableCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(coverableCount));
        }

        if (coveredCount < 0 || coveredCount > coverableCount)
        {
            throw new ArgumentOutOfRangeException(nameof(coveredCount));
        }
    }

    public static void ValidateProducerId(string producerId)
    {
        if (RoslynString.IsNullOrEmpty(producerId))
        {
            throw new ArgumentException("A stable, non-empty producer id is required.", nameof(producerId));
        }
    }

    public static void ValidateCustomMetricName(CoverageMetric metric, string? customMetricName)
    {
        if (metric == CoverageMetric.Custom && RoslynString.IsNullOrWhiteSpace(customMetricName))
        {
            throw new ArgumentException("A custom metric name is required when metric is Custom.", nameof(customMetricName));
        }

        if (metric != CoverageMetric.Custom && customMetricName is not null)
        {
            throw new ArgumentException("A custom metric name is only valid when metric is Custom.", nameof(customMetricName));
        }
    }
}
