// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="UndeclaredProcessGlobalStateMutationAnalyzer"/> (MSTEST0074) and
/// <see cref="CurrentDirectoryMutationUnderParallelizationAnalyzer"/> (MSTEST0075). Adds a
/// <c>[ResourceLock(WellKnownResources.X)]</c> attribute to the enclosing test method. The concrete
/// <c>WellKnownResources</c> member is read from the diagnostic's <c>ResourceMember</c> property; when that
/// property is absent (for example when a lock is already declared) no fix is offered.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddResourceLockFixer))]
[Shared]
public sealed class AddResourceLockFixer : CodeFixProvider
{
    private const string WellKnownResourcesFullName = "Microsoft.VisualStudio.TestTools.UnitTesting.WellKnownResources";
    private const string ResourceLockAttributeFullName = "Microsoft.VisualStudio.TestTools.UnitTesting.ResourceLockAttribute";

    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(
            DiagnosticIds.UndeclaredProcessGlobalStateMutationRuleId,
            DiagnosticIds.CurrentDirectoryMutationUnderParallelizationRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Diagnostic diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.TryGetValue(ParallelSafetyHelper.ResourceMemberPropertyKey, out string? resourceMember)
            || resourceMember is null)
        {
            // No member to reference (for example a lock is already declared) - nothing to fix.
            return;
        }

        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        SyntaxToken syntaxToken = root.FindToken(diagnosticSpan.Start);
        MethodDeclarationSyntax? methodDeclaration = syntaxToken.Parent?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (methodDeclaration is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: string.Format(CultureInfo.CurrentCulture, CodeFixResources.AddResourceLockFix, resourceMember),
                createChangedDocument: c => AddResourceLockAttributeAsync(context.Document, methodDeclaration, resourceMember, c),
                equivalenceKey: $"{nameof(AddResourceLockFixer)}_{resourceMember}"),
            diagnostic);
    }

    private static async Task<Document> AddResourceLockAttributeAsync(Document document, MethodDeclarationSyntax methodDeclaration, string resourceMember, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        SyntaxGenerator generator = editor.Generator;

        SyntaxNode argument = generator.AttributeArgument(
            generator.MemberAccessExpression(
                generator.DottedName(WellKnownResourcesFullName),
                resourceMember));

        SyntaxNode attribute = generator.Attribute(ResourceLockAttributeFullName, [argument])
            .WithAdditionalAnnotations(Simplifier.Annotation);

        editor.AddAttribute(methodDeclaration, attribute);
        return editor.GetChangedDocument();
    }
}
