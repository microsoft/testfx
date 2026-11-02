// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

internal readonly struct CoverageCounts
{
    internal CoverageCounts(long coveredCount, long coverableCount)
    {
        CoverageMetricHelper.ValidateCounts(coveredCount, coverableCount);

        CoveredCount = coveredCount;
        CoverableCount = coverableCount;
    }

    internal long CoveredCount { get; }

    internal long CoverableCount { get; }

    internal bool HasCoverableData => CoverableCount > 0;

    internal double Percentage => HasCoverableData ? (double)CoveredCount / CoverableCount * 100d : 0d;
}
