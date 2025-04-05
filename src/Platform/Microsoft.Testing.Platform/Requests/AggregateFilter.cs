// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Represents an aggregate filter for test execution that combines multiple filters.
/// </summary>
/// <param name="innerFilters">The list of inner filters to combine.</param>
public sealed class AggregateFilter(params IReadOnlyList<ITestExecutionFilter> innerFilters) : ITestExecutionFilter
{
    /// <summary>
    /// Gets the list of inner filters.
    /// </summary>
    public IReadOnlyList<ITestExecutionFilter> InnerFilters { get; } = innerFilters;

    private List<ITestExecutionFilter>? _enabledFilters;

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync()
    {
        await GetEnabledFiltersAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> MatchesFilterAsync(TestNode testNode)
    {
        foreach (ITestExecutionFilter testExecutionFilter in await GetEnabledFiltersAsync())
        {
            if (!await testExecutionFilter.MatchesFilterAsync(testNode))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<List<ITestExecutionFilter>> GetEnabledFiltersAsync()
        => _enabledFilters ??= await Task.Run(async () =>
        {
            var list = new List<ITestExecutionFilter>();

            foreach (ITestExecutionFilter testExecutionFilter in InnerFilters)
            {
                if (await testExecutionFilter.IsEnabledAsync())
                {
                    list.Add(testExecutionFilter);
                }
            }

            return list;
        });
}
