// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

namespace DiscoveryAndExecutionTests.Utilities;

internal static class TestCaseFilterFactory
{
    private static readonly MethodInfo CachedGetMultiValueMethod = typeof(TestCaseFilterFactory).GetMethod(nameof(GetMultiValue), BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly MethodInfo CachedEqualsComparerMethod = typeof(TestCaseFilterFactory).GetMethod(nameof(EqualsComparer), BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly MethodInfo CachedContainsComparerMethod = typeof(TestCaseFilterFactory).GetMethod(nameof(ContainsComparer), BindingFlags.Static | BindingFlags.NonPublic);

    public static ITestCaseFilterExpression ParseTestFilter(string filterString)
    {
        ValidateArg.NotNullOrEmpty(filterString, nameof(filterString));
        if (Regex.IsMatch(filterString, @"\(\s*\)"))
        {
            throw new FormatException($"Invalid filter, empty parenthesis: {filterString}");
        }

        var tokens = TokenizeFilter(filterString);

        var ops = new Stack<Operator>();
        var exp = new Stack<Expression<Func<Func<string, object>, bool>>>();

        // simplified version of microsoft/vstest/src/Microsoft.TestPlatform.Common/Filtering/FilterExpression.cs

        // This is based on standard parsing of in order expression using two stacks (operand stack and operator stack)
        // Precedence(And) > Precedence(Or)
        foreach (var t in tokens)
        {
            var token = t.Trim();
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            switch (token)
            {
                case "&":
                case "|":
                    var op = token == "&" ? Operator.And : Operator.Or;
                    var top = ops.Count == 0 ? Operator.None : ops.Peek();
                    if (ops.Count == 0 || top == Operator.OpenBrace || top < op)
                    {
                        ops.Push(op);
                        continue;
                    }

                    MergeExpression(exp, ops.Pop());
                    continue;

                case "(":
                    ops.Push(Operator.OpenBrace);
                    continue;

                case ")":
                    if (ops.Count == 0)
                    {
                        throw new FormatException($"Invalid filter, missing parenthesis open: {filterString}");
                    }

                    while (ops.Peek() != Operator.OpenBrace)
                    {
                        MergeExpression(exp, ops.Pop());
                        if (ops.Count == 0)
                        {
                            throw new FormatException($"Invalid filter, missing parenthesis open: {filterString}");
                        }
                    }

                    ops.Pop();
                    continue;

                default:
                    var e = ConditionExpresion(token);
                    exp.Push(e);
                    break;
            }
        }

        while (ops.Count != 0)
        {
            MergeExpression(exp, ops.Pop());
        }

        if (exp.Count != 1)
        {
            throw new FormatException($"Invalid filter, missing operator: {filterString}");
        }

        var lambda = exp.Pop().Compile();

        return new TestFilterExpression(filterString, lambda);
    }

    private class TestFilterExpression : ITestCaseFilterExpression
    {
        private readonly Func<Func<string, object>, bool> _expression;

        public TestFilterExpression(string filter, Func<Func<string, object>, bool> expression)
        {
            TestCaseFilterValue = filter;
            _expression = expression;
        }

        public string TestCaseFilterValue { get; }

        public bool MatchTestCase(TestCase testCase, Func<string, object> propertyValueProvider) => _expression(propertyValueProvider);
    }

    private static void MergeExpression(Stack<Expression<Func<Func<string, object>, bool>>> exp, Operator op)
    {
        ValidateArg.NotNull(exp, nameof(exp));
        if (op is not Operator.And and not Operator.Or)
        {
            throw new ArgumentException($"Unexpected operator: {op}", nameof(op));
        }

        if (exp.Count != 2)
        {
            throw new ArgumentException($"Unexpected expression tree: {exp.Count} elements, expected 2.", nameof(exp));
        }

        var parameter = Expression.Parameter(typeof(Func<string, object>), "value");
        var right = Expression.Invoke(exp.Pop(), parameter);
        var left = Expression.Invoke(exp.Pop(), parameter);

        Expression body = op == Operator.And ? Expression.And(left, right) : Expression.Or(left, right);

        var lambda = Expression.Lambda<Func<Func<string, object>, bool>>(body, parameter);

        exp.Push(lambda);
    }

    private static IEnumerable<string> TokenizeFilter(string filterString)
    {
        var token = new StringBuilder(filterString.Length);

        var escaping = false;
        for (int i = 0; i < filterString.Length; i++)
        {
            var c = filterString[i];

            if (escaping)
            {
                token.Append(c);
                escaping = false;
                continue;
            }

            switch (c)
            {
                case FilterHelper.EscapeCharacter:
                    escaping = true;
                    continue;

                case '&':
                case '|':
                case '(':
                case ')':
                    if (token.Length != 0)
                    {
                        yield return token.ToString();
                        token.Clear();
                    }

                    yield return c.ToString();
                    continue;

                default:
                    token.Append(c);
                    break;
            }
        }

        if (token.Length != 0)
        {
            yield return token.ToString();
        }
    }

    private static IEnumerable<string> TokenizeCondition(string conditionString)
    {
        ValidateArg.NotNullOrEmpty(conditionString, nameof(conditionString));
        var token = new StringBuilder(conditionString.Length);

        var escaped = false;
        for (int i = 0; i < conditionString.Length; i++)
        {
            var c = conditionString[i];

            if (escaped)
            {
                token.Append(c);
                escaped = false;
                continue;
            }

            switch (c)
            {
                case '=':
                case '~':
                case '!':
                    if (token.Length > 0)
                    {
                        yield return token.ToString();
                        token.Clear();
                    }

                    if (c == '!')
                    {
                        var op = conditionString[i + 1];

                        if (op is '~' or '=')
                        {
                            yield return token.ToString() + conditionString[++i];
                            continue;
                        }
                    }

                    yield return c.ToString();
                    continue;

                default:
                    token.Append(c);
                    break;
            }
        }

        if (token.Length > 0)
        {
            yield return token.ToString();
        }
    }

    private static string[] GetMultiValue(object value)
    {
        if (value is string[] i)
        {
            return i;
        }
        else if (value != null)
        {
            return [value.ToString()];
        }

        return null;
    }

    private static bool EqualsComparer(string[] values, string value)
    {
        if (values == null)
        {
            return false;
        }

        foreach (var v in values)
        {
            if (v.Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsComparer(string[] values, string value)
    {
        if (values == null)
        {
            return false;
        }

        foreach (var v in values)
        {
            if (v.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static Expression<Func<Func<string, object>, bool>> ConditionExpresion(string conditionString)
    {
        ValidateArg.NotNull(conditionString, nameof(conditionString));

        var condition = TokenizeCondition(conditionString).ToArray();

        Expression parameterName, expectedValue, parameterValueProvider, expression;
        string op;
        if (condition.Length == 1)
        {
            parameterName = Expression.Constant("FullyQualifiedName");
            expectedValue = Expression.Constant(conditionString.Trim());
            op = "~";
        }
        else if (condition.Length == 3)
        {
            parameterName = Expression.Constant(condition[0]);
            expectedValue = Expression.Constant(condition[2].Trim());
            op = condition[1];
        }
        else
        {
            throw new FormatException("Invalid ConditionExpresion: " + conditionString);
        }

        ParameterExpression parameter = Expression.Parameter(typeof(Func<string, object>), "p");

        parameterValueProvider = Expression.Call(CachedGetMultiValueMethod, Expression.Invoke(parameter, parameterName));
        MethodInfo comparer = op.Last() switch
        {
            '=' => CachedEqualsComparerMethod,
            '~' => CachedContainsComparerMethod,
            _ => throw new FormatException($"Invalid operator in {conditionString}: {condition[1]}"),
        };
        expression = Expression.Call(comparer, parameterValueProvider, expectedValue);

        if (op[0] == '!')
        {
            expression = Expression.Not(expression);
        }

        var lambda = Expression.Lambda<Func<Func<string, object>, bool>>(expression, parameter);

        return lambda;
    }

    private enum Operator
    {
        None,
        Or,
        And,
        OpenBrace,
        CloseBrace,
    }
}
