// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MSTest.Analyzers;

/// <summary>
/// Code fix provider for CS1503 error when using Properties.Contains("key") instead of Properties.ContainsKey("key") on TestContext.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TestContextPropertiesContainsKeyFixer))]
[Shared]
public sealed class TestContextPropertiesContainsKeyFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create("CS1503");

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        // The node here refers to the ArgumentSyntax representing the argument passed
        // to Contains (e.g., `TestContext.Properties.Contains([|"key"|])`).
        SyntaxNode? node = root.FindNode(context.Span, getInnermostNodeForTie: false);

        // Ensure that we have an invocation on the form Something.Contains(argument)
        if (node is not ArgumentSyntax argumentSyntax ||
            node.Parent is not ArgumentListSyntax argumentListSyntax ||
            argumentListSyntax.Parent is not InvocationExpressionSyntax invocationExpressionSyntax ||
            invocationExpressionSyntax.Expression is not MemberAccessExpressionSyntax memberAccessExpressionSyntax ||
            memberAccessExpressionSyntax.Name is not IdentifierNameSyntax { Identifier.ValueText: "Contains" } containsIdentifier)
        {
            return;
        }

        SemanticModel? semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        // If the passed argument is not a string, we cannot fix it.
        if (semanticModel.GetTypeInfo(argumentSyntax.Expression).Type?.SpecialType != SpecialType.System_String)
        {
            return;
        }

        // It's expected that we have an overload resolution failure, with a candidate symbol referring to Contains(KeyValuePair<string, object>).
        SymbolInfo methodSymbolInfo = semanticModel.GetSymbolInfo(memberAccessExpressionSyntax);
        if (methodSymbolInfo.CandidateReason != CandidateReason.OverloadResolutionFailure ||
            methodSymbolInfo.CandidateSymbols.Length != 1 ||
            !methodSymbolInfo.CandidateSymbols[0].Equals(GetExpectedContainsMethodCandidate(semanticModel.Compilation), SymbolEqualityComparer.Default))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.UseContainsKeyInsteadOfContainsFix,
                createChangedDocument: c => ReplaceContainsWithContainsKeyAsync(context.Document, root, containsIdentifier),
                equivalenceKey: nameof(TestContextPropertiesContainsKeyFixer)),
            context.Diagnostics);
    }

    private static IMethodSymbol? GetExpectedContainsMethodCandidate(Compilation compilation)
    {
        INamedTypeSymbol stringType = compilation.GetSpecialType(SpecialType.System_String);
        INamedTypeSymbol objectType = compilation.GetSpecialType(SpecialType.System_Object);

        INamedTypeSymbol? kvpType = compilation.GetTypeByMetadataName("System.Collections.Generic.KeyValuePair`2")
            ?.Construct(stringType, objectType);
        if (kvpType is null)
        {
            return null;
        }

        INamedTypeSymbol iCollectionType = compilation.GetSpecialType(SpecialType.System_Collections_Generic_ICollection_T).Construct(kvpType);
        ImmutableArray<ISymbol> containsMembers = iCollectionType.GetMembers("Contains");
        return containsMembers.Length == 1 ? containsMembers[0] as IMethodSymbol : null;
    }

    private static Task<Document> ReplaceContainsWithContainsKeyAsync(
        Document document,
        SyntaxNode root,
        IdentifierNameSyntax containsIdentifier)
    {
        SyntaxNode newRoot = root.ReplaceNode(containsIdentifier, containsIdentifier.WithIdentifier(SyntaxFactory.Identifier("ContainsKey")));
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
