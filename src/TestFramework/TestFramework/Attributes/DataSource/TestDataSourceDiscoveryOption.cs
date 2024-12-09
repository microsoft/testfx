// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The supported discovery modes for <see cref="ITestDataSource"/> tests.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete("Type is obsolete and will be removed in v4, instead use 'TestDataSourceUnfoldingStrategy'.", DiagnosticId = "MSTESTOBS")]
#else
[Obsolete("Type is obsolete and will be removed in v4, instead use 'TestDataSourceUnfoldingStrategy'.")]
#endif
public enum TestDataSourceDiscoveryOption
{
    /// <summary>
    /// Discover tests during execution.
    /// </summary>
    /// <remarks>
    /// This was the default option until version 2.2.3.
    /// </remarks>
    DuringExecution = 1,

    /// <summary>
    /// Discover and expand ITestDataSource based tests.
    /// </summary>
    /// <remarks>
    /// This is the default behavior after version 2.2.3.
    /// </remarks>
    DuringDiscovery = 2,
}
