// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Requests;

public sealed partial class TreeNodeFilter
{
    /// <summary>
    /// Checks whether a node path matches the tree node filter.
    /// </summary>
    /// <param name="testNodeFullPath">The segment URL encoded path.</param>
    /// <param name="filterableProperties">The URL encoded node properties.</param>
    public bool MatchesFilter(string testNodeFullPath, PropertyBag filterableProperties)
    {
        _ = testNodeFullPath ?? throw new ArgumentNullException(nameof(testNodeFullPath));
        ArgumentGuard.Ensure(testNodeFullPath.Length > 0 && testNodeFullPath[0] == PathSeparator, nameof(testNodeFullPath),
            string.Format(CultureInfo.InvariantCulture, PlatformResources.TreeNodeFilterPathShouldStartWithSlashErrorMessage, PathSeparator));

        int currentCharIndex = 1;
        int currentFragmentIndex = 0;
        while (true)
        {
            int nextFragmentStartIndex = testNodeFullPath.IndexOf(PathSeparator, currentCharIndex);

            if (currentFragmentIndex >= _filters.Count)
            {
                // Note: The regex for ** is .*.*, so we match against such a value expression.
                FilterExpression lastFilter = _filters[^1];
                if (lastFilter is ValueAndPropertyExpression valueAndPropertyExpression)
                {
                    lastFilter = valueAndPropertyExpression.Value;
                }

                return currentFragmentIndex > 0 && lastFilter is ValueExpression { Value: ".*.*" };
            }

            if (!MatchFilterPattern(
                    _filters[currentFragmentIndex],
                    testNodeFullPath,
                    currentCharIndex,
                    nextFragmentStartIndex == -1 ? testNodeFullPath.Length : nextFragmentStartIndex,
                    filterableProperties))
            {
                return false;
            }

            currentFragmentIndex++;

            if (nextFragmentStartIndex < 0)
            {
                break;
            }

            currentCharIndex = nextFragmentStartIndex + 1;
        }

        return true;
    }

    private static bool MatchFilterPattern(
        FilterExpression filterExpression,
        string testNodeFullPath,
        int startFragmentIndex,
        int endFragmentIndex,
        PropertyBag properties)
    {
        string str = testNodeFullPath[startFragmentIndex..endFragmentIndex];
        return MatchFilterPattern(filterExpression, str, properties);
    }

    private static bool MatchFilterPattern(
        FilterExpression filterExpression,
        string testNodeFragment,
        PropertyBag properties)
    {
        switch (filterExpression)
        {
            case ValueExpression vExpr:
                return vExpr.Regex.IsMatch(testNodeFragment);
            case OperatorExpression { Op: FilterOperator.Or, SubExpressions: var subexprs }:
                for (int i = 0; i < subexprs.Count; i++)
                {
                    if (MatchFilterPattern(subexprs[i], testNodeFragment, properties))
                    {
                        return true;
                    }
                }

                return false;
            case OperatorExpression { Op: FilterOperator.And, SubExpressions: var subexprs }:
                for (int i = 0; i < subexprs.Count; i++)
                {
                    if (!MatchFilterPattern(subexprs[i], testNodeFragment, properties))
                    {
                        return false;
                    }
                }

                return true;
            case OperatorExpression { Op: FilterOperator.Not, SubExpressions: [var singleSubExpr] }:
                return !MatchFilterPattern(singleSubExpr, testNodeFragment, properties);
            case ValueAndPropertyExpression { Value: var valueExpr, Properties: var propExpr }:
                return MatchFilterPattern(valueExpr, testNodeFragment, properties)
                    && MatchProperties(propExpr, properties);
            case NopExpression:
                return true;
            default:
                throw ApplicationStateGuard.Unreachable();
        }
    }

    private static bool MatchProperties(
        FilterExpression propertyExpr,
        PropertyBag properties)
    {
        switch (propertyExpr)
        {
            case PropertyExpression { PropertyName: var propExpr, Value: var valueExpr }:
                // Use the struct-based enumerator on PropertyBag to iterate every property
                // (including TestNodeStateProperty) without allocating, while keeping
                // TreeNodeFilter decoupled from PropertyBag's internal storage layout.
                PropertyBag.PropertyBagEnumerator enumerator = properties.GetStructEnumerator();
                while (enumerator.MoveNext())
                {
                    if (IsMatchingProperty(enumerator.Current, propExpr, valueExpr))
                    {
                        return true;
                    }
                }

                return false;
            case OperatorExpression { Op: FilterOperator.Or, SubExpressions: var subExprs }:
                for (int i = 0; i < subExprs.Count; i++)
                {
                    if (MatchProperties(subExprs[i], properties))
                    {
                        return true;
                    }
                }

                return false;
            case OperatorExpression { Op: FilterOperator.And, SubExpressions: var subExprs }:
                for (int i = 0; i < subExprs.Count; i++)
                {
                    if (!MatchProperties(subExprs[i], properties))
                    {
                        return false;
                    }
                }

                return true;
            case OperatorExpression { Op: FilterOperator.Not, SubExpressions: [var singleSubExpr] }:
                return !MatchProperties(singleSubExpr, properties);
            default:
                throw ApplicationStateGuard.Unreachable();
        }
    }

    private static bool IsMatchingProperty(IProperty prop, ValueExpression propExpr, ValueExpression valueExpr)
        => prop is TestMetadataProperty testMetadataProperty &&
            propExpr.Regex.IsMatch(testMetadataProperty.Key) &&
            valueExpr.Regex.IsMatch(testMetadataProperty.Value);

    private static bool HasPropertyFilterExpression(FilterExpression expression)
    {
        if (expression is ValueAndPropertyExpression)
        {
            return true;
        }

        if (expression is OperatorExpression op)
        {
            for (int i = 0; i < op.SubExpressions.Count; i++)
            {
                if (HasPropertyFilterExpression(op.SubExpressions[i]))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
