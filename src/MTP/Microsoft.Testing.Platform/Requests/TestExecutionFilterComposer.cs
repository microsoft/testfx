// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Requests;

internal static class TestExecutionFilterComposer
{
    public static async Task<ITestExecutionFilter> ComposeAsync(
        ITestExecutionFilter requestFilter,
        IReadOnlyList<ITestExecutionFilterProvider> providers,
        TestExecutionFilterContext context,
        bool allowProviderContributions,
        CancellationToken cancellationToken)
    {
        List<(string ProviderUid, ITestExecutionFilter Filter)>? providerFilters = null;
        foreach (ITestExecutionFilterProvider provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ITestExecutionFilter? providerFilter = await provider.GetFilterAsync(context, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (providerFilter is null or NopFilter)
            {
                continue;
            }

            if (!allowProviderContributions)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        PlatformResources.TestExecutionFilterProviderNotSupportedForServer,
                        provider.Uid,
                        providerFilter.GetType().FullName));
            }

            (providerFilters ??= []).Add((provider.Uid, providerFilter));
        }

        // No provider contributed a constraint, so there is nothing to compose. Return the built-in request
        // filter as-is: applications without providers keep the exact object and semantics they had before
        // composition existed, including filter representations only their own framework understands.
        if (providerFilters is null)
        {
            return requestFilter;
        }

        List<TestNodeUidListFilter> uidFilters = [];
        List<ITestExecutionFilter> otherFilters = [];
        AddConstraints(requestFilter, providerUid: null, uidFilters, otherFilters);
        foreach ((string providerUid, ITestExecutionFilter providerFilter) in providerFilters)
        {
            AddConstraints(providerFilter, providerUid, uidFilters, otherFilters);
        }

        if (uidFilters.Count > 0)
        {
            HashSet<string>? intersection = null;
            foreach (TestNodeUidListFilter uidFilter in uidFilters.OrderBy(filter => filter.TestNodeUids.Length))
            {
                var currentUids = new HashSet<string>(
                    uidFilter.TestNodeUids.Select(uid => uid.Value),
                    StringComparer.Ordinal);

                if (intersection is null)
                {
                    intersection = currentUids;
                }
                else
                {
                    intersection.IntersectWith(currentUids);
                }

                if (intersection.Count == 0)
                {
                    break;
                }
            }

            RoslynDebug.Assert(intersection is not null);
            otherFilters.Add(new TestNodeUidListFilter(
                [.. intersection.OrderBy(uid => uid, StringComparer.Ordinal).Select(uid => new TestNodeUid(uid))]));
        }

        return otherFilters.Count switch
        {
            0 => new NopFilter(),
            1 => otherFilters[0],
            _ => new CompositeTestExecutionFilter(TestExecutionFilterOperator.And, [.. otherFilters]),
        };
    }

    internal static IEnumerable<ITestExecutionFilter> GetLeafFilters(ITestExecutionFilter filter)
    {
        if (filter is not CompositeTestExecutionFilter composite)
        {
            yield return filter;
            yield break;
        }

        if (composite.Operator != TestExecutionFilterOperator.And)
        {
            throw new NotSupportedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.UnsupportedTestExecutionFilterOperatorValue,
                    composite.Operator));
        }

        foreach (ITestExecutionFilter childFilter in composite.Filters)
        {
            foreach (ITestExecutionFilter leafFilter in GetLeafFilters(childFilter))
            {
                yield return leafFilter;
            }
        }
    }

    private static void AddConstraints(
        ITestExecutionFilter filter,
        string? providerUid,
        List<TestNodeUidListFilter> uidFilters,
        List<ITestExecutionFilter> otherFilters)
    {
        switch (filter)
        {
            case NopFilter:
                return;

            case TestNodeUidListFilter uidFilter:
                uidFilters.Add(uidFilter);
                return;

            case TreeNodeFilter treeNodeFilter:
                otherFilters.Add(treeNodeFilter);
                return;

            case CompositeTestExecutionFilter composite when composite.Operator == TestExecutionFilterOperator.And:
                foreach (ITestExecutionFilter childFilter in composite.Filters)
                {
                    AddConstraints(childFilter, providerUid, uidFilters, otherFilters);
                }

                return;

            case CompositeTestExecutionFilter composite:
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        PlatformResources.UnsupportedTestExecutionFilterOperatorValue,
                        composite.Operator));

            default:
                throw providerUid is null
                    ? new InvalidOperationException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            PlatformResources.UnsupportedRequestTestExecutionFilter,
                            filter.GetType().FullName))
                    : new InvalidOperationException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            PlatformResources.UnsupportedProviderTestExecutionFilter,
                            providerUid,
                            filter.GetType().FullName));
        }
    }
}
