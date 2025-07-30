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

        // Register a code action that will invoke the fix
        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.PassCancellationTokenFix,
                createChangedDocument: c => AddCancellationTokenParameterAsync(context.Document, invocationExpression, testContextMemberName, c),
                equivalenceKey: "AddTestContextCancellationToken"),
            diagnostic);
    }

    internal static async Task<Document> AddCancellationTokenParameterAsync(
        Document document,
        InvocationExpressionSyntax invocationExpression,
        string? testContextMemberName,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Find the containing method to determine the context
        MethodDeclarationSyntax? containingMethod = invocationExpression.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        ClassDeclarationSyntax? containingClass = invocationExpression.FirstAncestorOrSelf<ClassDeclarationSyntax>();

        string testContextReference;

        if (testContextMemberName is not null)
        {
            // TestContext is already available in scope
            testContextReference = testContextMemberName;
        }
        else
        {
            // TestContext is not in scope, we need to handle this case
            if (containingMethod?.Modifiers.Any(SyntaxKind.StaticKeyword) == true)
            {
                // For static methods, add TestContext parameter and use it
                testContextReference = "testContext";
                if (containingMethod is not null)
                {
                    var updatedMethod = AddTestContextParameterToMethod(containingMethod);
                    editor.ReplaceNode(containingMethod, updatedMethod);
                }
            }
            else
            {
                // For instance methods, reference TestContext property (will be added by FixAllProvider)
                testContextReference = TestContextShouldBeValidAnalyzer.TestContextPropertyName;
            }
        }

        // Create the TestContext.CancellationTokenSource.Token expression
        MemberAccessExpressionSyntax testContextExpression = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(testContextReference),
                SyntaxFactory.IdentifierName("CancellationTokenSource")),
            SyntaxFactory.IdentifierName("Token"));

        ArgumentListSyntax currentArguments = invocationExpression.ArgumentList;
        SeparatedSyntaxList<ArgumentSyntax> newArguments = currentArguments.Arguments.Add(SyntaxFactory.Argument(testContextExpression));
        InvocationExpressionSyntax newInvocation = invocationExpression.WithArgumentList(currentArguments.WithArguments(newArguments));
        editor.ReplaceNode(invocationExpression, newInvocation);
        return editor.GetChangedDocument();
    }

    private static MethodDeclarationSyntax AddTestContextParameterToMethod(MethodDeclarationSyntax method)
    {
        // Create TestContext parameter
        var testContextParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("testContext"))
            .WithType(SyntaxFactory.IdentifierName("TestContext"));

        // Add the parameter to the method
        var updatedParameterList = method.ParameterList.Parameters.Count == 0
            ? SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList(testContextParameter))
            : method.ParameterList.AddParameters(testContextParameter);

        return method.WithParameterList(method.ParameterList.WithParameters(updatedParameterList));
    }
}

/// <summary>
/// Custom FixAllProvider for <see cref="FlowTestContextCancellationTokenFixer"/> that can add TestContext property when needed.
/// </summary>
internal sealed class FlowTestContextCancellationTokenFixAllProvider : FixAllProvider
{
    public static readonly FlowTestContextCancellationTokenFixAllProvider Instance = new();

    private FlowTestContextCancellationTokenFixAllProvider() { }

    public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
    {
        var documentsAndDiagnosticsToFixMap = new Dictionary<Document, ImmutableArray<Diagnostic>>();
        var progressTracker = fixAllContext.ProgressTracker;
        progressTracker.Report(CodeFixResources.PassCancellationTokenFix);

        // Collect diagnostics by document
        switch (fixAllContext.Scope)
        {
            case FixAllScope.Document:
                {
                    ImmutableArray<Diagnostic> diagnostics = await fixAllContext.GetDocumentDiagnosticsAsync(fixAllContext.Document).ConfigureAwait(false);
                    documentsAndDiagnosticsToFixMap.Add(fixAllContext.Document, diagnostics);
                    break;
                }

            case FixAllScope.Project:
                {
                    Project project = fixAllContext.Project;
                    ImmutableArray<Document> documentsToFix = project.Documents.ToImmutableArray();
                    var tasks = documentsToFix.Select(async document =>
                    {
                        ImmutableArray<Diagnostic> diagnostics = await fixAllContext.GetDocumentDiagnosticsAsync(document).ConfigureAwait(false);
                        return (document, diagnostics);
                    });

                    foreach ((Document document, ImmutableArray<Diagnostic> diagnostics) in await Task.WhenAll(tasks).ConfigureAwait(false))
                    {
                        if (diagnostics.Length > 0)
                        {
                            documentsAndDiagnosticsToFixMap.Add(document, diagnostics);
                        }
                    }

                    break;
                }

            case FixAllScope.Solution:
                {
                    Solution solution = fixAllContext.Solution;
                    var documentsToFix = solution.Projects.SelectMany(project => project.Documents).ToImmutableArray();
                    var tasks = documentsToFix.Select(async document =>
                    {
                        ImmutableArray<Diagnostic> diagnostics = await fixAllContext.GetDocumentDiagnosticsAsync(document).ConfigureAwait(false);
                        return (document, diagnostics);
                    });

                    foreach ((Document document, ImmutableArray<Diagnostic> diagnostics) in await Task.WhenAll(tasks).ConfigureAwait(false))
                    {
                        if (diagnostics.Length > 0)
                        {
                            documentsAndDiagnosticsToFixMap.Add(document, diagnostics);
                        }
                    }

                    break;
                }

            default:
                return null;
        }

        // Create code action to fix all documents
        return CodeAction.Create(
            CodeFixResources.PassCancellationTokenFix,
            ct => FixAllAsync(documentsAndDiagnosticsToFixMap, ct),
            nameof(FlowTestContextCancellationTokenFixAllProvider));
    }

    private static async Task<Solution> FixAllAsync(
        Dictionary<Document, ImmutableArray<Diagnostic>> documentsAndDiagnosticsToFixMap,
        CancellationToken cancellationToken)
    {
        Solution solution = documentsAndDiagnosticsToFixMap.First().Key.Project.Solution;

        foreach ((Document document, ImmutableArray<Diagnostic> diagnostics) in documentsAndDiagnosticsToFixMap)
        {
            Document updatedDocument = await FixDocumentAsync(document, diagnostics, cancellationToken).ConfigureAwait(false);
            solution = solution.WithDocumentSyntaxRoot(document.Id, await updatedDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false));
        }

        return solution;
    }

    private static async Task<Document> FixDocumentAsync(
        Document document,
        ImmutableArray<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Group diagnostics by containing class
        var diagnosticsByClass = new Dictionary<ClassDeclarationSyntax, List<(InvocationExpressionSyntax invocation, string? testContextMemberName)>>();

        foreach (Diagnostic diagnostic in diagnostics)
        {
            SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            if (node is not InvocationExpressionSyntax invocationExpression)
            {
                continue;
            }

            ClassDeclarationSyntax? containingClass = invocationExpression.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            if (containingClass is null)
            {
                continue;
            }

            diagnostic.Properties.TryGetValue(FlowTestContextCancellationTokenAnalyzer.TestContextMemberNamePropertyKey, out string? testContextMemberName);

            if (!diagnosticsByClass.TryGetValue(containingClass, out List<(InvocationExpressionSyntax, string?)>? invocations))
            {
                invocations = [];
                diagnosticsByClass[containingClass] = invocations;
            }

            invocations.Add((invocationExpression, testContextMemberName));
        }

        // Process each class
        foreach ((ClassDeclarationSyntax containingClass, List<(InvocationExpressionSyntax invocation, string? testContextMemberName)> invocations) in diagnosticsByClass)
        {
            // Check if we need to add TestContext property to this class
            bool needsTestContextProperty = invocations.Any(inv => inv.testContextMemberName is null && !IsInStaticMethod(inv.invocation));

            ClassDeclarationSyntax updatedClass = containingClass;

            // Add TestContext property if needed
            if (needsTestContextProperty && !HasTestContextProperty(containingClass))
            {
                updatedClass = AddTestContextProperty(updatedClass);
                editor.ReplaceNode(containingClass, updatedClass);
            }

            // Process all invocations in this class
            foreach ((InvocationExpressionSyntax invocation, string? testContextMemberName) in invocations)
            {
                // Create the TestContext reference
                string testContextReference;
                MethodDeclarationSyntax? containingMethod = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();

                if (testContextMemberName is not null)
                {
                    // TestContext is already available in scope
                    testContextReference = testContextMemberName;
                }
                else
                {
                    // TestContext is not in scope, we need to handle this case
                    if (containingMethod?.Modifiers.Any(SyntaxKind.StaticKeyword) == true)
                    {
                        // For static methods, add TestContext parameter and use it
                        testContextReference = "testContext";
                        if (containingMethod is not null)
                        {
                            var updatedMethod = AddTestContextParameterToMethod(containingMethod);
                            editor.ReplaceNode(containingMethod, updatedMethod);
                        }
                    }
                    else
                    {
                        // For instance methods, reference TestContext property
                        testContextReference = TestContextShouldBeValidAnalyzer.TestContextPropertyName;
                    }
                }

                // Create the TestContext.CancellationTokenSource.Token expression
                MemberAccessExpressionSyntax testContextExpression = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(testContextReference),
                        SyntaxFactory.IdentifierName("CancellationTokenSource")),
                    SyntaxFactory.IdentifierName("Token"));

                ArgumentListSyntax currentArguments = invocation.ArgumentList;
                SeparatedSyntaxList<ArgumentSyntax> newArguments = currentArguments.Arguments.Add(SyntaxFactory.Argument(testContextExpression));
                InvocationExpressionSyntax newInvocation = invocation.WithArgumentList(currentArguments.WithArguments(newArguments));
                editor.ReplaceNode(invocation, newInvocation);
            }
        }

        return editor.GetChangedDocument();
    }

    private static bool IsInStaticMethod(InvocationExpressionSyntax invocation)
    {
        MethodDeclarationSyntax? method = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        return method?.Modifiers.Any(SyntaxKind.StaticKeyword) == true;
    }

    private static bool HasTestContextProperty(ClassDeclarationSyntax classDeclaration)
    {
        return classDeclaration.Members
            .OfType<PropertyDeclarationSyntax>()
            .Any(prop => prop.Identifier.ValueText == TestContextShouldBeValidAnalyzer.TestContextPropertyName);
    }

    private static ClassDeclarationSyntax AddTestContextProperty(ClassDeclarationSyntax classDeclaration)
    {
        // Create TestContext property: public TestContext TestContext { get; set; }
        PropertyDeclarationSyntax testContextProperty = SyntaxFactory.PropertyDeclaration(
                SyntaxFactory.IdentifierName("TestContext"),
                SyntaxFactory.Identifier(TestContextShouldBeValidAnalyzer.TestContextPropertyName))
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithAccessorList(SyntaxFactory.AccessorList(
                SyntaxFactory.List(
                [
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                ])));

        return classDeclaration.AddMembers(testContextProperty);
    }
}
