// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0056: <inheritdoc cref="Resources.EmptyTestMethodTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptyTestMethodAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.EmptyTestMethodTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.EmptyTestMethodDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.EmptyTestMethodMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor EmptyTestMethodRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.EmptyTestMethodRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(EmptyTestMethodRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol))
            {
                INamedTypeSymbol? dataTestMethodAttributeSymbol = context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDataTestMethodAttribute, out INamedTypeSymbol? dataTestMethod) ? dataTestMethod : null;

                context.RegisterSyntaxNodeAction(
                    context => AnalyzeMethodDeclaration(context, testMethodAttributeSymbol, dataTestMethodAttributeSymbol),
                    SyntaxKind.MethodDeclaration);
            }
        });
    }

    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context, INamedTypeSymbol testMethodAttributeSymbol, INamedTypeSymbol? dataTestMethodAttributeSymbol)
    {
        MethodDeclarationSyntax methodDeclaration = (MethodDeclarationSyntax)context.Node;
        IMethodSymbol? methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);

        if (methodSymbol == null)
        {
            return;
        }

        // Check if method has [TestMethod] or [DataTestMethod] attribute
        bool isTestMethod = methodSymbol.HasAttribute(testMethodAttributeSymbol);
        bool isDataTestMethod = dataTestMethodAttributeSymbol != null && methodSymbol.HasAttribute(dataTestMethodAttributeSymbol);

        if (!isTestMethod && !isDataTestMethod)
        {
            return;
        }

        // Check if method body is empty
        if (IsMethodBodyEmpty(methodDeclaration))
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(EmptyTestMethodRule, methodSymbol.Name));
        }
    }

    private static bool IsMethodBodyEmpty(MethodDeclarationSyntax methodDeclaration)
    {
        if (methodDeclaration.Body != null)
        {
            // Method has a block body - check if it contains any statements
            return methodDeclaration.Body.Statements.Count == 0;
        }

        if (methodDeclaration.ExpressionBody != null)
        {
            // Method has an expression body - it's not empty
            return false;
        }

        // Method is abstract or interface method - not empty for our purposes
        return false;
    }
}
