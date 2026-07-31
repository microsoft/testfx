// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.CtrfReport;

/// <summary>
/// Controls how <see cref="CtrfReportMerger"/> combines the <c>tests[]</c> arrays of its inputs.
/// </summary>
internal enum CtrfMergeMode
{
    /// <summary>
    /// Concatenates the inputs, which is correct when they describe disjoint sets of tests (the shard or
    /// per-module case). This is the default: MTP test UIDs are only unique WITHIN an assembly, so collapsing
    /// by identity across modules would fuse same-named tests from different assemblies.
    /// </summary>
    Concatenate,

    /// <summary>
    /// Folds rows describing the same logical test into one, which is correct when the inputs are successive
    /// attempts of the same test module (<c>--retry-failed-tests</c>): the last attempt wins and earlier ones
    /// become its <c>retryAttempts[]</c>. Inputs MUST be supplied in attempt order, and MUST come from the same
    /// module for identities to be comparable.
    /// </summary>
    CollapseRetryAttempts,
}
