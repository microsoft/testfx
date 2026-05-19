// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="AvoidAssertAreEqualOnCollectionsAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AvoidAssertAreEqualOnCollectionsFixer))]
[Shared]
public sealed class AvoidAssertAreEqualOnCollectionsFixer : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.AvoidAssertAreEqualOnCollectionsRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];
        SyntaxNode diagnosticNode = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        if (diagnosticNode is not InvocationExpressionSyntax invocation)
        {
            Debug.Fail($"Is this an interesting scenario where IInvocationOperation for Assert call isn't associated with InvocationExpressionSyntax? SyntaxNode type: '{diagnosticNode.GetType()}', Text: '{diagnosticNode.GetText()}'");
            return;
        }

        SemanticModel semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel.GetOperation(invocation, context.CancellationToken) is not IInvocationOperation invocationOperation ||
            !TryCreateReplacementContext(invocationOperation, semanticModel, out ReplacementContext replacementContext))
        {
            return;
        }

        if (replacementContext.CanOfferSequenceFixes)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: string.Format(CultureInfo.InvariantCulture, CodeFixResources.AvoidAssertAreEqualOnCollectionsOrderedFix, replacementContext.SequenceMethodName),
                    ct => ApplyFixAsync(context.Document, invocation, replacementContext, preserveGenericTypeArguments: false, addInAnyOrderArgument: false, useEquivalentMethod: false, ct),
                    equivalenceKey: $"{nameof(AvoidAssertAreEqualOnCollectionsFixer)}.Ordered"),
                diagnostic);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: string.Format(CultureInfo.InvariantCulture, CodeFixResources.AvoidAssertAreEqualOnCollectionsInAnyOrderFix, replacementContext.SequenceMethodName),
                    ct => ApplyFixAsync(context.Document, invocation, replacementContext, preserveGenericTypeArguments: false, addInAnyOrderArgument: true, useEquivalentMethod: false, ct),
                    equivalenceKey: $"{nameof(AvoidAssertAreEqualOnCollectionsFixer)}.InAnyOrder"),
                diagnostic);
        }

        if (replacementContext.CanOfferEquivalentFix)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: string.Format(CultureInfo.InvariantCulture, CodeFixResources.AvoidAssertAreEqualOnCollectionsEquivalentFix, replacementContext.EquivalentMethodName),
                    ct => ApplyFixAsync(context.Document, invocation, replacementContext, preserveGenericTypeArguments: true, addInAnyOrderArgument: false, useEquivalentMethod: true, ct),
                    equivalenceKey: $"{nameof(AvoidAssertAreEqualOnCollectionsFixer)}.Equivalent"),
                diagnostic);
        }
    }

    private static bool TryCreateReplacementContext(IInvocationOperation invocationOperation, SemanticModel semanticModel, out ReplacementContext replacementContext)
    {
        IArgumentOperation? expectedArgumentOperation = invocationOperation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 0);
        IArgumentOperation? actualArgumentOperation = invocationOperation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 1);
        if (expectedArgumentOperation?.Syntax is not ArgumentSyntax expectedArgument ||
            actualArgumentOperation?.Syntax is not ArgumentSyntax actualArgument)
        {
            replacementContext = default;
            return false;
        }

        IArgumentOperation? comparerArgumentOperation = invocationOperation.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "comparer");
        IArgumentOperation? messageArgumentOperation = invocationOperation.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "message");
        ArgumentSyntax? comparerArgument = comparerArgumentOperation?.Syntax as ArgumentSyntax;
        ArgumentSyntax? messageArgument = messageArgumentOperation?.Syntax as ArgumentSyntax;

        bool isAreEqual = invocationOperation.TargetMethod.Name == "AreEqual";
        bool canOfferEquivalentFix = comparerArgument is null;
        bool canOfferSequenceFixes = comparerArgument is null;

        replacementContext = new ReplacementContext(
            expectedArgument,
            actualArgument,
            comparerArgument,
            messageArgument,
            isAreEqual ? "AreSequenceEqual" : "AreNotSequenceEqual",
            isAreEqual ? "AreEquivalent" : "AreNotEquivalent",
            canOfferSequenceFixes,
            canOfferEquivalentFix);
        return true;
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        ReplacementContext replacementContext,
        bool preserveGenericTypeArguments,
        bool addInAnyOrderArgument,
        bool useEquivalentMethod,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        string replacementMethodName = useEquivalentMethod ? replacementContext.EquivalentMethodName : replacementContext.SequenceMethodName;
        ExpressionSyntax newExpression = ReplaceMethodName(invocation.Expression, replacementMethodName, preserveGenericTypeArguments);

        var arguments = new List<ArgumentSyntax>
        {
            StripArgumentName(replacementContext.ExpectedArgument),
            StripArgumentName(replacementContext.ActualArgument),
        };

        if (!useEquivalentMethod && replacementContext.ComparerArgument is not null)
        {
            arguments.Add(StripArgumentName(replacementContext.ComparerArgument));
        }

        if (addInAnyOrderArgument)
        {
            arguments.Add(SyntaxFactory.Argument(CreateInAnyOrderExpression()));
        }

        if (replacementContext.MessageArgument is not null)
        {
            arguments.Add(StripArgumentName(replacementContext.MessageArgument));
        }

        InvocationExpressionSyntax newInvocation = invocation
            .WithExpression(newExpression)
            .WithArgumentList(invocation.ArgumentList.WithArguments(SyntaxFactory.SeparatedList(arguments)))
            .WithAdditionalAnnotations(Formatter.Annotation);

        editor.ReplaceNode(invocation, newInvocation);
        return editor.GetChangedDocument();
    }

    private static ExpressionSyntax ReplaceMethodName(ExpressionSyntax expression, string methodName, bool preserveGenericTypeArguments)
        => expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.WithName(ReplaceMethodName(memberAccess.Name, methodName, preserveGenericTypeArguments)),
            SimpleNameSyntax simpleName => ReplaceMethodName(simpleName, methodName, preserveGenericTypeArguments),
            _ => expression,
        };

    private static SimpleNameSyntax ReplaceMethodName(SimpleNameSyntax simpleName, string methodName, bool preserveGenericTypeArguments)
        => preserveGenericTypeArguments && simpleName is GenericNameSyntax genericName
            ? genericName.WithIdentifier(SyntaxFactory.Identifier(methodName))
            : SyntaxFactory.IdentifierName(methodName);

    private static ArgumentSyntax StripArgumentName(ArgumentSyntax argument)
        => argument.WithNameColon(null);

    private static ExpressionSyntax CreateInAnyOrderExpression()
        => SyntaxFactory.ParseExpression("Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder");

    private readonly record struct ReplacementContext(
        ArgumentSyntax ExpectedArgument,
        ArgumentSyntax ActualArgument,
        ArgumentSyntax? ComparerArgument,
        ArgumentSyntax? MessageArgument,
        string SequenceMethodName,
        string EquivalentMethodName,
        bool CanOfferSequenceFixes,
        bool CanOfferEquivalentFix);
}
