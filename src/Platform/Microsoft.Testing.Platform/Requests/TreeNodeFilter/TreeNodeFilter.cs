// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

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

    private static void ValidateExpression(FilterExpression expr, bool isMatchAllAllowed)
    {
        switch (expr)
        {
            case OperatorExpression { Op: FilterOperator.Not, SubExpressions: var subexprsNot } when subexprsNot.Count != 1:
            case OperatorExpression { Op: FilterOperator.And, SubExpressions.Count: < 2 }:
            case OperatorExpression { Op: FilterOperator.Or, SubExpressions.Count: < 2 }:
                throw ApplicationStateGuard.Unreachable();

            case OperatorExpression opExpr:
                foreach (FilterExpression childExpr in opExpr.SubExpressions)
                {
                    ValidateExpression(childExpr, isMatchAllAllowed);
                }

                break;

            case ValueExpression vExpr when vExpr.Value.Contains(PathSeparator):
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, PlatformResources.TreeNodeFilterCannotContainSlashCharacterErrorMessage, vExpr.Value));

            case ValueExpression vExpr when vExpr.Value.Equals(AllNodesBelowRegexString, StringComparison.Ordinal) && !isMatchAllAllowed:
                throw new ArgumentException(PlatformResources.TreeNodeFilterOnlyLastLevelCanContainMutiLevelWildcardErrorMessage);
        }
    }

    private static void ProcessStackOperator(OperatorKind op, Stack<FilterExpression> expr, Stack<OperatorKind> ops, string filter)
    {
        switch (op)
        {
            case OperatorKind.And:
            case OperatorKind.Or:
                List<FilterExpression> subexprs =
                [
                    expr.Pop(),
                    expr.Pop()
                ];

                // Note: An OR/AND operator allow to pass it in a list of expressions.
                // We can keep popping following operators and add them to the collection,
                // so that A | B | C is represented as OR { A, B, C }.
                // This limits the recursion needed to evaluate the expressions down the line.
                while (ops.Count > 0 && ops.Peek() == op)
                {
                    ops.Pop();
                    subexprs.Add(expr.Pop());
                }

                FilterOperator filterOperator = op switch
                {
                    OperatorKind.And => FilterOperator.And,
                    OperatorKind.Or => FilterOperator.Or,
                    _ => throw ApplicationStateGuard.Unreachable(),
                };

                expr.Push(new OperatorExpression(filterOperator, subexprs.ToArray()));
                break;

            case OperatorKind.FilterEquals:
            case OperatorKind.FilterNotEquals:
                FilterExpression valueExpr = expr.Pop();
                FilterExpression propExpr = expr.Pop();

                if (propExpr is not ValueExpression propValueExpr ||
                    valueExpr is not ValueExpression valueValueExpr)
                {
                    throw new InvalidOperationException();
                }

                FilterExpression filterExpression = new PropertyExpression(propValueExpr, valueValueExpr);
                if (op == OperatorKind.FilterNotEquals)
                {
                    filterExpression = new OperatorExpression(FilterOperator.Not, [filterExpression]);
                }

                expr.Push(filterExpression);
                break;

            case OperatorKind.UnaryNot:
                FilterExpression notOperator = expr.Pop();
                expr.Push(new OperatorExpression(FilterOperator.Not, [notOperator]));
                break;

            default:
                // Note: Handling of other operations in valid scenarios should be handled by the caller.
                //       Reaching this code for instance means that we're trying to process / operator
                //       in the middle of a ( expression ).
                throw new InvalidOperationException(
                    string.Format(CultureInfo.InvariantCulture, PlatformResources.TreeNodeFilterUnexpectedSlashOperatorInPathSegmentErrorMessage, filter));
        }
    }

    private static IEnumerable<string> TokenizeFilter(string filter)
    {
        int i = 0;
        StringBuilder lastStringTokenBuilder = new();
        int openedSquareBrackets = 0;

        while (i < filter.Length)
        {
            switch (filter[i])
            {
                case '\\':
                    if (i + 1 < filter.Length)
                    {
                        // Note: In case of an escape sequence take the next character and
                        //       add to the token in an escaped form. This is to encode [ as \[
                        //       so that regex will parse it directly.
                        lastStringTokenBuilder.Append(Regex.Escape(filter[i + 1].ToString()));

                        // Note: Skip the next character.
                        i++;
                    }
                    else
                    {
                        // Note: An escape character should not terminate a filter string.
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.TreeNodeFilterEscapeCharacterShouldNotBeLastErrorMessage, filter));
                    }

                    break;

                case '*':
                    lastStringTokenBuilder.Append(".*");
                    break;

                case '[':
                    openedSquareBrackets++;
                    goto case '=';

                case ']':
                    openedSquareBrackets--;
                    goto case '=';

                case '/':
                    if (openedSquareBrackets > 0)
                    {
                        lastStringTokenBuilder.Append(filter[i]);
                    }
                    else
                    {
                        goto case '=';
                    }

                    break;

                case '=':
                case '(':
                case ')':
                case '|':
                case '&':
                    if (lastStringTokenBuilder.Length > 0)
                    {
                        yield return lastStringTokenBuilder.ToString();
                        lastStringTokenBuilder.Clear();
                    }

                    yield return filter[i].ToString();

                    break;

                case '!':
                    if (i + 1 < filter.Length && filter[i + 1] == '=')
                    {
                        if (lastStringTokenBuilder.Length > 0)
                        {
                            yield return lastStringTokenBuilder.ToString();
                            lastStringTokenBuilder.Clear();
                        }

                        yield return "!=";
                        i++;
                    }
                    else if (i - 1 >= 0 && filter[i - 1] == '(')
                    {
                        // Note: If we have a ! at the start of an expression, we should
                        //       treat it as a NOT operator.
                        yield return "!";
                    }
                    else
                    {
                        goto default;
                    }

                    break;

                default:
                    lastStringTokenBuilder.Append(Regex.Escape(filter[i].ToString()));
                    break;
            }

            i++;
        }

        if (lastStringTokenBuilder.Length > 0)
        {
            yield return lastStringTokenBuilder.ToString();
        }
    }
}
