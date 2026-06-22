// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// A tree based filter for test execution.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed partial class TreeNodeFilter : ITestExecutionFilter
{
    /// <summary>
    /// The path separator character.
    /// </summary>
    public const char PathSeparator = '/';

    // Note: After the token gets expanded into regex ** gets converted to .*.*.
    internal const string AllNodesBelowRegexString = ".*.*";
    private readonly List<FilterExpression> _filters;

    internal TreeNodeFilter(string filter)
    {
        Filter = filter ?? throw new ArgumentNullException(nameof(filter));
        _filters = ParseFilter(filter);
        bool containsPropertyFilters = false;
        for (int i = 0; i < _filters.Count; i++)
        {
            if (HasPropertyFilterExpression(_filters[i]))
            {
                containsPropertyFilters = true;
                break;
            }
        }

        ContainsPropertyFilters = containsPropertyFilters;
    }

    /// <summary>
    /// Gets the filter string.
    /// </summary>
    public string Filter { get; }

    /// <summary>
    /// Gets a value indicating whether any filter segment contains a property expression (e.g., <c>Method[Trait=Foo]</c>).
    /// When <see langword="false"/>, the <see cref="PropertyBag"/> argument to <see cref="MatchesFilter"/> is never
    /// inspected, and callers may safely pass an empty bag to avoid per-node allocation.
    /// </summary>
    internal bool ContainsPropertyFilters { get; }
}
