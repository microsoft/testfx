// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0033: <inheritdoc cref="Resources.DoNotDuplicateTestMethodTitle"/>.
/// Detects test methods with different names but identical or very similar implementations.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class DoNotDuplicateTestMethodAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.DoNotDuplicateTestMethodTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.DoNotDuplicateTestMethodMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor DoNotDuplicateTestMethodRule = DiagnosticDescriptorHelper.Create(
    DiagnosticIds.DoNotDuplicateTestMethodRuleId,
    Title,
    MessageFormat,
    null,
    Category.Design,
    DiagnosticSeverity.Warning,
    isEnabledByDefault: true,
    customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// Gets the diagnostic descriptors supported by this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(DoNotDuplicateTestMethodRule);

    /// <summary>
    /// Initializes the analyzer by registering actions to analyze test classes for duplicate method implementations.
    /// </summary>
    /// <param name="context">The analysis context to register actions with.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            if (compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol)
                && compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol))
            {
                ConcurrentBag<(IMethodSymbol Symbol, SyntaxNode Syntax)> testMethodsFound = [];

                compilationContext.RegisterSymbolAction(
                    context => CollectTestMethod(context, testClassAttributeSymbol, testMethodAttributeSymbol, testMethodsFound),
                    SymbolKind.Method);

                compilationContext.RegisterCompilationEndAction(
                    context => AnalyzeCollectedTestMethods(context, testMethodsFound));
            }
        });
    }

    private static bool IsTestMethod(IMethodSymbol method, INamedTypeSymbol testMethodAttributeSymbol) =>
    method.GetAttributes().Any(attr =>
    {
        if (attr.AttributeClass == null)
        {
            return false;
        }

        INamedTypeSymbol? currentType = attr.AttributeClass;
        while (currentType != null)
        {
            if (SymbolEqualityComparer.Default.Equals(currentType, testMethodAttributeSymbol))
            {
                return true;
            }

            currentType = currentType.BaseType;
        }

        return false;
    });

    private static SyntaxNode? GetMethodBody(SyntaxNode methodNode)
    {
        // Try block body first
        System.Reflection.PropertyInfo? bodyProperty = methodNode.GetType().GetProperty("Body");
        object? body = bodyProperty?.GetValue(methodNode);
        if (body != null)
        {
            return body as SyntaxNode;
        }

        // Try expression body
        System.Reflection.PropertyInfo? expressionBodyProperty = methodNode.GetType().GetProperty("ExpressionBody");
        object? exprBody = expressionBodyProperty?.GetValue(methodNode);
        if (exprBody != null)
        {
            // Return the whole ArrowExpressionClauseSyntax, not just the Expression
            return exprBody as SyntaxNode;
        }

        // For VB methods, try Statements
        return methodNode.GetType().GetProperty("Statements") is not null ? methodNode : null;
    }

    private static double CalculateSimilarity(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) && string.IsNullOrEmpty(str2))
        {
            return 1.0;
        }

        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
        {
            return 0.0;
        }

        // Use Levenshtein distance for methods similarity calculation
        int distance = LevenshteinDistance(str1, str2);
        int maxLength = Math.Max(str1.Length, str2.Length);

        return 1.0 - ((double)distance / maxLength);
    }

    private static int LevenshteinDistance(string s1, string s2)
    {
        int[,] d = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
        {
            d[i, 0] = i;
        }

        for (int j = 0; j <= s2.Length; j++)
        {
            d[0, j] = j;
        }

        for (int j = 1; j <= s2.Length; j++)
        {
            for (int i = 1; i <= s1.Length; i++)
            {
                int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[s1.Length, s2.Length];
    }

    private static void CollectTestMethod(
         SymbolAnalysisContext context,
         INamedTypeSymbol testClassAttributeSymbol,
         INamedTypeSymbol testMethodAttributeSymbol,
         ConcurrentBag<(IMethodSymbol Symbol, SyntaxNode Syntax)> testMethodsFound)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        if (!IsTestMethod(methodSymbol, testMethodAttributeSymbol))
        {
            return;
        }

        // Check if the containing type is a test class
        if (methodSymbol.ContainingType != null &&
            methodSymbol.ContainingType.GetAttributes().Any(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, testClassAttributeSymbol)))
        {
            // Get the syntax node
            SyntaxReference? syntaxRef = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef != null)
            {
                SyntaxNode syntax = syntaxRef.GetSyntax(context.CancellationToken);
                testMethodsFound.Add((methodSymbol, syntax));
            }
        }
    }

    private static void AnalyzeCollectedTestMethods(
    CompilationAnalysisContext context,
    ConcurrentBag<(IMethodSymbol Symbol, SyntaxNode Syntax)> testMethodsFound)
    {
        // Group by containing type
        var methodsByType = testMethodsFound
            .GroupBy(m => m.Symbol.ContainingType, SymbolEqualityComparer.Default)
            .ToList();

        foreach (IGrouping<ISymbol?, (IMethodSymbol Symbol, SyntaxNode Syntax)> typeGroup in methodsByType)
        {
            var methods = typeGroup
                .OrderBy(m => m.Symbol.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? string.Empty)
                .ThenBy(m => m.Symbol.Locations.FirstOrDefault()?.SourceSpan.Start ?? int.MaxValue)
                .ToList();

            // Compare each pair of test methods for duplicate implementations
            for (int i = 0; i < methods.Count; i++)
            {
                for (int j = i + 1; j < methods.Count; j++)
                {
                    (IMethodSymbol Symbol, SyntaxNode Syntax) method1 = methods[i];
                    (IMethodSymbol Symbol, SyntaxNode Syntax) method2 = methods[j];

                    // Skip if methods have the same name
                    if (method1.Symbol.Name == method2.Symbol.Name)
                    {
                        continue;
                    }

                    // Compare method implementations (without semantic model)
                    if (AreMethodBodiesSimilar(method1.Syntax, method2.Syntax))
                    {
                        Location? location = method2.Symbol.Locations.FirstOrDefault();
                        if (location != null)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                DoNotDuplicateTestMethodRule,
                                location,
                                method2.Symbol.Name,
                                method1.Symbol.Name));
                        }
                    }
                }
            }
        }
    }

    private static bool AreMethodBodiesSimilar(SyntaxNode method1, SyntaxNode method2)
    {
        string normalizedBody1 = NormalizeMethodBodyText(method1);
        string normalizedBody2 = NormalizeMethodBodyText(method2);

        if (string.IsNullOrWhiteSpace(normalizedBody1) || string.IsNullOrWhiteSpace(normalizedBody2))
        {
            return false;
        }

        if (normalizedBody1 == normalizedBody2)
        {
            return true;
        }

        double similarity = CalculateSimilarity(normalizedBody1, normalizedBody2);
        return similarity > 0.995;
    }

    private static string NormalizeMethodBodyText(SyntaxNode methodNode)
    {
        SyntaxNode? body = GetMethodBody(methodNode);
        if (body == null)
        {
            return string.Empty;
        }

        // Simple text-based normalization without semantic analysis
        var sb = new StringBuilder();
        foreach (SyntaxToken token in body.DescendantTokens())
        {
            // Just get the token text
            sb.Append(token.Text);
            sb.Append(' ');
        }

        return sb.ToString().Trim();
    }
}
