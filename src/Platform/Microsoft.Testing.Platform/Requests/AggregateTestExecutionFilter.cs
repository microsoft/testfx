// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Represents an aggregate filter that combines multiple test execution filters using AND logic.
/// A test node must match all inner filters to pass this aggregate filter.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
[SuppressMessage("ApiDesign", "RS0016:Add public types and members to the declared API", Justification = "Experimental API")]
public sealed class AggregateTestExecutionFilter : ITestExecutionFilter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AggregateTestExecutionFilter"/> class.
    /// </summary>
    /// <param name="innerFilters">The collection of inner filters to aggregate.</param>
    public AggregateTestExecutionFilter(IReadOnlyCollection<ITestExecutionFilter> innerFilters)
    {
        Guard.NotNull(innerFilters);
        if (innerFilters.Count == 0)
        {
            throw new ArgumentException("At least one inner filter must be provided.", nameof(innerFilters));
        }

        InnerFilters = innerFilters;
    }

    /// <summary>
    /// Gets the collection of inner filters.
    /// </summary>
    public IReadOnlyCollection<ITestExecutionFilter> InnerFilters { get; }

    /// <inheritdoc />
    public bool Matches(TestNode testNode)
    {
        // AND logic: all inner filters must match
        foreach (ITestExecutionFilter filter in InnerFilters)
        {
            if (!filter.Matches(testNode))
            {
                return false;
            }
        }

        return true;
    }
}
