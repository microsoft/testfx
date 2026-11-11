// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;
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
        = [DiagnosticIds.CollectionAssertToAssertRuleId];

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Diagnostic diagnostic = context.Diagnostics[0];
        diagnostic.Properties.TryGetValue(AssertToAssertAnalyzerHelpers.ProperAssertMethodNameKey, out string? properAssertMethodName);
        diagnostic.Properties.TryGetValue(CollectionAssertToAssertAnalyzer.FixKindKey, out string? fixKind);
        return IsUnsupportedTypeMigration(properAssertMethodName, fixKind)
            ? Task.CompletedTask
            : AssertToAssertFixerHelpers.RegisterCodeFixAsync(
                context,
                AssertToAssertAnalyzerHelpers.ProperAssertMethodNameKey,
                CodeFixResources.CollectionAssertToAssertTitle,
                CollectionAssertToAssertAnalyzer.FixKindKey,
                FixAssertAsync);
    }

    private static bool IsUnsupportedTypeMigration(string? properAssertMethodName, string? fixKind)
        => properAssertMethodName == "AreAllOfType"
            || fixKind == CollectionAssertToAssertAnalyzer.FixKindInstanceOfType;

    private static Task<Document> FixAssertAsync(
        Document document,
        InvocationExpressionSyntax invocationExpr,
        string properAssertMethodName,
        string? fixKind,
        CancellationToken cancellationToken)
        => fixKind is null
            ? Task.FromResult(document)
            : FixCollectionAssertAsync(document, invocationExpr, properAssertMethodName, fixKind, cancellationToken);

    private static async Task<Document> FixCollectionAssertAsync(
        Document document,
        InvocationExpressionSyntax invocationExpr,
        string properAssertMethodName,
        string fixKind,
        CancellationToken cancellationToken)
    {
        if (IsUnsupportedTypeMigration(properAssertMethodName, fixKind)
            || invocationExpr.Expression is not MemberAccessExpressionSyntax memberAccessExpr)
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

        // A null element must not bind to Assert's predicate overload after the argument swap.
        if (fixKind == CollectionAssertToAssertAnalyzer.FixKindSwapTwoArgs
            && properAssertMethodName is "Contains" or "DoesNotContain"
            && invocationOperation.Arguments.Any(argument => argument.Parameter?.Ordinal == 1
                && argument.Value.ConstantValue.HasValue
                && argument.Value.ConstantValue.Value is null))
        {
            TypeSyntax objectType = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword));
            if ((semanticModel.GetNullableContext(invocationExpr.SpanStart) & NullableContext.AnnotationsEnabled) != 0)
            {
                objectType = SyntaxFactory.NullableType(objectType);
            }

            ExpressionSyntax element = orderedArguments![1].Expression;
            orderedArguments[1] = orderedArguments[1].WithExpression(
                SyntaxFactory.CastExpression(objectType, element.WithoutTrivia()).WithTriviaFrom(element));
        }

        List<ArgumentSyntax> newArguments = BuildNewArguments(orderedArguments!, fixKind);

        ArgumentListSyntax newArgumentList = invocationExpr.ArgumentList.WithArguments(SyntaxFactory.SeparatedList(newArguments));

        // Replace `<qualifier>.CollectionAssert.<Method>` with `<qualifier>.Assert.<ProperMethod>`,
        // preserving any namespace/alias qualifier so we don't accidentally bind to a different
        // `Assert` type (or fail to bind at all) when the source file lacks
        // `using Microsoft.VisualStudio.TestTools.UnitTesting;`.
        ExpressionSyntax newAssertExpression = memberAccessExpr.Expression switch
        {
            // `CollectionAssert.X(...)` (with using directive) → `Assert.X(...)`.
            IdentifierNameSyntax => SyntaxFactory.IdentifierName("Assert"),

            // `Foo.Bar.CollectionAssert.X(...)` or `global::Foo.Bar.CollectionAssert.X(...)` →
            // swap only the trailing `CollectionAssert` identifier for `Assert`.
            MemberAccessExpressionSyntax qualified => qualified.WithName(SyntaxFactory.IdentifierName("Assert")),

            // `SomeAlias::CollectionAssert.X(...)` → `SomeAlias::Assert.X(...)`.
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.WithName(SyntaxFactory.IdentifierName("Assert")),

            _ => SyntaxFactory.IdentifierName("Assert"),
        };

        SimpleNameSyntax newMethodName = SyntaxFactory.IdentifierName(properAssertMethodName);

        MemberAccessExpressionSyntax newMemberAccess = memberAccessExpr
            .WithExpression(newAssertExpression)
            .WithName(newMethodName);

        InvocationExpressionSyntax newInvocationExpr = invocationExpr
            .WithExpression(newMemberAccess)
            .WithArgumentList(newArgumentList)
            .WithLeadingTrivia(invocationExpr.GetLeadingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);

        return await AssertToAssertFixerHelpers.ReplaceInvocationAsync(document, invocationExpr, newInvocationExpr, cancellationToken).ConfigureAwait(false);
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
