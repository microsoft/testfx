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
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="FlowTestContextCancellationTokenAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FlowTestContextCancellationTokenFixer))]
[Shared]
public sealed class FlowTestContextCancellationTokenFixer : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.FlowTestContextCancellationTokenRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // Use custom FixAllProvider to handle adding TestContext property when needed
        => FlowTestContextCancellationTokenFixAllProvider.Instance;

    /// <inheritdoc />
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the invocation expression identified by the diagnostic
        SyntaxNode node = root.FindNode(diagnosticSpan, getInnermostNodeForTie: true);
        if (node is not InvocationExpressionSyntax invocationExpression)
        {
            return;
        }

        diagnostic.Properties.TryGetValue(FlowTestContextCancellationTokenAnalyzer.TestContextMemberNamePropertyKey, out string? testContextMemberName);
        diagnostic.Properties.TryGetValue(FlowTestContextCancellationTokenAnalyzer.CancellationTokenParameterNamePropertyKey, out string? cancellationTokenParameterName);
        diagnostic.Properties.TryGetValue(nameof(FlowTestContextCancellationTokenAnalyzer.TestContextState), out string? testContextState);

        // Register a code action that will invoke the fix
        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.PassCancellationTokenFix,
                createChangedDocument: async c =>
                {
                    DocumentEditor editor = await DocumentEditor.CreateAsync(context.Document, context.CancellationToken).ConfigureAwait(false);
                    return ApplyFix(editor, invocationExpression, testContextMemberName, testContextState, cancellationTokenParameterName, adjustedSymbols: null, c);
                },
                equivalenceKey: nameof(FlowTestContextCancellationTokenFixer)),
            diagnostic);
    }

    internal static Document ApplyFix(
        DocumentEditor editor,
        InvocationExpressionSyntax invocationExpression,
        string? testContextMemberName,
        string? testContextState,
        string? cancellationTokenParameterName,
        HashSet<ISymbol>? adjustedSymbols,
        CancellationToken cancellationToken)
    {
        if (testContextState == nameof(FlowTestContextCancellationTokenAnalyzer.TestContextState.CouldBeInScopeAsProperty))
        {
            Debug.Assert(testContextMemberName is null, "TestContext member name should be null when state is CouldBeInScopeAsProperty");
            AddCancellationTokenArgument(editor, invocationExpression, "TestContext", cancellationTokenParameterName);
            TypeDeclarationSyntax? containingTypeDeclaration = invocationExpression.FirstAncestorOrSelf<TypeDeclarationSyntax>();
            if (containingTypeDeclaration is not null)
            {
                // adjustedSymbols is null meaning we are only applying a single fix (in that case we add the property).
                // If we are in fix all, we then verify if a previous fix has already added the property.
                // We only add the property if it wasn't added by a previous fix.
                // NOTE: We don't expect GetDeclaredSymbol to return null, but if it did (e.g, error scenario), we add the property.
                if (adjustedSymbols is null ||
                    editor.SemanticModel.GetDeclaredSymbol(containingTypeDeclaration, cancellationToken) is not { } symbol ||
                    adjustedSymbols.Add(symbol))
                {
                    editor.ReplaceNode(containingTypeDeclaration, (containingTypeDeclaration, _) => AddTestContextProperty((TypeDeclarationSyntax)containingTypeDeclaration));
                }
            }
        }
        else if (testContextState == nameof(FlowTestContextCancellationTokenAnalyzer.TestContextState.CouldBeInScopeAsParameter))
        {
            Debug.Assert(testContextMemberName is null, "TestContext member name should be null when state is CouldBeInScopeAsParameter");
            AddCancellationTokenArgument(editor, invocationExpression, "testContext", cancellationTokenParameterName);
            MethodDeclarationSyntax? containingMethodDeclaration = invocationExpression.FirstAncestorOrSelf<MethodDeclarationSyntax>();

            if (containingMethodDeclaration is not null)
            {
                // adjustedSymbols is null meaning we are only applying a single fix (in that case we add the parameter).
                // If we are in fix all, we then verify if a previous fix has already added the parameter.
                // We only add the parameter if it wasn't added by a previous fix.
                // NOTE: We don't expect GetDeclaredSymbol to return null, but if it did (e.g, error scenario), we add the property.
                if (adjustedSymbols is null ||
                    editor.SemanticModel.GetDeclaredSymbol(containingMethodDeclaration, cancellationToken) is not { } symbol ||
                    adjustedSymbols.Add(symbol))
                {
                    editor.ReplaceNode(containingMethodDeclaration, (containingMethodDeclaration, _) => AddTestContextParameterToMethod((MethodDeclarationSyntax)containingMethodDeclaration));
                }
            }
        }
        else
        {
            Guard.NotNull(testContextMemberName);
            AddCancellationTokenArgument(editor, invocationExpression, testContextMemberName, cancellationTokenParameterName);
        }

        return editor.GetChangedDocument();
    }

    internal static void AddCancellationTokenArgument(
        DocumentEditor editor,
        InvocationExpressionSyntax invocationExpression,
        string testContextMemberName,
        string? cancellationTokenParameterName)
    {
        // Find the containing method to determine the context
        MethodDeclarationSyntax? containingMethod = invocationExpression.FirstAncestorOrSelf<MethodDeclarationSyntax>();

        // Create the TestContext.CancellationToken expression
        MemberAccessExpressionSyntax testContextExpression = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.IdentifierName(testContextMemberName),
            SyntaxFactory.IdentifierName("CancellationToken"));

        editor.ReplaceNode(invocationExpression, (node, _) =>
        {
            var invocationExpression = (InvocationExpressionSyntax)node;
            ArgumentListSyntax currentArguments = invocationExpression.ArgumentList;
            NameColonSyntax? nameColon = cancellationTokenParameterName is null ? null : SyntaxFactory.NameColon(cancellationTokenParameterName);
            SeparatedSyntaxList<ArgumentSyntax> newArguments = currentArguments.Arguments.Add(SyntaxFactory.Argument(nameColon, default, testContextExpression));
            return invocationExpression.WithArgumentList(currentArguments.WithArguments(newArguments));
        });
    }

    internal static MethodDeclarationSyntax AddTestContextParameterToMethod(MethodDeclarationSyntax method)
    {
        // Create TestContext parameter
        ParameterSyntax testContextParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("testContext"))
            .WithType(SyntaxFactory.IdentifierName("TestContext"));

        // Add the parameter to the method
        SeparatedSyntaxList<ParameterSyntax> updatedParameterList = method.ParameterList.Parameters.Count == 0
            ? SyntaxFactory.SingletonSeparatedList(testContextParameter)
            : method.ParameterList.Parameters.Add(testContextParameter);

        return method.WithParameterList(method.ParameterList.WithParameters(updatedParameterList));
    }

    internal static TypeDeclarationSyntax AddTestContextProperty(TypeDeclarationSyntax typeDeclaration)
    {
        PropertyDeclarationSyntax testContextProperty = SyntaxFactory.PropertyDeclaration(
            SyntaxFactory.IdentifierName("TestContext"),
            "TestContext")
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithAccessorList(SyntaxFactory.AccessorList(
                SyntaxFactory.List(new[]
                {
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                })));

        return typeDeclaration.AddMembers(testContextProperty);
    }
}

/// <summary>
/// Custom FixAllProvider for <see cref="FlowTestContextCancellationTokenFixer"/> that can add TestContext property when needed.
/// This ensures that when multiple fixes are applied to the same class, the TestContext property is added only once.
/// </summary>
internal sealed class FlowTestContextCancellationTokenFixAllProvider : FixAllProvider
{
    public static readonly FlowTestContextCancellationTokenFixAllProvider Instance = new();

    private FlowTestContextCancellationTokenFixAllProvider()
    {
    }

    public override Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
        => Task.FromResult<CodeAction?>(new FixAllCodeAction(fixAllContext));

    private sealed class FixAllCodeAction : CodeAction
    {
        private readonly FixAllContext _fixAllContext;

        public FixAllCodeAction(FixAllContext fixAllContext)
            => _fixAllContext = fixAllContext;

        public override string Title => CodeFixResources.PassCancellationTokenFix;

        public override string? EquivalenceKey => nameof(FlowTestContextCancellationTokenFixer);

        protected override async Task<Solution?> GetChangedSolutionAsync(CancellationToken cancellationToken)
        {
            FixAllContext fixAllContext = _fixAllContext;
            var editor = new SolutionEditor(fixAllContext.Solution);
            var fixedSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

            if (fixAllContext.Scope == FixAllScope.Document)
            {
                DocumentEditor documentEditor = await editor.GetDocumentEditorAsync(fixAllContext.Document!.Id, cancellationToken).ConfigureAwait(false);
                foreach (Diagnostic diagnostic in await fixAllContext.GetDocumentDiagnosticsAsync(fixAllContext.Document!).ConfigureAwait(false))
                {
                    FixOneDiagnostic(documentEditor, diagnostic, fixedSymbols, cancellationToken);
                }
            }
            else if (fixAllContext.Scope == FixAllScope.Project)
            {
                await FixAllInProjectAsync(fixAllContext, fixAllContext.Project, editor, fixedSymbols, cancellationToken).ConfigureAwait(false);
            }
            else if (fixAllContext.Scope == FixAllScope.Solution)
            {
                foreach (Project project in fixAllContext.Solution.Projects)
                {
                    await FixAllInProjectAsync(fixAllContext, project, editor, fixedSymbols, cancellationToken).ConfigureAwait(false);
                }
            }

            return editor.GetChangedSolution();
        }

        private static async Task FixAllInProjectAsync(FixAllContext fixAllContext, Project project, SolutionEditor editor, HashSet<ISymbol> fixedSymbols, CancellationToken cancellationToken)
        {
            foreach (Diagnostic diagnostic in await fixAllContext.GetAllDiagnosticsAsync(project).ConfigureAwait(false))
            {
                DocumentId documentId = editor.OriginalSolution.GetDocumentId(diagnostic.Location.SourceTree)!;
                DocumentEditor documentEditor = await editor.GetDocumentEditorAsync(documentId, cancellationToken).ConfigureAwait(false);
                FixOneDiagnostic(documentEditor, diagnostic, fixedSymbols, cancellationToken);
            }
        }

        private static void FixOneDiagnostic(DocumentEditor documentEditor, Diagnostic diagnostic, HashSet<ISymbol> fixedSymbols, CancellationToken cancellationToken)
        {
            SyntaxNode node = documentEditor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            if (node is not InvocationExpressionSyntax invocationExpression)
            {
                return;
            }

            diagnostic.Properties.TryGetValue(FlowTestContextCancellationTokenAnalyzer.TestContextMemberNamePropertyKey, out string? testContextMemberName);
            diagnostic.Properties.TryGetValue(FlowTestContextCancellationTokenAnalyzer.CancellationTokenParameterNamePropertyKey, out string? cancellationTokenParameterName);
            diagnostic.Properties.TryGetValue(nameof(FlowTestContextCancellationTokenAnalyzer.TestContextState), out string? testContextState);

            FlowTestContextCancellationTokenFixer.ApplyFix(documentEditor, invocationExpression, testContextMemberName, testContextState, cancellationTokenParameterName, fixedSymbols, cancellationToken);
        }
    }
}
