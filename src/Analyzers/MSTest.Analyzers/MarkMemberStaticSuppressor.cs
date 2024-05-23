// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class MarkMemberStaticSuppressor : DiagnosticSuppressor
{
    internal static readonly SuppressionDescriptor Rule = new(
        DiagnosticIds.TestMemberCanBeStaticSuppressorId,
        // https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1822
        "CA1822",
        Resources.MarkMemberStaticSuppressorJustification);

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = ImmutableArray.Create(Rule);

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol)
            || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestInitializeAttribute, out INamedTypeSymbol? testInitializeAttributeSymbol)
            || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestCleanupAttribute, out INamedTypeSymbol? testCleanupAttributeSymbol))
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.ReportedDiagnostics)
        {
            // The diagnostic is reported on the test method
            if (diagnostic.Location.SourceTree is not { } tree)
            {
                continue;
            }

            SyntaxNode root = tree.GetRoot(context.CancellationToken);
            SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            SemanticModel semanticModel = context.GetSemanticModel(tree);
            if (semanticModel.GetDeclaredSymbol(node, context.CancellationToken) is not IMethodSymbol declaredMethodSymbol)
            {
                return;
            }

            foreach (AttributeData attribute in declaredMethodSymbol.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, testInitializeAttributeSymbol)
                    || SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, testCleanupAttributeSymbol)
                    || attribute.AttributeClass.Inherits(testMethodAttributeSymbol))
                {
                    context.ReportSuppression(Suppression.Create(Rule, diagnostic));
                    break;
                }
            }
        }
    }
}
