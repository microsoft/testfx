// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// How per-scope values are combined for a threshold evaluation. Append-only. The population being
/// aggregated is identified separately by <see cref="TestCoverageThresholdMessage.AggregatedOver"/> —
/// e.g. <see cref="Minimum"/> over <see cref="CoverageScopeLevel.Module"/> vs. over
/// <see cref="CoverageScopeLevel.File"/> are distinct evaluations.
/// </summary>
public enum CoverageAggregation
{
    /// <summary>Not an aggregate — a single scope's own value (use with an exact scope).</summary>
    None = 0,

    /// <summary>Aggregate covered / aggregate coverable across the population.</summary>
    Total = 1,

    /// <summary>Worst scope in the population.</summary>
    Minimum = 2,

    /// <summary>Mean of per-scope percentages in the population.</summary>
    Average = 3,

    /// <summary>Best scope in the population.</summary>
    Maximum = 4,
}
