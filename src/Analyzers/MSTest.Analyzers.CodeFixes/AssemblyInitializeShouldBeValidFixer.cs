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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AssemblyInitializeShouldBeValidFixer))]
[Shared]
public sealed class AssemblyInitializeShouldBeValidFixer : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.AssemblyInitializeShouldBeValidRuleId);

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

        FixtureMethodSignatureChanges fixesToApply = context.Diagnostics.Aggregate(FixtureMethodSignatureChanges.None, (acc, diagnostic) =>
        {
            if (diagnostic.Descriptor == AssemblyInitializeShouldBeValidAnalyzer.StaticRule)
            {
                return acc | FixtureMethodSignatureChanges.MakeStatic;
            }

            if (diagnostic.Descriptor == AssemblyInitializeShouldBeValidAnalyzer.PublicRule)
            {
                return acc | FixtureMethodSignatureChanges.MakePublic;
            }

            if (diagnostic.Descriptor == AssemblyInitializeShouldBeValidAnalyzer.ReturnTypeRule)
            {
                return acc | FixtureMethodSignatureChanges.FixReturnType;
            }

            if (diagnostic.Descriptor == AssemblyInitializeShouldBeValidAnalyzer.NotAsyncVoidRule)
            {
                return acc | FixtureMethodSignatureChanges.FixAsyncVoid;
            }

            if (diagnostic.Descriptor == AssemblyInitializeShouldBeValidAnalyzer.SingleContextParameterRule)
            {
                return acc | FixtureMethodSignatureChanges.AddTestContextParameter;
            }

            if (diagnostic.Descriptor == AssemblyInitializeShouldBeValidAnalyzer.NotGenericRule)
            {
                return acc | FixtureMethodSignatureChanges.RemoveGeneric;
            }

            // return accumulator unchanged, either the action cannot be fixed or it will be fixed by default.
            return acc;
        });

        if (fixesToApply != FixtureMethodSignatureChanges.None)
        {
            // If we have some fixes to apply, we want to ensure the new method signature will have the TestContext parameter.
            fixesToApply |= FixtureMethodSignatureChanges.AddTestContextParameter;

            context.RegisterCodeFix(
                CodeAction.Create(
                    CodeFixResources.AssemblyInitializeShouldBeValidCodeFix,
                    ct => FixtureMethodFixer.FixSignatureAsync(context.Document, root, node, fixesToApply, ct),
                    nameof(CodeFixResources.AssemblyInitializeShouldBeValidCodeFix)),
                context.Diagnostics);
        }
    }
}
