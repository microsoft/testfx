// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Analyzer.Utilities.Extensions;

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
        SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        foreach (Diagnostic diagnostic in context.Diagnostics.Where(d => FixableDiagnosticIds.Contains(d.Id)))
        {
            if (root.FindNode(diagnostic.Location.SourceSpan) is InvocationExpressionSyntax invocation)
            {
                RegisterStringFormatCodeFix(context, diagnostic, root, invocation);
                RegisterInterpolatedStringCodeFix(context, diagnostic, root, invocation);
            }
        }
    }

    private static void RegisterStringFormatCodeFix(CodeFixContext context, Diagnostic diagnostic, SyntaxNode root, InvocationExpressionSyntax invocation)
    {
        var codeAction = CodeAction.Create(
            title: CodeFixResources.AvoidAssertFormatParametersUseStringFormat,
            createChangedDocument: _ => CreateStringFormatFix(context.Document, root, invocation),
            equivalenceKey: "StringFormat");

        context.RegisterCodeFix(codeAction, diagnostic);
    }

    private static void RegisterInterpolatedStringCodeFix(CodeFixContext context, Diagnostic diagnostic, SyntaxNode root, InvocationExpressionSyntax invocation)
    {
        // Only offer interpolated string fix for simple cases
        if (CanConvertToInterpolatedString(invocation))
        {
            var codeAction = CodeAction.Create(
                title: CodeFixResources.AvoidAssertFormatParametersUseInterpolatedString,
                createChangedDocument: _ => CreateInterpolatedStringFix(context.Document, root, invocation),
                equivalenceKey: "InterpolatedString");

            context.RegisterCodeFix(codeAction, diagnostic);
        }
    }

    private static Task<Document> CreateStringFormatFix(Document document, SyntaxNode root, InvocationExpressionSyntax invocation)
    {
        var argumentList = invocation.ArgumentList;
        var arguments = argumentList.Arguments;

        if (arguments.Count < 2)
        {
            return Task.FromResult(document);
        }

        // Find the format string and params arguments
        var formatArgument = arguments[arguments.Count - 2];
        var paramsArguments = arguments.Skip(arguments.Count - 1);

        // Create string.Format call
        var stringFormatInvocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("string"),
                SyntaxFactory.IdentifierName("Format")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList<ArgumentSyntax>(
                        new[] { formatArgument }.Concat(paramsArguments))));

        // Replace the format string + params with the string.Format call
        var newArguments = arguments.Take(arguments.Count - 2)
            .Append(SyntaxFactory.Argument(stringFormatInvocation));

        var newArgumentList = argumentList.WithArguments(SyntaxFactory.SeparatedList(newArguments));
        var newInvocation = invocation.WithArgumentList(newArgumentList);

        var newRoot = root.ReplaceNode(invocation, newInvocation);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static Task<Document> CreateInterpolatedStringFix(Document document, SyntaxNode root, InvocationExpressionSyntax invocation)
    {
        var argumentList = invocation.ArgumentList;
        var arguments = argumentList.Arguments;

        if (arguments.Count < 2)
        {
            return Task.FromResult(document);
        }

        // Find the format string and params arguments
        var formatArgument = arguments[arguments.Count - 2];
        var paramsArguments = arguments.Skip(arguments.Count - 1).ToArray();

        // Try to convert to interpolated string
        if (TryCreateInterpolatedString(formatArgument, paramsArguments, out var interpolatedString))
        {
            // Replace the format string + params with the interpolated string
            var newArguments = arguments.Take(arguments.Count - 2)
                .Append(SyntaxFactory.Argument(interpolatedString));

            var newArgumentList = argumentList.WithArguments(SyntaxFactory.SeparatedList(newArguments));
            var newInvocation = invocation.WithArgumentList(newArgumentList);

            var newRoot = root.ReplaceNode(invocation, newInvocation);
            return Task.FromResult(document.WithSyntaxRoot(newRoot));
        }

        return Task.FromResult(document);
    }

    private static bool CanConvertToInterpolatedString(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 2)
        {
            return false;
        }

        var formatArgument = arguments[arguments.Count - 2];
        
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

        var formatString = literal.Token.ValueText;
        var interpolatedContents = new List<InterpolatedStringContentSyntax>();
        var currentIndex = 0;

        for (int i = 0; i < formatString.Length; i++)
        {
            if (formatString[i] == '{' && i + 1 < formatString.Length)
            {
                // Add text before the placeholder
                if (i > currentIndex)
                {
                    var text = formatString.Substring(currentIndex, i - currentIndex);
                    interpolatedContents.Add(SyntaxFactory.InterpolatedStringText(
                        SyntaxFactory.Token(SyntaxTriviaList.Empty, SyntaxKind.InterpolatedStringTextToken, text, text, SyntaxTriviaList.Empty)));
                }

                // Find the end of the placeholder
                var closeIndex = formatString.IndexOf('}', i + 1);
                if (closeIndex == -1)
                {
                    return false; // Invalid format string
                }

                // Extract the placeholder index
                var placeholder = formatString.Substring(i + 1, closeIndex - i - 1);
                if (int.TryParse(placeholder, out var paramIndex) && paramIndex < paramsArguments.Length)
                {
                    // Create interpolation expression
                    var interpolation = SyntaxFactory.Interpolation(paramsArguments[paramIndex].Expression);
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
            var text = formatString.Substring(currentIndex);
            interpolatedContents.Add(SyntaxFactory.InterpolatedStringText(
                SyntaxFactory.Token(SyntaxTriviaList.Empty, SyntaxKind.InterpolatedStringTextToken, text, text, SyntaxTriviaList.Empty)));
        }

        interpolatedString = SyntaxFactory.InterpolatedStringExpression(
            SyntaxFactory.Token(SyntaxKind.InterpolatedStringStartToken),
            SyntaxFactory.List(interpolatedContents),
            SyntaxFactory.Token(SyntaxKind.InterpolatedStringEndToken));

        return true;
    }
}