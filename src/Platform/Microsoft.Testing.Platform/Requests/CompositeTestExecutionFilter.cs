// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Represents an explicit composition of test execution filters.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class CompositeTestExecutionFilter : ITestExecutionFilter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeTestExecutionFilter"/> class.
    /// </summary>
    /// <param name="operator">The operator used to combine the child filters.</param>
    /// <param name="filters">The child filters.</param>
    public CompositeTestExecutionFilter(TestExecutionFilterOperator @operator, params ITestExecutionFilter[] filters)
    {
        if (@operator != TestExecutionFilterOperator.And)
        {
            throw new ArgumentOutOfRangeException(nameof(@operator), @operator, PlatformResources.UnsupportedTestExecutionFilterOperator);
        }

        _ = filters ?? throw new ArgumentNullException(nameof(filters));
        if (filters.Length < 2)
        {
            throw new ArgumentException(PlatformResources.CompositeTestExecutionFilterRequiresTwoFilters, nameof(filters));
        }

        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i] is null)
            {
                throw new ArgumentException(PlatformResources.CompositeTestExecutionFilterCannotContainNull, nameof(filters));
            }
        }

        Operator = @operator;
        Filters = Array.AsReadOnly([.. filters]);
    }

    /// <summary>
    /// Gets the operator used to combine the child filters.
    /// </summary>
    public TestExecutionFilterOperator Operator { get; }

    /// <summary>
    /// Gets the child filters.
    /// </summary>
    public IReadOnlyList<ITestExecutionFilter> Filters { get; }
}
