// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0071: <inheritdoc cref="Resources.RedundantTestMethodDisplayNameTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class RedundantTestMethodDisplayNameAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.RedundantTestMethodDisplayNameTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.RedundantTestMethodDisplayNameMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.RedundantTestMethodDisplayNameDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.RedundantTestMethodDisplayNameRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol))
            {
                return;
            }

            context.RegisterSymbolAction(
                context => AnalyzeSymbol(context, testMethodAttributeSymbol),
                SymbolKind.Method);
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testMethodAttributeSymbol)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        foreach (AttributeData attribute in methodSymbol.GetAttributes())
        {
            // Only consider attributes that are (or derive from) '[TestMethod]'.
            if (!attribute.AttributeClass.Inherits(testMethodAttributeSymbol))
            {
                continue;
            }

            foreach (KeyValuePair<string, TypedConstant> namedArgument in attribute.NamedArguments)
            {
                if (namedArgument.Key != "DisplayName"
                    || namedArgument.Value.Value is not string displayName
                    || !string.Equals(displayName, methodSymbol.Name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (attribute.ApplicationSyntaxReference is { } syntaxReference)
                {
                    context.ReportDiagnostic(syntaxReference.CreateDiagnostic(Rule, context.CancellationToken, methodSymbol.Name));
                }
                else
                {
                    context.ReportDiagnostic(methodSymbol.CreateDiagnostic(Rule, methodSymbol.Name));
                }
            }
        }
    }
}
