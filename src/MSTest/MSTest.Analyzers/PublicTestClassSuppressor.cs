// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0091: <inheritdoc cref="Resources.PublicTestClassSuppressorJustification"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class PublicTestClassSuppressor : DiagnosticSuppressor
{
    // CA1515: Consider making public types internal.
    private const string SuppressedDiagnosticId = "CA1515";

    /// <inheritdoc cref="Resources.PublicTestClassSuppressorJustification"/>
    public static readonly SuppressionDescriptor Rule =
        new(DiagnosticIds.PublicTestClassSuppressorRuleId, SuppressedDiagnosticId, Resources.PublicTestClassSuppressorJustification);

    /// <inheritdoc />
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = [Rule];

    /// <inheritdoc />
    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol))
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.ReportedDiagnostics)
        {
            if (diagnostic.Location.SourceTree is not { } tree)
            {
                continue;
            }

            SyntaxNode root = tree.GetRoot(context.CancellationToken);
            SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            SemanticModel semanticModel = context.GetSemanticModel(tree);

            if (semanticModel.GetDeclaredSymbol(node, context.CancellationToken) is INamedTypeSymbol { TypeKind: TypeKind.Class } type
                && type.IsTestClass(testClassAttributeSymbol))
            {
                context.ReportSuppression(Suppression.Create(Rule, diagnostic));
            }
        }
    }
}
