﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

using Polyfills;

namespace DiscoveryAndExecutionTests.Utilities;

internal static class TestCaseFilterFactory
{
    private static readonly MethodInfo CachedGetMultiValueMethod = typeof(TestCaseFilterFactory).GetMethod(nameof(GetMultiValue), BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly MethodInfo CachedEqualsComparerMethod = typeof(TestCaseFilterFactory).GetMethod(nameof(EqualsComparer), BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly MethodInfo CachedContainsComparerMethod = typeof(TestCaseFilterFactory).GetMethod(nameof(ContainsComparer), BindingFlags.Static | BindingFlags.NonPublic);

    public static ITestCaseFilterExpression ParseTestFilter(string filterString)
    {
        Guard.NotNullOrEmpty(filterString);
        if (Regex.IsMatch(filterString, @"\(\s*\)"))
        {
            throw new FormatException($"Invalid filter, empty parenthesis: {filterString}");
        }

        IEnumerable<string> tokens = TokenizeFilter(filterString);

        var ops = new Stack<Operator>();
        var exp = new Stack<Expression<Func<Func<string, object?>, bool>>>();

        // simplified version of microsoft/vstest/src/Microsoft.TestPlatform.Common/Filtering/FilterExpression.cs

        // This is based on standard parsing of in order expression using two stacks (operand stack and operator stack)
        // Precedence(And) > Precedence(Or)
        foreach (string t in tokens)
        {
            string token = t.Trim();
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            switch (token)
            {
                case "&":
                case "|":
                    Operator op = token == "&" ? Operator.And : Operator.Or;
                    Operator top = ops.Count == 0 ? Operator.None : ops.Peek();
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
                    Expression<Func<Func<string, object?>, bool>> e = ConditionExpression(token);
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

        Func<Func<string, object?>, bool> lambda = exp.Pop().Compile();

        return new TestFilterExpression(filterString, lambda);
    }

    private class TestFilterExpression : ITestCaseFilterExpression
    {
        private readonly Func<Func<string, object?>, bool> _expression;

        public TestFilterExpression(string filter, Func<Func<string, object?>, bool> expression)
        {
            TestCaseFilterValue = filter;
            _expression = expression;
        }

        public string TestCaseFilterValue { get; }

        public bool MatchTestCase(TestCase testCase, Func<string, object?> propertyValueProvider)
            => _expression(propertyValueProvider);
    }

    private static void MergeExpression(Stack<Expression<Func<Func<string, object?>, bool>>> exp, Operator op)
    {
        Guard.NotNull(exp);
        if (op is not Operator.And and not Operator.Or)
        {
            throw new ArgumentException($"Unexpected operator: {op}", nameof(op));
        }

        if (exp.Count != 2)
        {
            throw new ArgumentException($"Unexpected expression tree: {exp.Count} elements, expected 2.", nameof(exp));
        }

        ParameterExpression parameter = Expression.Parameter(typeof(Func<string, object>), "value");
        InvocationExpression right = Expression.Invoke(exp.Pop(), parameter);
        InvocationExpression left = Expression.Invoke(exp.Pop(), parameter);

        Expression body = op == Operator.And ? Expression.And(left, right) : Expression.Or(left, right);

        var lambda = Expression.Lambda<Func<Func<string, object?>, bool>>(body, parameter);

        exp.Push(lambda);
    }

    private static IEnumerable<string> TokenizeFilter(string filterString)
    {
        var token = new StringBuilder(filterString.Length);

        bool escaping = false;
        for (int i = 0; i < filterString.Length; i++)
        {
            char c = filterString[i];

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
        Guard.NotNullOrEmpty(conditionString);
        var token = new StringBuilder(conditionString.Length);

        for (int i = 0; i < conditionString.Length; i++)
        {
            char c = conditionString[i];

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
                        char op = conditionString[i + 1];

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

    private static string[]? GetMultiValue(object value)
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

        foreach (string v in values)
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

        foreach (string v in values)
        {
            if (v.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static Expression<Func<Func<string, object?>, bool>> ConditionExpression(string conditionString)
    {
        Guard.NotNull(conditionString);

        string[] condition = [.. TokenizeCondition(conditionString)];

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
            throw new FormatException("Invalid ConditionExpression: " + conditionString);
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

        var lambda = Expression.Lambda<Func<Func<string, object?>, bool>>(expression, parameter);

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
