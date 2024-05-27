// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class UseAsyncSuffixTestMethodSuppressor : DiagnosticSuppressor
{
    // VSTHRD200: Use Async suffix for async methods
    // https://github.com/microsoft/vs-threading/blob/main/doc/analyzers/VSTHRD200.md
    private const string SuppressedDiagnosticId = "VSTHRD200";

    internal static readonly SuppressionDescriptor Rule =
        new(DiagnosticIds.UseAsyncSuffixTestMethodSuppressorRuleId, SuppressedDiagnosticId, Resources.UseAsyncSuffixTestMethodSuppressorJustification);

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = ImmutableArray.Create(Rule);

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol))
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
            ISymbol? declaredSymbol = semanticModel.GetDeclaredSymbol(node, context.CancellationToken);
            if (declaredSymbol is IMethodSymbol method
                && method.GetAttributes().Any(attr => attr.AttributeClass.Inherits(testMethodAttributeSymbol)))
            {
                context.ReportSuppression(Suppression.Create(Rule, diagnostic));
            }
        }
    }
}
