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
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers.CodeFixes;

/// <summary>
/// Code fixer for <see cref="CollectionAssertToAssertAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CollectionAssertToAssertFixer))]
[Shared]
public sealed class CollectionAssertToAssertFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.CollectionAssertToAssertRuleId);

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        Diagnostic diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.TryGetValue(CollectionAssertToAssertAnalyzer.ProperAssertMethodNameKey, out string? properAssertMethodName)
            || properAssertMethodName is null
            || !diagnostic.Properties.TryGetValue(CollectionAssertToAssertAnalyzer.FixKindKey, out string? fixKind)
            || fixKind is null)
        {
            return;
        }

        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not InvocationExpressionSyntax invocationExpr)
        {
            return;
        }

        // We only know how to rewrite `<expr>.<member>(...)`-shaped invocations. `using static` and similar
        // shapes fall through without a fix; the diagnostic still surfaces so the user can migrate manually.
        if (invocationExpr.Expression is not MemberAccessExpressionSyntax)
        {
            return;
        }

        string title = string.Format(CultureInfo.InvariantCulture, CodeFixResources.CollectionAssertToAssertTitle, properAssertMethodName);
        var action = CodeAction.Create(
            title: title,
            createChangedDocument: ct => FixCollectionAssertAsync(context.Document, invocationExpr, properAssertMethodName, fixKind, ct),
            equivalenceKey: title);

        context.RegisterCodeFix(action, diagnostic);
    }

    private static async Task<Document> FixCollectionAssertAsync(
        Document document,
        InvocationExpressionSyntax invocationExpr,
        string properAssertMethodName,
        string fixKind,
        CancellationToken cancellationToken)
    {
        if (invocationExpr.Expression is not MemberAccessExpressionSyntax memberAccessExpr)
        {
            return document;
        }

        SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel.GetOperation(invocationExpr, cancellationToken) is not IInvocationOperation invocationOperation)
        {
            return document;
        }

        // Re-order the original arguments according to the source signature's parameter ordinals
        // (handles named arguments) and strip the name colons because the target Assert method's
        // parameter names may differ from CollectionAssert's.
        if (!TryGetArgumentsByOrdinal(invocationOperation, out ArgumentSyntax[]? orderedArguments))
        {
            return document;
        }

        SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        List<ArgumentSyntax> newArguments = BuildNewArguments(orderedArguments!, fixKind);

        ArgumentListSyntax newArgumentList = invocationExpr.ArgumentList.WithArguments(SyntaxFactory.SeparatedList(newArguments));

        // Replace `<anything>.CollectionAssert.<Method>` with `Assert.<ProperMethod>`.
        MemberAccessExpressionSyntax newMemberAccess = memberAccessExpr
            .WithExpression(SyntaxFactory.IdentifierName("Assert"))
            .WithName(SyntaxFactory.IdentifierName(properAssertMethodName));

        InvocationExpressionSyntax newInvocationExpr = invocationExpr
            .WithExpression(newMemberAccess)
            .WithArgumentList(newArgumentList)
            .WithLeadingTrivia(invocationExpr.GetLeadingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);

        return document.WithSyntaxRoot(root.ReplaceNode(invocationExpr, newInvocationExpr));
    }

    private static bool TryGetArgumentsByOrdinal(IInvocationOperation invocationOperation, out ArgumentSyntax[]? orderedArguments)
    {
        int parameterCount = invocationOperation.TargetMethod.Parameters.Length;
        var ordered = new ArgumentSyntax[parameterCount];
        int filled = 0;
        foreach (IArgumentOperation argument in invocationOperation.Arguments)
        {
            if (argument.Parameter is null
                || argument.Parameter.Ordinal < 0
                || argument.Parameter.Ordinal >= parameterCount
                || argument.Syntax is not ArgumentSyntax argumentSyntax)
            {
                orderedArguments = null;
                return false;
            }

            // Strip the name colon because the target Assert overload may have differently-named parameters.
            ordered[argument.Parameter.Ordinal] = argumentSyntax.WithNameColon(null);
            filled++;
        }

        if (filled != parameterCount)
        {
            orderedArguments = null;
            return false;
        }

        orderedArguments = ordered;
        return true;
    }

    private static List<ArgumentSyntax> BuildNewArguments(ArgumentSyntax[] orderedArguments, string fixKind)
    {
        var newArguments = new List<ArgumentSyntax>(orderedArguments.Length + 1);
        switch (fixKind)
        {
            case CollectionAssertToAssertAnalyzer.FixKindSwapTwoArgs:
                newArguments.Add(orderedArguments[1]);
                newArguments.Add(orderedArguments[0]);
                for (int i = 2; i < orderedArguments.Length; i++)
                {
                    newArguments.Add(orderedArguments[i]);
                }

                break;

            case CollectionAssertToAssertAnalyzer.FixKindAddInAnyOrder:
                newArguments.Add(orderedArguments[0]);
                newArguments.Add(orderedArguments[1]);
                newArguments.Add(SyntaxFactory.Argument(CreateInAnyOrderExpression()));
                for (int i = 2; i < orderedArguments.Length; i++)
                {
                    newArguments.Add(orderedArguments[i]);
                }

                break;

            default:
                // FixKindSimple: keep the arguments in their original positional order.
                newArguments.AddRange(orderedArguments);
                break;
        }

        return newArguments;
    }

    // Emit a fully-qualified reference to avoid breaking when the file lacks a using directive
    // for `Microsoft.VisualStudio.TestTools.UnitTesting` (e.g. fully-qualified CollectionAssert calls).
    private static ExpressionSyntax CreateInAnyOrderExpression()
        => SyntaxFactory.ParseExpression("Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder");
}
