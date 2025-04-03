// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Represents a filter based on test node UIDs.
/// </summary>
public sealed class TestNodeUidListFilter : ITestExecutionFilter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestNodeUidListFilter"/> class.
    /// </summary>
    /// <param name="testNodeUids">The test node UIDs to filter.</param>
    public TestNodeUidListFilter(TestNodeUid[] testNodeUids) => TestNodeUids = testNodeUids;

    /// <summary>
    /// Gets the test node UIDs to filter.
    /// </summary>
    public TestNodeUid[] TestNodeUids { get; }

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(TestNodeUids.Length > 0);

    /// <inheritdoc />
    public Task<bool> MatchesFilterAsync(TestNode testNode) => Task.FromResult(TestNodeUids.Contains(testNode.Uid));
}
