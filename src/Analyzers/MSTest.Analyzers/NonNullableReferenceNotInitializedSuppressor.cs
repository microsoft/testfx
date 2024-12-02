// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0028: <inheritdoc cref="Resources.UseAsyncSuffixTestFixtureMethodSuppressorJustification"/>.
/// </summary>
#pragma warning disable RS1004 // Recommend adding language support to diagnostic analyzer - This suppressor is not valid for VB
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1004 // Recommend adding language support to diagnostic analyzer
public sealed class NonNullableReferenceNotInitializedSuppressor : DiagnosticSuppressor
{
    // CS8618: Non-nullable variable must contain a non-null value when exiting constructor. Consider declaring it as nullable.
    // https://learn.microsoft.com/dotnet/csharp/language-reference/compiler-messages/nullable-warnings?f1url=%3FappId%3Droslyn%26k%3Dk(CS8618)#nonnullable-reference-not-initialized
    private const string SuppressedDiagnosticId = "CS8618";

    internal static readonly SuppressionDescriptor Rule =
        new(DiagnosticIds.NonNullableReferenceNotInitializedSuppressorRuleId, SuppressedDiagnosticId, Resources.UseAsyncSuffixTestFixtureMethodSuppressorJustification);

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = ImmutableArray.Create(Rule);

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext, out INamedTypeSymbol? testContextSymbol)
            || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol))
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
            if (declaredSymbol is IPropertySymbol property
                && string.Equals(property.Name, "TestContext", StringComparison.OrdinalIgnoreCase)
                && SymbolEqualityComparer.Default.Equals(testContextSymbol, property.GetMethod?.ReturnType)
                && property.ContainingType.GetAttributes().Any(attr => attr.AttributeClass.Inherits(testClassAttributeSymbol)))
            {
                context.ReportSuppression(Suppression.Create(Rule, diagnostic));
            }
        }
    }
}
