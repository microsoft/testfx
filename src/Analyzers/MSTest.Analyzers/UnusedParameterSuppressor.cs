// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0047: <inheritdoc cref="Resources.UnusedParameterSuppressorJustification"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class UnusedParameterSuppressor : DiagnosticSuppressor
{
    // IDE0060: Remove unused parameter 'name' if it is not part of a shipped public API
    // https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide0060
    private const string SuppressedDiagnosticId = "IDE0060";

    public static readonly SuppressionDescriptor Rule =
        new(DiagnosticIds.UnusedParameterSuppressorRuleId, SuppressedDiagnosticId, Resources.UnusedParameterSuppressorJustification);

    /// <inheritdoc />
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyInitializeAttribute, out INamedTypeSymbol? assemblyInitializeAttributeSymbol)
            || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassInitializeAttribute, out INamedTypeSymbol? classInitializeAttributeSymbol)
            || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext, out INamedTypeSymbol? testContextSymbol))
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.ReportedDiagnostics)
        {
            // The diagnostic is reported on the parameter
            if (diagnostic.Location.SourceTree is not { } tree)
            {
                continue;
            }

            SyntaxNode root = tree.GetRoot(context.CancellationToken);
            SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            SemanticModel semanticModel = context.GetSemanticModel(tree);
            ISymbol? declaredSymbol = semanticModel.GetDeclaredSymbol(node, context.CancellationToken);

            if (declaredSymbol is IParameterSymbol parameter
                && SymbolEqualityComparer.Default.Equals(testContextSymbol, parameter.Type)
                && parameter.ContainingSymbol is IMethodSymbol method
                && method.GetAttributes().Any(attr =>
                    SymbolEqualityComparer.Default.Equals(attr.AttributeClass, assemblyInitializeAttributeSymbol) ||
                    SymbolEqualityComparer.Default.Equals(attr.AttributeClass, classInitializeAttributeSymbol)))
            {
                context.ReportSuppression(Suppression.Create(Rule, diagnostic));
            }
        }
    }
}
