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
using Microsoft.CodeAnalysis.Text;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AvoidExpectedExceptionAttributeFixer))]
[Shared]
public sealed class AvoidExpectedExceptionAttributeFixer : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.AvoidExpectedExceptionAttributeRuleId);

    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        SyntaxToken syntaxToken = root.FindToken(diagnosticSpan.Start);
        if (syntaxToken.Parent is null)
        {
            return;
        }

        if (diagnostic.Properties.ContainsKey(DiagnosticDescriptorHelper.CannotFixPropertyKey))
        {
            return;
        }

        // Find the method declaration identified by the diagnostic.
        MethodDeclarationSyntax methodDeclaration = syntaxToken.Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().First();
        SemanticModel semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("SemanticModel cannot be null.");

        if (!semanticModel.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingExpectedExceptionAttribute, out INamedTypeSymbol? expectedExceptionAttributeSymbol))
        {
            return;
        }

        IMethodSymbol? methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken);
        if (methodSymbol is null)
        {
            return;
        }

        AttributeData? attribute = methodSymbol.GetAttributes().FirstOrDefault(
            attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, expectedExceptionAttributeSymbol));

        if (attribute?.ApplicationSyntaxReference is not { } syntaxRef)
        {
            return;
        }

        if (await syntaxRef.GetSyntaxAsync(context.CancellationToken).ConfigureAwait(false) is not { } attributeSyntax)
        {
            return;
        }

        TypedConstant exceptionTypeArgument = attribute.ConstructorArguments.Where(a => a.Kind == TypedConstantKind.Type).FirstOrDefault();
        if (exceptionTypeArgument.Value is not ITypeSymbol exceptionTypeSymbol)
        {
            return;
        }

        // Register a code action that will invoke the fix.
        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.UseAssertThrowsExceptionOnLastStatementFix,
                createChangedDocument: c => WrapLastStatementWithAssertThrowsExceptionAsync(context.Document, methodDeclaration, attributeSyntax, exceptionTypeSymbol, c),
                equivalenceKey: nameof(AvoidExpectedExceptionAttributeFixer)),
            diagnostic);
    }

    private static async Task<Document> WrapLastStatementWithAssertThrowsExceptionAsync(
        Document document,
        MethodDeclarationSyntax methodDeclaration,
        SyntaxNode attributeSyntax,
        ITypeSymbol exceptionTypeSymbol,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        editor.RemoveNode(attributeSyntax);

        SyntaxNode? oldStatement = (SyntaxNode?)methodDeclaration.Body?.Statements.LastOrDefault() ?? methodDeclaration.ExpressionBody?.Expression;
        if (oldStatement is null)
        {
            return editor.GetChangedDocument();
        }

        SyntaxNode newLambdaExpression = oldStatement switch
        {
            ExpressionStatementSyntax oldLambdaExpression => oldLambdaExpression.Expression,
            _ => oldStatement,
        };

        SyntaxGenerator generator = editor.Generator;
        newLambdaExpression = generator.VoidReturningLambdaExpression(newLambdaExpression);

        bool containsAsyncCode = newLambdaExpression.DescendantNodesAndSelf().Any(n => n is AwaitExpressionSyntax);
        if (containsAsyncCode)
        {
            newLambdaExpression = ((LambdaExpressionSyntax)newLambdaExpression).WithAsyncKeyword(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
        }

        SyntaxNode newStatement = generator.InvocationExpression(
                generator.MemberAccessExpression(
                    generator.IdentifierName("Assert"),
                    generator.GenericName(containsAsyncCode ? "ThrowsExceptionAsync" : "ThrowsException", [exceptionTypeSymbol])),
                newLambdaExpression);

        if (containsAsyncCode)
        {
            newStatement = generator.AwaitExpression(newStatement);
        }

        if (methodDeclaration.Body is not null)
        {
            // For block bodies, we need to wrap the invocation (or the await expression) in expression statement. Otherwise, we shouldn't.
            newStatement = generator.ExpressionStatement(newStatement);
        }

        editor.ReplaceNode(oldStatement, newStatement);
        return editor.GetChangedDocument();
    }
}
