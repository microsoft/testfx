// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AssemblyCleanupShouldBeValidFixer))]
[Shared]
public sealed class AssemblyCleanupShouldBeValidFixer : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.AssemblyCleanupShouldBeValidRuleId);

    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        SyntaxNode node = root.FindNode(context.Span);
        if (node == null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                CodeFixResources.AssemblyCleanupShouldBeValidCodeFix,
                ct => FixSignatureAsync(context.Document, context.Diagnostics, root, node, ct),
                nameof(CodeFixResources.AssemblyCleanupShouldBeValidCodeFix)),
            context.Diagnostics);
    }

    private static Task<Solution> FixSignatureAsync(Document document, ImmutableArray<Diagnostic> diagnostics, SyntaxNode root,
        SyntaxNode node, CancellationToken cancellationToken)
    {
        FixtureMethodSignatureChanges fixesToApply = diagnostics.Aggregate(FixtureMethodSignatureChanges.None, (acc, diagnostic) =>
        {
            if (diagnostic.Descriptor == AssemblyCleanupShouldBeValidAnalyzer.StaticRule)
            {
                return acc | FixtureMethodSignatureChanges.MakeStatic;
            }

            if (diagnostic.Descriptor == AssemblyCleanupShouldBeValidAnalyzer.PublicRule)
            {
                return acc | FixtureMethodSignatureChanges.MakePublic;
            }

            if (diagnostic.Descriptor == AssemblyCleanupShouldBeValidAnalyzer.ReturnTypeRule)
            {
                return acc | FixtureMethodSignatureChanges.FixReturnType;
            }

            if (diagnostic.Descriptor == AssemblyCleanupShouldBeValidAnalyzer.NotAsyncVoidRule)
            {
                return acc | FixtureMethodSignatureChanges.FixAsyncVoid;
            }

            if (diagnostic.Descriptor == AssemblyCleanupShouldBeValidAnalyzer.NoParametersRule)
            {
                return acc | FixtureMethodSignatureChanges.RemoveParameters;
            }

            // return accumulator unchanged, either the action cannot be fixed or it will be fixed by default.
            return acc;
        });

        return FixtureMethodFixer.FixSignatureAsync(document, root, node, fixesToApply, cancellationToken);
    }
}
