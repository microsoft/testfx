// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics.Where(d => FixableDiagnosticIds.Contains(d.Id)))
        {
            if (root.FindNode(diagnostic.Location.SourceSpan) is InvocationExpressionSyntax invocation)
            {
                RegisterStringFormatCodeFix(context, diagnostic, root, invocation);
                await RegisterInterpolatedStringCodeFix(context, diagnostic, root, invocation);
            }
        }
    }

    private static void RegisterStringFormatCodeFix(CodeFixContext context, Diagnostic diagnostic, SyntaxNode root, InvocationExpressionSyntax invocation)
    {
        var codeAction = CodeAction.Create(
            title: CodeFixResources.AvoidAssertFormatParametersUseStringFormat,
            createChangedDocument: _ => CreateStringFormatFixAsync(context.Document, root, invocation),
            equivalenceKey: "StringFormat");

        context.RegisterCodeFix(codeAction, diagnostic);
    }

    private static async Task RegisterInterpolatedStringCodeFix(CodeFixContext context, Diagnostic diagnostic, SyntaxNode root, InvocationExpressionSyntax invocation)
    {
        // Only offer interpolated string fix for simple cases
        if (await CanConvertToInterpolatedStringAsync(context.Document, invocation))
        {
            var codeAction = CodeAction.Create(
                title: CodeFixResources.AvoidAssertFormatParametersUseInterpolatedString,
                createChangedDocument: _ => CreateInterpolatedStringFixAsync(context.Document, root, invocation),
                equivalenceKey: "InterpolatedString");

            context.RegisterCodeFix(codeAction, diagnostic);
        }
    }

    private static async Task<Document> CreateStringFormatFixAsync(Document document, SyntaxNode root, InvocationExpressionSyntax invocation)
    {
        ArgumentListSyntax argumentList = invocation.ArgumentList;
        SeparatedSyntaxList<ArgumentSyntax> arguments = argumentList.Arguments;

        if (arguments.Count < 2)
        {
            return document;
        }

        // Use semantic analysis to find the correct format string and params arguments
        if (!await TryGetFormatParameterPositionsAsync(document, invocation, out int formatIndex, out int paramsStartIndex))
        {
            return document;
        }

        ArgumentSyntax formatArgument = arguments[formatIndex];
        IEnumerable<ArgumentSyntax> paramsArguments = arguments.Skip(paramsStartIndex);

        // Create string.Format call
        InvocationExpressionSyntax stringFormatInvocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("string"),
                SyntaxFactory.IdentifierName("Format")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList<ArgumentSyntax>(
                        new[] { formatArgument }.Concat(paramsArguments))));

        // Replace the format string + params with the string.Format call
        IEnumerable<ArgumentSyntax> newArguments = arguments.Take(formatIndex)
            .Append(SyntaxFactory.Argument(stringFormatInvocation));

        ArgumentListSyntax newArgumentList = argumentList.WithArguments(SyntaxFactory.SeparatedList(newArguments));
        InvocationExpressionSyntax newInvocation = invocation.WithArgumentList(newArgumentList);

        SyntaxNode newRoot = root.ReplaceNode(invocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> CreateInterpolatedStringFixAsync(Document document, SyntaxNode root, InvocationExpressionSyntax invocation)
    {
        ArgumentListSyntax argumentList = invocation.ArgumentList;
        SeparatedSyntaxList<ArgumentSyntax> arguments = argumentList.Arguments;

        if (arguments.Count < 2)
        {
            return document;
        }

        // Use semantic analysis to find the correct format string and params arguments
        if (!await TryGetFormatParameterPositionsAsync(document, invocation, out int formatIndex, out int paramsStartIndex))
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

    private static async Task<bool> CanConvertToInterpolatedStringAsync(Document document, InvocationExpressionSyntax invocation)
    {
        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 2)
        {
            return false;
        }

        // Use semantic analysis to find the correct format string position
        if (!await TryGetFormatParameterPositionsAsync(document, invocation, out int formatIndex, out _))
        {
            return false;
        }

        ArgumentSyntax formatArgument = arguments[formatIndex];

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

    private static async Task<bool> TryGetFormatParameterPositionsAsync(Document document, InvocationExpressionSyntax invocation, out int formatIndex, out int paramsStartIndex)
    {
        formatIndex = -1;
        paramsStartIndex = -1;

        SemanticModel? semanticModel = await document.GetSemanticModelAsync().ConfigureAwait(false);
        if (semanticModel is null)
        {
            return false;
        }

        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
        {
            return false;
        }

        ImmutableArray<IParameterSymbol> parameters = methodSymbol.Parameters;
        if (parameters.Length < 2)
        {
            return false;
        }

        // Find the format string parameter (second-to-last with StringSyntax attribute)
        IParameterSymbol formatParameter = parameters[parameters.Length - 2];
        if (formatParameter.Type?.SpecialType != SpecialType.System_String)
        {
            return false;
        }

        // Find the params parameter (last parameter with params object[])
        IParameterSymbol paramsParameter = parameters[parameters.Length - 1];
        if (!paramsParameter.IsParams || 
            paramsParameter.Type is not IArrayTypeSymbol arrayType ||
            arrayType.ElementType.SpecialType != SpecialType.System_Object)
        {
            return false;
        }

        // Map parameter indices to argument indices
        formatIndex = parameters.Length - 2;
        paramsStartIndex = parameters.Length - 1;

        return true;
    }
}
