// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Represents an aggregate filter for test execution that combines multiple filters.
/// </summary>
/// <param name="innerFilters">The list of inner filters to combine.</param>
public sealed class AggregateFilter(params IReadOnlyList<ITestExecutionFilter> innerFilters) : ITestExecutionFilter, IReceiveTestNodesExtension
{
    /// <summary>
    /// Gets the list of inner filters.
    /// </summary>
    public IReadOnlyList<ITestExecutionFilter> InnerFilters { get; } = innerFilters;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public bool MatchesFilter(TestNode testNode) => InnerFilters.All(x => x.MatchesFilter(testNode));

    /// <inheritdoc />
    public void ReceiveTestNodes(IReadOnlyList<TestNode> testNodes)
    {
        // Notify all inner filters about the test nodes if they implement IReceiveTestNodesExtension.
        foreach (IReceiveTestNodesExtension receiveTestNodesFilter in InnerFilters.OfType<IReceiveTestNodesExtension>())
        {
            receiveTestNodesFilter.ReceiveTestNodes(testNodes);
        }
    }
}
