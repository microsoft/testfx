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
/// Code fixer for <see cref="PublicTypeShouldBeTestClassAnalyzer"/>, <see cref="TypeContainingTestMethodShouldBeATestClassAnalyzer"/>
/// and <see cref="UseConditionBaseWithTestClassAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddTestClassFixer))]
[Shared]
public sealed class AddTestClassFixer : CodeFixProvider
{
    private const string TestClassAttributeName = "TestClass";
    private const string FullyQualifiedTestClassAttributeName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestClass";

    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(
            DiagnosticIds.PublicTypeShouldBeTestClassRuleId,
            DiagnosticIds.TypeContainingTestMethodShouldBeATestClassRuleId,
            DiagnosticIds.UseConditionBaseWithTestClassRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        SyntaxToken syntaxToken = root.FindToken(diagnosticSpan.Start);
        if (syntaxToken.Parent is null)
        {
            return;
        }

        // Find the type declaration identified by the diagnostic. A condition attribute (MSTEST0041) can be
        // declared with a custom AttributeUsage targeting a type kind that has no TypeDeclarationSyntax (an enum,
        // for example), and [TestClass] is meaningless on an interface, so bail out in those cases.
        TypeDeclarationSyntax? declaration = syntaxToken.Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (declaration is null or InterfaceDeclarationSyntax)
        {
            return;
        }

        bool isStruct = declaration is StructDeclarationSyntax
            || (declaration is RecordDeclarationSyntax { ClassOrStructKeyword: var keyword } && keyword.IsKind(SyntaxKind.StructKeyword));

        // MSTEST0041 fires on whatever target the condition attribute allows. When that target is a struct, the
        // attribute only got there because its own AttributeUsage permits structs, so turning the struct into a
        // class would strand the attribute on a target it doesn't allow (CS0592). The other rules only ever ask for
        // a test class, where the conversion is the intended fix.
        if (isStruct && diagnostic.Id == DiagnosticIds.UseConditionBaseWithTestClassRuleId)
        {
            return;
        }

        // For structs and record structs, we need to change them to classes/record classes since [TestClass] cannot be applied to structs
        if (declaration is StructDeclarationSyntax)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.ChangeStructToClassAndAddTestClassFix,
                    createChangedDocument: c => ChangeStructToClassAndAddTestClassAttributeAsync(context.Document, declaration, c),
                    equivalenceKey: $"{nameof(AddTestClassFixer)}_ChangeStructToClass_{diagnostic.Id}"),
                diagnostic);
        }
        else if (declaration is RecordDeclarationSyntax recordDeclaration
            && recordDeclaration.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword))
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.ChangeStructToClassAndAddTestClassFix,
                    createChangedDocument: c => ChangeRecordStructToRecordClassAndAddTestClassAttributeAsync(context.Document, recordDeclaration, c),
                    equivalenceKey: $"{nameof(AddTestClassFixer)}_ChangeRecordStructToClass_{diagnostic.Id}"),
                diagnostic);
        }
        else
        {
            // For classes and record classes, just add the [TestClass] attribute
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.AddTestClassFix,
                    createChangedDocument: c => AddTestClassAttributeAsync(context.Document, declaration, c),
                    equivalenceKey: $"{nameof(AddTestClassFixer)}_{diagnostic.Id}"),
                diagnostic);
        }
    }

    private static async Task<Document> AddTestClassAttributeAsync(Document document, TypeDeclarationSyntax typeDeclaration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        AttributeListSyntax attributeList = await CreateTestClassAttributeListAsync(document, typeDeclaration.Identifier.SpanStart, cancellationToken).ConfigureAwait(false);

        TypeDeclarationSyntax newTypeDeclaration = typeDeclaration.AddAttributeLists(attributeList);
        editor.ReplaceNode(typeDeclaration, newTypeDeclaration);

        return editor.GetChangedDocument();
    }

    private static async Task<Document> ChangeStructToClassAndAddTestClassAttributeAsync(Document document, TypeDeclarationSyntax structDeclaration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        AttributeListSyntax attributeList = await CreateTestClassAttributeListAsync(document, structDeclaration.Identifier.SpanStart, cancellationToken).ConfigureAwait(false);

        // Convert struct to class
        ClassDeclarationSyntax classDeclaration = SyntaxFactory.ClassDeclaration(structDeclaration.Identifier)
            .WithModifiers(structDeclaration.Modifiers)
            .WithTypeParameterList(structDeclaration.TypeParameterList)
            .WithConstraintClauses(structDeclaration.ConstraintClauses)
            .WithBaseList(structDeclaration.BaseList)
            .WithMembers(structDeclaration.Members)
            .WithAttributeLists(structDeclaration.AttributeLists.Add(attributeList))
            .WithLeadingTrivia(structDeclaration.GetLeadingTrivia())
            .WithTrailingTrivia(structDeclaration.GetTrailingTrivia());

        editor.ReplaceNode(structDeclaration, classDeclaration);

        return editor.GetChangedDocument();
    }

    private static async Task<Document> ChangeRecordStructToRecordClassAndAddTestClassAttributeAsync(Document document, RecordDeclarationSyntax recordStructDeclaration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        AttributeListSyntax attributeList = await CreateTestClassAttributeListAsync(document, recordStructDeclaration.Identifier.SpanStart, cancellationToken).ConfigureAwait(false);

        // Filter out readonly modifier since it's not valid for record classes
        SyntaxTokenList filteredModifiers = SyntaxFactory.TokenList(
            recordStructDeclaration.Modifiers.Where(modifier => !modifier.IsKind(SyntaxKind.ReadOnlyKeyword)));

        // Convert record struct to record class by creating a new RecordDeclarationSyntax
        // We need to create a new record declaration instead of just changing the keyword
        RecordDeclarationSyntax recordClassDeclaration = SyntaxFactory.RecordDeclaration(
                recordStructDeclaration.Keyword,
                recordStructDeclaration.Identifier)
            .WithModifiers(filteredModifiers)
            .WithTypeParameterList(recordStructDeclaration.TypeParameterList)
            .WithParameterList(recordStructDeclaration.ParameterList)
            .WithBaseList(recordStructDeclaration.BaseList)
            .WithConstraintClauses(recordStructDeclaration.ConstraintClauses)
            .WithOpenBraceToken(recordStructDeclaration.OpenBraceToken)
            .WithMembers(recordStructDeclaration.Members)
            .WithCloseBraceToken(recordStructDeclaration.CloseBraceToken)
            .WithSemicolonToken(recordStructDeclaration.SemicolonToken)
            .WithAttributeLists(recordStructDeclaration.AttributeLists.Add(attributeList))
            .WithLeadingTrivia(recordStructDeclaration.GetLeadingTrivia())
            .WithTrailingTrivia(recordStructDeclaration.GetTrailingTrivia());

        editor.ReplaceNode(recordStructDeclaration, recordClassDeclaration);

        return editor.GetChangedDocument();
    }

    private static async Task<AttributeListSyntax> CreateTestClassAttributeListAsync(Document document, int position, CancellationToken cancellationToken)
    {
        SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        string attributeName = semanticModel is not null && IsTestClassAttributeInScope(semanticModel, position)
            ? TestClassAttributeName
            : FullyQualifiedTestClassAttributeName;

        NameSyntax testClassAttributeName = SyntaxFactory.ParseName(attributeName);
        AttributeSyntax testClassAttribute = SyntaxFactory.Attribute(testClassAttributeName);
        return SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(testClassAttribute));
    }

    private static bool IsTestClassAttributeInScope(SemanticModel semanticModel, int position)
    {
        INamedTypeSymbol? testClassAttributeSymbol = semanticModel.Compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute);
        return testClassAttributeSymbol is not null
            && semanticModel.LookupNamespacesAndTypes(position, name: $"{TestClassAttributeName}Attribute")
                .Any(symbol => SymbolEqualityComparer.Default.Equals(symbol, testClassAttributeSymbol));
    }
}
