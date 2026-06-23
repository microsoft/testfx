// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace MSTest.Analyzers.Helpers;

/// <summary>
/// Base code fixer for fixture method signature diagnostics.
/// </summary>
public abstract class FixtureMethodSignatureFixerBase : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic rule ID supported by this fixer.
    /// </summary>
    protected abstract string DiagnosticRuleId { get; }

    /// <summary>
    /// Gets a value indicating whether the fixture method should have no parameters.
    /// </summary>
    protected abstract bool IsParameterLess { get; }

    /// <summary>
    /// Gets a value indicating whether the fixture method should be static.
    /// </summary>
    protected abstract bool ShouldBeStatic { get; }

    private ImmutableArray<string> _fixableDiagnosticIds;

    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds
    {
        get
        {
            if (_fixableDiagnosticIds.IsDefault)
            {
                _fixableDiagnosticIds = ImmutableArray.Create(DiagnosticRuleId);
            }

            return _fixableDiagnosticIds;
        }
    }

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        SyntaxNode node = root.FindNode(context.Span);

        if (context.Diagnostics.Any(d => !d.Properties.ContainsKey(DiagnosticDescriptorHelper.CannotFixPropertyKey)))
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    CodeFixResources.FixSignatureCodeFix,
                    ct => FixtureMethodFixer.FixSignatureAsync(context.Document, root, node, IsParameterLess, ShouldBeStatic, ct),
                    GetType().Name),
                context.Diagnostics);
        }
    }
}
