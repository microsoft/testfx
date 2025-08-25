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
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers.CodeFixes;

/// <summary>
/// Code fix for MSTEST0053: Avoid using Assert methods with format parameters.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AvoidAssertFormatParametersFixer))]
[Shared]
public sealed class AvoidAssertFormatParametersFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.AvoidAssertFormatParametersRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];
        if (root.FindNode(context.Span, getInnermostNodeForTie: true) is InvocationExpressionSyntax invocation)
        {
            RegisterStringFormatCodeFix(context, diagnostic, root, invocation);
            await RegisterInterpolatedStringCodeFixAsync(context, diagnostic, root, invocation).ConfigureAwait(false);
        }
    }

    private static void RegisterStringFormatCodeFix(CodeFixContext context, Diagnostic diagnostic, SyntaxNode root, InvocationExpressionSyntax invocation)
    {
        var codeAction = CodeAction.Create(
            title: CodeFixResources.AvoidAssertFormatParametersUseStringFormat,
            createChangedDocument: ct => CreateStringFormatFixAsync(context.Document, root, invocation, ct),
            equivalenceKey: $"{nameof(AvoidAssertFormatParametersFixer)}_StringFormat");

        context.RegisterCodeFix(codeAction, diagnostic);
    }

    private static async Task RegisterInterpolatedStringCodeFixAsync(CodeFixContext context, Diagnostic diagnostic, SyntaxNode root, InvocationExpressionSyntax invocation)
    {
        // Only offer interpolated string fix for simple cases
        if (await CanConvertToInterpolatedStringAsync(context.Document, invocation, context.CancellationToken).ConfigureAwait(false))
        {
            var codeAction = CodeAction.Create(
                title: CodeFixResources.AvoidAssertFormatParametersUseInterpolatedString,
                createChangedDocument: ct => CreateInterpolatedStringFixAsync(context.Document, root, invocation, ct),
                equivalenceKey: $"{nameof(AvoidAssertFormatParametersFixer)}_InterpolatedString");

            context.RegisterCodeFix(codeAction, diagnostic);
        }
    }

    private static async Task<Document> CreateStringFormatFixAsync(Document document, SyntaxNode root, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
    {
        ArgumentListSyntax oldArgumentList = invocation.ArgumentList;
        if (oldArgumentList.Arguments.Count < 2)
        {
            return document;
        }

        SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel.GetOperation(invocation, cancellationToken) is not IInvocationOperation invocationOperation)
        {
            return document;
        }

        // Use semantic analysis to find the correct format string and params arguments
        (bool success, int formatIndex, int paramsStartIndex) = TryGetFormatParameterPositions(invocationOperation.TargetMethod);
        if (!success)
        {
            return document;
        }

        IArgumentOperation? paramsArgumentOperation = invocationOperation.Arguments.SingleOrDefault(arg => arg.Parameter?.Ordinal == paramsStartIndex);
        IArgumentOperation? messageArgumentOperation = invocationOperation.Arguments.SingleOrDefault(arg => arg.Parameter?.Ordinal == formatIndex);
        if (paramsArgumentOperation is null || messageArgumentOperation is null)
        {
            return document;
        }

        ArgumentListSyntax newArgumentList = oldArgumentList;
        var formatArgument = (ArgumentSyntax)messageArgumentOperation.Syntax;
        if (paramsArgumentOperation.ArgumentKind == ArgumentKind.ParamArray)
        {
            ImmutableArray<IOperation> elementValues = ((IArrayCreationOperation)paramsArgumentOperation.Value).Initializer!.ElementValues;
            IEnumerable<ArgumentSyntax> paramsArguments = elementValues.Select(e => (ArgumentSyntax)e.Syntax.Parent!);

            InvocationExpressionSyntax stringFormatInvocation = CreateStringFormatCall([formatArgument, .. paramsArguments]);

            newArgumentList = newArgumentList.ReplaceNode(formatArgument, formatArgument.WithExpression(stringFormatInvocation));
            foreach (IOperation element in elementValues.OrderByDescending(e => oldArgumentList.Arguments.IndexOf((ArgumentSyntax)e.Syntax.Parent!)))
            {
                newArgumentList = newArgumentList.WithArguments(newArgumentList.Arguments.RemoveAt(oldArgumentList.Arguments.IndexOf((ArgumentSyntax)element.Syntax.Parent!)));
            }
        }
        else if (paramsArgumentOperation.ArgumentKind == ArgumentKind.Explicit)
        {
            var paramsArgumentSyntax = (ArgumentSyntax)paramsArgumentOperation.Syntax;
            InvocationExpressionSyntax stringFormatInvocation = CreateStringFormatCall([formatArgument, paramsArgumentSyntax]);

            newArgumentList = newArgumentList.ReplaceNode(formatArgument, formatArgument.WithExpression(stringFormatInvocation));
            newArgumentList = newArgumentList.WithArguments(newArgumentList.Arguments.RemoveAt(oldArgumentList.Arguments.IndexOf(paramsArgumentSyntax)));
        }

        return document.WithSyntaxRoot(root.ReplaceNode(oldArgumentList, newArgumentList));

        static InvocationExpressionSyntax CreateStringFormatCall(IEnumerable<ArgumentSyntax> arguments)
            => SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                    SyntaxFactory.IdentifierName("Format")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments)));
    }

    private static async Task<Document> CreateInterpolatedStringFixAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        ArgumentListSyntax argumentList = invocation.ArgumentList;
        SeparatedSyntaxList<ArgumentSyntax> arguments = argumentList.Arguments;

        if (arguments.Count < 2)
        {
            return document;
        }

        // Use semantic analysis to find the correct format string and params arguments
        SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        (bool success, int formatIndex, int paramsStartIndex) = TryGetFormatParameterPositions(semanticModel, invocation, cancellationToken);
        if (!success)
        {
            return document;
        }

        ArgumentSyntax formatArgument = arguments[formatIndex];
        ArgumentSyntax[] paramsArguments = [.. arguments.Skip(paramsStartIndex)];

        // Try to convert to interpolated string
        if (TryCreateInterpolatedString(formatArgument, paramsArguments, out InterpolatedStringExpressionSyntax? interpolatedString))
        {
            // Replace the format string + params with the interpolated string
            IEnumerable<ArgumentSyntax> newArguments = arguments.Take(formatIndex)
                .Append(SyntaxFactory.Argument(interpolatedString));

            ArgumentListSyntax newArgumentList = argumentList.WithArguments(SyntaxFactory.SeparatedList(newArguments));
            InvocationExpressionSyntax newInvocation = invocation.WithArgumentList(newArgumentList);

            SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
            return document.WithSyntaxRoot(newRoot);
        }

        return document;
    }

    private static async Task<bool> CanConvertToInterpolatedStringAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 2)
        {
            return false;
        }

        // Use semantic analysis to find the correct format string position
        SemanticModel semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel.GetOperation(invocation, cancellationToken) is not IInvocationOperation invocationOperation)
        {
            return false;
        }

        (bool success, int formatIndex, int paramsIndex) = TryGetFormatParameterPositions(invocationOperation.TargetMethod);
        if (!success)
        {
            return false;
        }

        if (invocationOperation.Arguments.SingleOrDefault(arg => arg.Parameter?.Ordinal == paramsIndex) is not IArgumentOperation { ArgumentKind: ArgumentKind.ParamArray })
        {
            return false;
        }

        if (invocationOperation.Arguments.SingleOrDefault(arg => arg.Parameter?.Ordinal == formatIndex)?.Syntax is not ArgumentSyntax formatArgument)
        {
            return false;
        }

        // Check if the format string is a simple string literal
        return formatArgument.Expression is LiteralExpressionSyntax literal &&
               literal.Token.IsKind(SyntaxKind.StringLiteralToken);
    }

    private static bool TryCreateInterpolatedString(ArgumentSyntax formatArgument, ArgumentSyntax[] paramsArguments, out InterpolatedStringExpressionSyntax interpolatedString)
    {
        interpolatedString = null!;

        if (formatArgument.Expression is not LiteralExpressionSyntax literal ||
            !literal.Token.IsKind(SyntaxKind.StringLiteralToken))
        {
            return false;
        }

        string formatString = literal.Token.ValueText;
        var interpolatedContents = new List<InterpolatedStringContentSyntax>();
        int currentIndex = 0;

        for (int i = 0; i < formatString.Length; i++)
        {
            if (formatString[i] == '{' && i + 1 < formatString.Length)
            {
                // Add text before the placeholder
                if (i > currentIndex)
                {
                    string text = formatString[currentIndex..i];
                    interpolatedContents.Add(SyntaxFactory.InterpolatedStringText(
                        SyntaxFactory.Token(SyntaxTriviaList.Empty, SyntaxKind.InterpolatedStringTextToken, text, text, SyntaxTriviaList.Empty)));
                }

                // Find the end of the placeholder
                int closeIndex = formatString.IndexOf('}', i + 1);
                if (closeIndex == -1)
                {
                    return false; // Invalid format string
                }

                // Extract the placeholder index
                string placeholder = formatString.Substring(i + 1, closeIndex - i - 1);
                if (int.TryParse(placeholder, out int paramIndex) && paramIndex < paramsArguments.Length)
                {
                    // Create interpolation expression
                    InterpolationSyntax interpolation = SyntaxFactory.Interpolation(paramsArguments[paramIndex].Expression);
                    interpolatedContents.Add(interpolation);
                }
                else
                {
                    return false; // Invalid or out-of-range parameter index
                }

                currentIndex = closeIndex + 1;
                i = closeIndex;
            }
        }

        // Add remaining text
        if (currentIndex < formatString.Length)
        {
            string text = formatString[currentIndex..];
            interpolatedContents.Add(SyntaxFactory.InterpolatedStringText(
                SyntaxFactory.Token(SyntaxTriviaList.Empty, SyntaxKind.InterpolatedStringTextToken, text, text, SyntaxTriviaList.Empty)));
        }

        interpolatedString = SyntaxFactory.InterpolatedStringExpression(
            SyntaxFactory.Token(SyntaxKind.InterpolatedStringStartToken),
            SyntaxFactory.List(interpolatedContents),
            SyntaxFactory.Token(SyntaxKind.InterpolatedStringEndToken));

        return true;
    }

    private static (bool Success, int FormatIndex, int ParamsStartIndex) TryGetFormatParameterPositions(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellationToken);
        return symbolInfo.Symbol is not IMethodSymbol methodSymbol
            ? ((bool Success, int FormatIndex, int ParamsStartIndex))(false, -1, -1)
            : TryGetFormatParameterPositions(methodSymbol);
    }

    private static (bool Success, int FormatIndex, int ParamsStartIndex) TryGetFormatParameterPositions(IMethodSymbol methodSymbol)
    {
        ImmutableArray<IParameterSymbol> parameters = methodSymbol.Parameters;
        if (parameters.Length < 2)
        {
            return (false, -1, -1);
        }

        IParameterSymbol? formatParameter = parameters.SingleOrDefault(p => p.Name == "message" && p.Type.SpecialType == SpecialType.System_String);
        if (formatParameter is null)
        {
            return (false, -1, -1);
        }

        // Map parameter indices to argument indices
        int formatIndex = formatParameter.Ordinal;
        int paramsStartIndex = parameters.Length - 1;

        return (true, formatIndex, paramsStartIndex);
    }
}
