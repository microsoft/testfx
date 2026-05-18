// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Provides That extension to Assert class.
/// </summary>
public static partial class AssertExtensions
{
    /// <summary>
    /// Provides That extension to Assert class.
    /// </summary>
    extension(Assert _)
    {
        /// <summary>
        /// Evaluates a boolean condition and throws an <see cref="AssertFailedException"/> if the condition is <see
        /// langword="false"/>.
        /// </summary>
        /// <param name="condition">An expression representing the condition to evaluate. Cannot be <see langword="null"/>.</param>
        /// <param name="message">An optional message to include in the exception if the assertion fails.</param>
        /// <param name="conditionExpression">The source code of the condition expression. This parameter is automatically populated by the compiler.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="condition"/> is <see langword="null"/>.</exception>
        /// <exception cref="AssertFailedException">Thrown if the evaluated condition is <see langword="false"/>.</exception>
        public static void That(Expression<Func<bool>> condition, string? message = null, [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
        {
            TelemetryCollector.TrackAssertionCall("Assert.That");

            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition));
            }

            var details = new Dictionary<string, object?>();
            bool result = EvaluateAndCollectDetails(condition.Body, details);

            if (result)
            {
                return;
            }

            var sb = new StringBuilder();
            string expressionText = conditionExpression
                ?? throw new ArgumentNullException(nameof(conditionExpression));
            if (!string.IsNullOrWhiteSpace(message))
            {
                sb.AppendLine();
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, FrameworkMessages.AssertThatMessageFormat, message));
            }

            string detailsString = BuildDetailsString(details);
            if (!string.IsNullOrWhiteSpace(detailsString))
            {
                if (sb.Length == 0)
                {
                    sb.AppendLine();
                }

                sb.AppendLine(FrameworkMessages.AssertThatDetailsPrefix);
                sb.AppendLine(detailsString);
            }

            Assert.ReportAssertFailed($"Assert.That({expressionText})", sb.ToString().TrimEnd());
        }
    }

    private static string BuildDetailsString(Dictionary<string, object?> details)
    {
        if (details.Count == 0)
        {
            return string.Empty;
        }

        // Sort details alphabetically by variable name for consistent ordering
        IOrderedEnumerable<KeyValuePair<string, object?>> sortedDetails = details.OrderBy(kvp => kvp.Key, StringComparer.Ordinal);

        var sb = new StringBuilder();
        foreach (KeyValuePair<string, object?> kvp in sortedDetails)
        {
#if NET
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {kvp.Key} = {FormatValue(kvp.Value)}");
#else
            sb.AppendLine($"  {kvp.Key} = {FormatValue(kvp.Value)}");
#endif
        }

        return sb.ToString();
    }

    private static readonly object UnsetCapture = new();

    /// <summary>
    /// Evaluates <paramref name="body"/> exactly once while capturing the values of selected sub-expressions
    /// so the assertion failure message can describe what each named operand evaluated to.
    /// Sub-expressions inside short-circuited / unreached branches are evaluated lazily as a fallback
    /// (one evaluation total — the root never ran them). Fixes issue #6690.
    /// </summary>
    /// <returns><see langword="true"/> if the condition evaluated to <see langword="true"/>.</returns>
    private static bool EvaluateAndCollectDetails(Expression body, Dictionary<string, object?> details)
    {
        // Pass 1: Walk the tree to identify capture points and their display names.
        var context = new AnalysisContext();
        AnalyzeExpression(body, context);

        // Pass 2: Rewrite the tree so that each captured sub-expression's value is written
        // to a captures array as a side effect of the single evaluation of the root.
        ParameterExpression arrayParam = Expression.Parameter(typeof(object?[]), "captures");
        var rewriter = new CaptureRewriter(context.CaptureMap, arrayParam);
        Expression rewrittenBody = rewriter.Visit(body)!;

        // Compile and invoke ONCE.
        // This intentionally pays the rewrite+compile cost on every Assert.That call (including passing ones)
        // to guarantee single-pass evaluation for correctness (#6690). If this ever shows up as a hotspot,
        // we can consider caching the compiled delegate by expression-tree instance.
        var lambda = Expression.Lambda<Func<object?[], bool>>(rewrittenBody, arrayParam);
        object?[] values = new object?[context.CaptureNames.Count];

        // Pre-fill with a sentinel so we can distinguish "not captured" (because the branch was
        // short-circuited / not evaluated) from "captured null".
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = UnsetCapture;
        }

        bool result = lambda.Compile()(values);

        if (result)
        {
            return true;
        }

        // Build details using first-occurrence-per-name semantics, filtering out Func/Action values
        // (matching the historical behavior, using runtime type as the existing code did).
        for (int i = 0; i < context.CaptureNames.Count; i++)
        {
            string name = context.CaptureNames[i];
            if (details.ContainsKey(name))
            {
                continue;
            }

            object? value = values[i];
            if (ReferenceEquals(value, UnsetCapture))
            {
                // The capture slot was never written, meaning the sub-expression was not evaluated
                // (e.g., a short-circuited && / || branch or an unreached ternary branch).
                // Only fall back to evaluating expressions that are conventionally pure operand
                // reads (variable/property access, array indexers and lengths). For arbitrary
                // method calls we must NOT re-evaluate, otherwise we'd silently violate
                // short-circuit semantics for the user's code (issue #6690).
                Expression expr = context.CaptureExpressions[i];
                if (IsSafeToReevaluate(expr))
                {
                    try
                    {
                        value = Expression
                            .Lambda<Func<object?>>(Expression.Convert(expr, typeof(object)))
                            .Compile()();
                    }
                    catch
                    {
                        value = "<Failed to evaluate>";
                    }
                }
                else
                {
                    // Skip potentially side-effecting captures that were short-circuited.
                    continue;
                }
            }

            if (IsFuncOrActionType(value?.GetType()))
            {
                continue;
            }

            details[name] = value;
        }

        return false;
    }

    private static void AnalyzeExpression(Expression? expr, AnalysisContext context, bool suppressIntermediateValues = false)
    {
        if (expr is null)
        {
            return;
        }

        switch (expr)
        {
            // Special handling for array indexing (myArray[index])
            case BinaryExpression binaryExpr when binaryExpr.NodeType == ExpressionType.ArrayIndex:
                AnalyzeArrayIndexExpression(binaryExpr, context);
                break;

            case BinaryExpression binaryExpr:
                AnalyzeExpression(binaryExpr.Left, context, suppressIntermediateValues);
                AnalyzeExpression(binaryExpr.Right, context, suppressIntermediateValues);
                break;

            case TypeBinaryExpression typeBinaryExpr:
                AnalyzeExpression(typeBinaryExpr.Expression, context, suppressIntermediateValues);
                break;

            // Special handling for ArrayLength expressions
            case UnaryExpression unaryExpr when unaryExpr.NodeType == ExpressionType.ArrayLength:
                string arrayName = GetCleanMemberName(unaryExpr.Operand);
                string lengthDisplayName = $"{arrayName}.Length";
                context.AddCapture(unaryExpr, lengthDisplayName);

                if (unaryExpr.Operand is not MemberExpression)
                {
                    AnalyzeExpression(unaryExpr.Operand, context, suppressIntermediateValues);
                }

                break;

            case UnaryExpression unaryExpr:
                AnalyzeExpression(unaryExpr.Operand, context, suppressIntermediateValues);
                break;

            case MemberExpression memberExpr:
                AnalyzeMemberExpression(memberExpr, context);
                break;

            case MethodCallExpression callExpr:
                AnalyzeMethodCallExpression(callExpr, context, suppressIntermediateValues);
                break;

            case ConditionalExpression conditionalExpr:
                AnalyzeExpression(conditionalExpr.Test, context, suppressIntermediateValues);
                AnalyzeExpression(conditionalExpr.IfTrue, context, suppressIntermediateValues);
                AnalyzeExpression(conditionalExpr.IfFalse, context, suppressIntermediateValues);
                break;

            case InvocationExpression invocationExpr:
                AnalyzeExpression(invocationExpr.Expression, context, suppressIntermediateValues);
                foreach (Expression argument in invocationExpr.Arguments)
                {
                    AnalyzeExpression(argument, context, suppressIntermediateValues);
                }

                break;

            case NewExpression newExpr:
                foreach (Expression argument in newExpr.Arguments)
                {
                    AnalyzeExpression(argument, context, suppressIntermediateValues);
                }

                if (!suppressIntermediateValues)
                {
                    string newExprDisplay = GetCleanMemberName(newExpr);
                    context.AddCapture(newExpr, newExprDisplay);
                }

                break;

            case ListInitExpression listInitExpr:
                AnalyzeExpression(listInitExpr.NewExpression, context, suppressIntermediateValues: true);
                foreach (ElementInit initializer in listInitExpr.Initializers)
                {
                    foreach (Expression argument in initializer.Arguments)
                    {
                        AnalyzeExpression(argument, context, suppressIntermediateValues);
                    }
                }

                break;

            case NewArrayExpression newArrayExpr:
                foreach (Expression expression in newArrayExpr.Expressions)
                {
                    AnalyzeExpression(expression, context, suppressIntermediateValues);
                }

                break;
        }
    }

    private static void AnalyzeArrayIndexExpression(BinaryExpression arrayIndexExpr, AnalysisContext context)
    {
        string arrayName = GetCleanMemberName(arrayIndexExpr.Left);
        string indexValue = GetIndexArgumentDisplay(arrayIndexExpr.Right);
        string indexerDisplay = $"{arrayName}[{indexValue}]";
        context.AddCapture(arrayIndexExpr, indexerDisplay);

        AnalyzeExpression(arrayIndexExpr.Right, context);
    }

    private static void AnalyzeMemberExpression(MemberExpression memberExpr, AnalysisContext context)
    {
        // Skip Func and Action delegates as they don't provide useful information in assertion failures.
        // Use the static type so we don't have to evaluate the expression at analysis time.
        if (IsFuncOrActionType(memberExpr.Type))
        {
            return;
        }

        string displayName = GetCleanMemberName(memberExpr);
        context.AddCapture(memberExpr, displayName);

        // Only extract variables from the object being accessed if it's not a member expression or indexer (which would show the full collection)
        if (memberExpr.Expression is not null and not MemberExpression)
        {
            AnalyzeExpression(memberExpr.Expression, context, suppressIntermediateValues: true);
        }
    }

    private static void AnalyzeMethodCallExpression(MethodCallExpression callExpr, AnalysisContext context, bool suppressIntermediateValues = false)
    {
        // Special handling for indexers (get_Item calls)
        if (callExpr.Method.Name == "get_Item" && callExpr.Object is not null && callExpr.Arguments.Count == 1)
        {
            string objectName = GetCleanMemberName(callExpr.Object);
            string indexValue = GetIndexArgumentDisplay(callExpr.Arguments[0]);
            string indexerDisplay = $"{objectName}[{indexValue}]";
            context.AddCapture(callExpr, indexerDisplay);

            AnalyzeExpression(callExpr.Arguments[0], context, suppressIntermediateValues);
        }
        else if (IsArrayGetMethod(callExpr))
        {
            string objectName = GetCleanMemberName(callExpr.Object);
            string indexDisplay = string.Join(", ", callExpr.Arguments.Select(GetIndexArgumentDisplay));
            string indexerDisplay = $"{objectName}[{indexDisplay}]";
            context.AddCapture(callExpr, indexerDisplay);

            foreach (Expression argument in callExpr.Arguments)
            {
                AnalyzeExpression(argument, context, suppressIntermediateValues);
            }
        }
        else
        {
            if (callExpr.Method.ReturnType == typeof(bool))
            {
                if (callExpr.Object is not null)
                {
                    AnalyzeExpression(callExpr.Object, context, suppressIntermediateValues);
                }
            }
            else
            {
                string methodCallDisplay = GetMethodCallDisplayName(callExpr);
                context.AddCapture(callExpr, methodCallDisplay);
            }

            foreach (Expression argument in callExpr.Arguments)
            {
                AnalyzeExpression(argument, context, suppressIntermediateValues);
            }
        }
    }

    /// <summary>
    /// Builds a friendly display name for a method-call expression so the details message uses the same
    /// syntax the user wrote. Static methods get prefixed with their declaring type's name; instance methods on
    /// captured <c>this</c> render as <c>this.Method(...)</c>; extension methods use the first argument as the
    /// receiver. Fixes issue #6691.
    /// </summary>
    private static string GetMethodCallDisplayName(MethodCallExpression callExpr)
    {
        string methodName = callExpr.Method.Name;

        // Extension methods are static methods on a static class marked [Extension]; the receiver is the
        // first argument. Render like the user wrote: receiver.Method(rest).
        if (callExpr.Object is null
            && callExpr.Method.IsDefined(typeof(ExtensionAttribute), inherit: false)
            && callExpr.Arguments.Count > 0)
        {
            string receiver = GetCleanMemberName(callExpr.Arguments[0]);
            string extArgs = string.Join(", ", callExpr.Arguments.Skip(1).Select(static a => CleanExpressionText(a.ToString())));
            return $"{receiver}.{methodName}({extArgs})";
        }

        string argsStr = string.Join(", ", callExpr.Arguments.Select(static a => CleanExpressionText(a.ToString())));

        if (callExpr.Object is null)
        {
            // Regular static method: use the declaring type's short name as the receiver display.
            string typeName = callExpr.Method.DeclaringType is { } dt
                ? CleanTypeName(dt.FullName ?? dt.Name)
                : "<unknown>";
            return $"{typeName}.{methodName}({argsStr})";
        }

        if (IsCapturedThis(callExpr.Object, callExpr.Method.DeclaringType))
        {
            return $"this.{methodName}({argsStr})";
        }

        string objectDisplay = GetCleanMemberName(callExpr.Object);
        return $"{objectDisplay}.{methodName}({argsStr})";
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="objectExpr"/> is a reference to the enclosing
    /// instance (<c>this</c>) — either accessed via the compiler-synthesized display-class field
    /// (named like <c>&lt;&gt;4__this</c>) or as a <see cref="ConstantExpression"/> representing
    /// the enclosing instance (no closure case). For the constant form we require the expression's
    /// static type to match its runtime type and be assignable to <paramref name="declaringType"/>,
    /// so inherited methods on <c>this</c> still render as <c>this.Method(...)</c> without
    /// mis-labeling base-typed locals.
    /// </summary>
    private static bool IsCapturedThis(Expression objectExpr, Type? declaringType)
        // Display-class field for captured this is named like "<>4__this".
        => (objectExpr is MemberExpression me
                && me.Member.Name.StartsWith("<>", StringComparison.Ordinal)
                && me.Member.Name.EndsWith("__this", StringComparison.Ordinal))
            // No-closure case: the object is a ConstantExpression for the enclosing instance.
            // We still guard against base-typed values by requiring ce.Type == runtime type.
            || (declaringType is not null
                && objectExpr is ConstantExpression ce
                && ce.Value is not null
                && ce.Type == ce.Value.GetType()
                && declaringType.IsAssignableFrom(ce.Type));

    private static bool IsFuncOrActionType(Type? type)
    {
        if (type is null)
        {
            return false;
        }

        // Check for Action types
        if (type == typeof(Action) ||
            (type.IsGenericType && type.GetGenericTypeDefinition().Name.StartsWith("Action`", StringComparison.Ordinal)))
        {
            return true;
        }

        // Check for Func types
        return type.IsGenericType && type.GetGenericTypeDefinition().Name.StartsWith("Func`", StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns <see langword="true"/> for expression kinds that are conventionally pure operand
    /// reads (variable/property access, array indexers and lengths, collection indexers). These
    /// can safely be re-evaluated on their own when they were skipped by short-circuit evaluation
    /// of the root expression. Method calls and constructors are excluded because they may have
    /// side effects (see issue #6690).
    /// </summary>
    /// <remarks>
    /// This heuristic intentionally treats <see cref="MemberExpression"/> (which covers both
    /// fields and properties) and the well-known indexer-style method calls
    /// (<c>get_Item</c>/<c>Array.Get</c>) as pure, even though property getters and user-defined
    /// indexers can technically have side effects. This matches the pre-fix behavior — which
    /// always re-evaluated those expressions — and ensures backward compatibility with existing
    /// failure-detail output for short-circuited conditions like
    /// <c>name == "x" &amp;&amp; obj.Property == y</c>. Method calls with arbitrary names are
    /// excluded because they are the common case of side-effecting code (the original motivation
    /// for issue #6690). Safety is recursive: each child expression must also be safe.
    /// </remarks>
    private static bool IsSafeToReevaluate(Expression expr)
        => expr switch
        {
            ConstantExpression => true,
            ParameterExpression => true,
            MemberExpression memberExpr => memberExpr.Expression is null || IsSafeToReevaluate(memberExpr.Expression),
            BinaryExpression { NodeType: ExpressionType.ArrayIndex } arrayIndexExpr
                => IsSafeToReevaluate(arrayIndexExpr.Left) && IsSafeToReevaluate(arrayIndexExpr.Right),
            UnaryExpression { NodeType: ExpressionType.ArrayLength } arrayLengthExpr
                => IsSafeToReevaluate(arrayLengthExpr.Operand),
            // Indexer-style method calls are conventionally pure reads; the previous implementation
            // evaluated them eagerly too. Keep this limited to actual indexers and multidimensional
            // array reads so arbitrary user-defined `Get(...)` methods are not re-invoked.
            MethodCallExpression methodCallExpr when IsCollectionIndexerRead(methodCallExpr)
                => IsMethodCallSafe(methodCallExpr),
            _ => false,
        };

    private static bool IsMethodCallSafe(MethodCallExpression callExpr)
        => (callExpr.Object is null || IsSafeToReevaluate(callExpr.Object))
            && callExpr.Arguments.All(IsSafeToReevaluate);

    private static bool IsCollectionIndexerRead(MethodCallExpression callExpr)
        => callExpr.Method.Name == "get_Item" || IsArrayGetMethod(callExpr);

    private static bool IsArrayGetMethod(MethodCallExpression callExpr)
        => callExpr.Method.Name == "Get"
            && callExpr.Object is not null
            && callExpr.Method.DeclaringType == typeof(Array)
            && callExpr.Arguments.Count > 0;

    private sealed class AnalysisContext
    {
#pragma warning disable IDE0028 // Collection initialization can be simplified - Dictionary needs ReferenceEqualityComparer
        public Dictionary<Expression, int> CaptureMap { get; } = new(ReferenceEqualityComparer.Instance);
#pragma warning restore IDE0028

        public List<string> CaptureNames { get; } = [];

        public List<Expression> CaptureExpressions { get; } = [];

        public void AddCapture(Expression expr, string name)
        {
            // One slot per Expression instance, so duplicates of the same display name (e.g. `x + x`)
            // and side-effecting calls that appear multiple times each get their own slot.
            // First-occurrence-by-name wins at display-build time.
            if (CaptureMap.ContainsKey(expr))
            {
                return;
            }

            // Cannot box void-typed values into the captures array; nothing useful to display anyway.
            if (expr.Type == typeof(void))
            {
                return;
            }

            int index = CaptureNames.Count;
            CaptureNames.Add(name);
            CaptureExpressions.Add(expr);
            CaptureMap[expr] = index;
        }
    }

    /// <summary>
    /// Rewrites the lambda body so every captured sub-expression's value is stored into a captures array
    /// as a side effect of the single root evaluation. Each captured node <c>e</c> at slot <c>i</c> is
    /// replaced by <c>{ var t = e; captures[i] = (object)t; t }</c>, so <c>e</c> is evaluated exactly once.
    /// </summary>
    private sealed class CaptureRewriter : ExpressionVisitor
    {
        private readonly Dictionary<Expression, int> _captureMap;
        private readonly ParameterExpression _arrayParam;

        public CaptureRewriter(Dictionary<Expression, int> captureMap, ParameterExpression arrayParam)
        {
            _captureMap = captureMap;
            _arrayParam = arrayParam;
        }

        [return: NotNullIfNotNull(nameof(node))]
        public override Expression? Visit(Expression? node)
        {
            if (node is not null && _captureMap.TryGetValue(node, out int index))
            {
                // Visit children first so nested captures inside `node` are also rewritten and evaluated once.
                // base.Visit dispatches to VisitX (Member/MethodCall/...), which recurses into children via this.Visit,
                // so our override is consulted for every descendant.
                Expression visited = base.Visit(node)!;

                ParameterExpression temp = Expression.Variable(visited.Type);
                return Expression.Block(
                    visited.Type,
                    new[] { temp },
                    Expression.Assign(temp, visited),
                    Expression.Assign(
                        Expression.ArrayAccess(_arrayParam, Expression.Constant(index)),
                        Expression.Convert(temp, typeof(object))),
                    temp);
            }

            return base.Visit(node);
        }
    }

#if !NET
    /// <summary>
    /// Minimal stand-in for <c>System.Collections.Generic.ReferenceEqualityComparer</c>
    /// (which is .NET 5+ only) so we can key dictionaries by <see cref="Expression"/> reference
    /// from netstandard2.0.
    /// </summary>
    private sealed class ReferenceEqualityComparer : IEqualityComparer<Expression>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public bool Equals(Expression? x, Expression? y) => ReferenceEquals(x, y);

        public int GetHashCode(Expression obj) => RuntimeHelpers.GetHashCode(obj);
    }
#endif

    private static string GetCleanMemberName(Expression? expr)
        => expr is null
            ? "<null>"
            : CleanExpressionText(expr.ToString());

    private static string GetIndexArgumentDisplay(Expression indexArg)
    {
        try
        {
            if (indexArg is ConstantExpression constExpr)
            {
                return FormatValue(constExpr.Value);
            }

            // For complex index expressions, just use the expression string
            return CleanExpressionText(indexArg.ToString());
        }
        catch
        {
            return CleanExpressionText(indexArg.ToString());
        }
    }

    private static string FormatValue(object? value)
        => value switch
        {
            null => "null",
            string s => $"\"{s}\"",
            IEnumerable<object> e => $"[{string.Join(", ", e.Select(FormatValue))}]",
            IEnumerable e and not string => $"[{string.Join(", ", e.Cast<object>().Select(FormatValue))}]",
            _ => value.ToString() ?? "<null>",
        };

    private static string CleanExpressionText(string raw)
    {
        // Remove display class names and generated compiler prefixes
        string cleaned = raw;

        // Remove compiler-generated wrappers FIRST, before display class cleanup
        cleaned = RemoveCompilerGeneratedWrappers(cleaned);

        // Handle anonymous types - remove the compiler-generated type wrapper
        cleaned = RemoveAnonymousTypeWrappers(cleaned);

        // Handle list initialization expressions - convert from Add method calls to collection initializer syntax
        cleaned = CleanListInitializers(cleaned);

        // Handle compiler-generated display classes more comprehensively
        // Updated pattern to handle cases with and without parentheses around the display class
        cleaned = CompilerGeneratedDisplayClassRegex().Replace(cleaned, "$1");

        // Remove unnecessary outer parentheses and excessive consecutive parentheses
        cleaned = CleanParentheses(cleaned);

        return cleaned;
    }

    private static string RemoveAnonymousTypeWrappers(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var result = new StringBuilder();
        int i = 0;

        while (i < input.Length)
        {
            // Look for anonymous type pattern: new <>f__AnonymousType followed by generic parameters
            if (i <= input.Length - 4 && input.Substring(i, 4) == "new " &&
                i + 4 < input.Length && input.Substring(i + 4).StartsWith("<>f__AnonymousType", StringComparison.Ordinal))
            {
                // Find the start of the constructor parameters
                int constructorStart = input.IndexOf('(', i + 4);
                if (constructorStart == -1)
                {
                    result.Append(input[i]);
                    i++;
                    continue;
                }

                // Find the matching closing parenthesis
                int parenCount = 1;
                int constructorEnd = constructorStart + 1;
                while (constructorEnd < input.Length && parenCount > 0)
                {
                    if (input[constructorEnd] == '(')
                    {
                        parenCount++;
                    }
                    else if (input[constructorEnd] == ')')
                    {
                        parenCount--;
                    }

                    constructorEnd++;
                }

                if (parenCount == 0)
                {
                    // Extract the content inside the parentheses and wrap with anonymous type notation
                    string content = input.Substring(constructorStart + 1, constructorEnd - constructorStart - 2);
#if NET
                    result.Append(CultureInfo.InvariantCulture, $"new {{ {content} }}");
#else
                    result.Append($"new {{ {content} }}");
#endif
                    i = constructorEnd;
                    continue;
                }
            }

            result.Append(input[i]);
            i++;
        }

        return result.ToString();
    }

    private static string CleanListInitializers(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // Look for list initialization patterns with proper brace matching
        var result = new StringBuilder();
        int i = 0;

        while (i < input.Length)
        {
            // Look for "new List`1() {" or similar collection types
            if (TryMatchListInitPattern(input, i, out string collectionType, out int patternEnd))
            {
                // Find the matching closing brace for the initializer
                int braceStart = patternEnd;
                int braceCount = 1;
                int braceEnd = braceStart + 1;

                while (braceEnd < input.Length && braceCount > 0)
                {
                    if (input[braceEnd] == '{')
                    {
                        braceCount++;
                    }
                    else if (input[braceEnd] == '}')
                    {
                        braceCount--;
                    }

                    braceEnd++;
                }

                if (braceCount == 0)
                {
                    // Extract the content between braces
                    string initContent = input.Substring(braceStart + 1, braceEnd - braceStart - 2);

                    // Extract the generic type parameter and arguments from the Add method calls
                    string addMethodPattern = @"Void\s+Add\([^)]+\)\(([^)]+)\)";
                    MatchCollection addMatches = Regex.Matches(initContent, addMethodPattern);

                    if (addMatches.Count > 0)
                    {
                        // Extract type from the first Add method call
                        string firstAddPattern = @"Void\s+Add\(([^)]+)\)";
                        Match typeMatch = Regex.Match(initContent, firstAddPattern);
                        string genericType = "object"; // default fallback

                        if (typeMatch.Success)
                        {
                            string rawType = typeMatch.Groups[1].Value;
                            // Clean up type names like "Int32" to "int", "String" to "string", etc.
                            genericType = CleanTypeName(rawType);
                        }

                        // Extract all arguments from Add method calls
                        var arguments = new List<string>();
                        foreach (Match addMatch in addMatches)
                        {
                            string argument = addMatch.Groups[1].Value;
                            arguments.Add(argument);
                        }

                        // Construct the cleaned collection initializer
                        string argumentsList = string.Join(", ", arguments);
#if NET
                        result.Append(CultureInfo.InvariantCulture, $"new {collectionType}<{genericType}> {{ {argumentsList} }}");
#else
                        result.Append($"new {collectionType}<{genericType}> {{ {argumentsList} }}");
#endif
                        i = braceEnd;
                        continue;
                    }
                }
            }

            result.Append(input[i]);
            i++;
        }

        return result.ToString();
    }

    private static bool TryMatchListInitPattern(string input, int startIndex, out string collectionType, out int patternEnd)
    {
        collectionType = string.Empty;
        patternEnd = startIndex;

        // Check for "new " at the start
        if (startIndex + 4 >= input.Length || !input.Substring(startIndex, 4).Equals("new ", StringComparison.Ordinal))
        {
            return false;
        }

        int pos = startIndex + 4;

        // Skip whitespace
        while (pos < input.Length && char.IsWhiteSpace(input[pos]))
        {
            pos++;
        }

        // Check for collection type names
        string[] collectionTypes = ["List", "IList", "ICollection", "IEnumerable"];
        string matchedType = string.Empty;

        foreach (string type in collectionTypes)
        {
            if (pos + type.Length < input.Length &&
                input.Substring(pos, type.Length).Equals(type, StringComparison.Ordinal))
            {
                matchedType = type;
                pos += type.Length;
                break;
            }
        }

        if (string.IsNullOrEmpty(matchedType))
        {
            return false;
        }

        // Check for "`1()" pattern
        if (pos + 4 >= input.Length || !input.Substring(pos, 4).Equals("`1()", StringComparison.Ordinal))
        {
            return false;
        }

        pos += 4;

        // Skip whitespace
        while (pos < input.Length && char.IsWhiteSpace(input[pos]))
        {
            pos++;
        }

        // Check for opening brace
        if (pos >= input.Length || input[pos] != '{')
        {
            return false;
        }

        collectionType = matchedType;
        patternEnd = pos;
        return true;
    }

    private static string CleanTypeName(string typeName)
    {
        string cleanedTypeName = typeName switch
        {
            "Int32" => "int",
            "Int64" => "long",
            "Int16" => "short",
            "Byte" => "byte",
            "SByte" => "sbyte",
            "UInt32" => "uint",
            "UInt64" => "ulong",
            "UInt16" => "ushort",
            "Single" => "float",
            "Double" => "double",
            "Decimal" => "decimal",
            "Boolean" => "bool",
            "String" => "string",
            "Char" => "char",
            "Object" => "object",

            // Handle System. prefixed type names
            "System.Int32" => "int",
            "System.Int64" => "long",
            "System.Int16" => "short",
            "System.Byte" => "byte",
            "System.SByte" => "sbyte",
            "System.UInt32" => "uint",
            "System.UInt64" => "ulong",
            "System.UInt16" => "ushort",
            "System.Single" => "float",
            "System.Double" => "double",
            "System.Decimal" => "decimal",
            "System.Boolean" => "bool",
            "System.String" => "string",
            "System.Char" => "char",
            "System.Object" => "object",

            _ => typeName,
        };

        if (cleanedTypeName != typeName)
        {
            return cleanedTypeName;
        }

        string[] nestedSegments = typeName.Split('+');
        for (int i = 0; i < nestedSegments.Length; i++)
        {
            string segment = nestedSegments[i];
            int lastDot = segment.LastIndexOf('.');
            nestedSegments[i] = lastDot >= 0 ? segment.Substring(lastDot + 1) : segment;
        }

        return string.Join(".", nestedSegments);
    }

    private static string CleanParentheses(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        input = input.Trim();
        string previous;

        // Keep removing outer parentheses and cleaning excessive ones until no more changes occur
        do
        {
            previous = input;

            // Remove outer parentheses if they wrap the entire expression
            input = RemoveOuterParentheses(input);

            // Clean excessive consecutive parentheses in a single pass
            input = CleanExcessiveParentheses(input);
        }
        while (input != previous); // Repeat until no more changes

        return input;
    }

    private static string RemoveOuterParentheses(string input)
    {
        if (input.Length < 2 || !input.StartsWith("(", StringComparison.Ordinal) || !input.EndsWith(")", StringComparison.Ordinal))
        {
            return input;
        }

        // Check if the first and last parentheses are truly the outermost pair
        int parenCount = 0;
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '(')
            {
                parenCount++;
            }
            else if (input[i] == ')')
            {
                parenCount--;
                // If we reach 0 before the end, the first paren is not the outermost
                if (parenCount == 0 && i < input.Length - 1)
                {
                    return input;
                }
            }
        }

        // If we get here and parenCount is 0, the outer parens can be removed
        return parenCount == 0 ? input.Substring(1, input.Length - 2).Trim() : input;
    }

    private static string CleanExcessiveParentheses(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var result = new StringBuilder(input.Length);
        int i = 0;

        while (i < input.Length)
        {
            char currentChar = input[i];

            if (currentChar is '(' or ')')
            {
                // Count consecutive identical parentheses
                int count = 1;
                while (i + count < input.Length && input[i + count] == currentChar)
                {
                    count++;
                }

                // Keep at most 2 consecutive parentheses
                int keepCount = Math.Min(count, 2);
                result.Append(currentChar, keepCount);
                i += count;
            }
            else
            {
                result.Append(currentChar);
                i++;
            }
        }

        return result.ToString();
    }

    private static string RemoveCompilerGeneratedWrappers(string input)
    {
        var result = new StringBuilder();
        int i = 0;

        while (i < input.Length)
        {
            if (TryRemoveWrapper(input, ref i, "value(", RemoveCompilerGeneratedWrappers, result) ||
                TryRemoveWrapper(input, ref i, "ArrayLength(", content => RemoveCompilerGeneratedWrappers(content) + ".Length", result))
            {
                continue;
            }

            result.Append(input[i]);
            i++;
        }

        return result.ToString();
    }

    private static bool TryRemoveWrapper(string input, ref int index, string pattern,
        Func<string, string> transform, StringBuilder result)
    {
        if (index > input.Length - pattern.Length ||
            !string.Equals(input.Substring(index, pattern.Length), pattern, StringComparison.Ordinal))
        {
            return false;
        }

        int start = index + pattern.Length;
        int parenCount = 1;
        int i = start;

        // Find matching closing parenthesis
        while (i < input.Length && parenCount > 0)
        {
            if (input[i] == '(')
            {
                parenCount++;
            }
            else if (input[i] == ')')
            {
                parenCount--;
            }

            i++;
        }

        if (parenCount == 0)
        {
            // Extract content and apply transformation
            string content = input.Substring(start, i - start - 1);
            result.Append(transform(content));
            index = i;
            return true;
        }

        // Malformed, don't consume the pattern
        return false;
    }

#if NET
    [GeneratedRegex(@"[A-Za-z0-9_\.]+\+<>c__DisplayClass\d+_\d+\.(\w+(?:\.\w+)*(?:\[[^\]]+\])?)")]
    private static partial Regex CompilerGeneratedDisplayClassRegex();
#else
    private static Regex CompilerGeneratedDisplayClassRegex()
        => new(@"[A-Za-z0-9_\.]+\+<>c__DisplayClass\d+_\d+\.(\w+(?:\.\w+)*(?:\[[^\]]+\])?)", RegexOptions.Compiled);
#endif
}
