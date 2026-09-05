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
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="OSPlatformAttributesShouldBeConsistentAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OSPlatformAttributesShouldBeConsistentFixer))]
[Shared]
public sealed class OSPlatformAttributesShouldBeConsistentFixer : CodeFixProvider
{
    private const string MSTestNamespace = "Microsoft.VisualStudio.TestTools.UnitTesting";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.OSPlatformAttributesShouldBeConsistentRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Diagnostic diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.TryGetValue(OSPlatformAttributesShouldBeConsistentAnalyzer.ConditionModeKey, out string? conditionMode)
            || !diagnostic.Properties.TryGetValue(OSPlatformAttributesShouldBeConsistentAnalyzer.OperatingSystemsKey, out string? operatingSystems)
            || conditionMode is null
            || operatingSystems is null)
        {
            return;
        }

        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        SyntaxNode diagnosticNode = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        MemberDeclarationSyntax? declaration = diagnosticNode.FirstAncestorOrSelf<MemberDeclarationSyntax>();
        if (declaration is not MethodDeclarationSyntax and not TypeDeclarationSyntax)
        {
            return;
        }

        SemanticModel semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        ISymbol? declaredSymbol = semanticModel.GetDeclaredSymbol(declaration, context.CancellationToken);
        AttributeData? existingOSCondition = declaredSymbol?.GetAttributes().FirstOrDefault(
            attribute => attribute.AttributeClass?.ToDisplayString() == WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingOSConditionAttribute);
        Document targetDocument = context.Document;
        MemberDeclarationSyntax targetDeclaration = declaration;
        TextSpan? existingAttributeSpan = null;
        if (existingOSCondition?.ApplicationSyntaxReference is { } existingSyntaxReference)
        {
            if (context.Document.Project.Solution.GetDocument(existingSyntaxReference.SyntaxTree) is not { } existingDocument)
            {
                return;
            }

            targetDocument = existingDocument;
            SyntaxNode targetRoot = await targetDocument.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (targetRoot.FindNode(existingSyntaxReference.Span).FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } existingDeclaration)
            {
                return;
            }

            targetDeclaration = existingDeclaration;
            existingAttributeSpan = existingSyntaxReference.Span;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                CodeFixResources.MakeOSConditionConsistentFix,
                createChangedSolution: ct => MakeOSConditionConsistentAsync(targetDocument, targetDeclaration, existingAttributeSpan, conditionMode, operatingSystems, ct),
                nameof(OSPlatformAttributesShouldBeConsistentFixer)),
            diagnostic);
    }

    private static async Task<Solution> MakeOSConditionConsistentAsync(
        Document document,
        MemberDeclarationSyntax declaration,
        TextSpan? existingAttributeSpan,
        string conditionMode,
        string operatingSystems,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        AttributeSyntax? existingAttribute = existingAttributeSpan is { } span
            ? editor.OriginalRoot.FindNode(span, getInnermostNodeForTie: true).FirstAncestorOrSelf<AttributeSyntax>()
            : null;
        AttributeArgumentListSyntax argumentList = CreateArgumentList(conditionMode, operatingSystems);
        if (existingAttribute is not null)
        {
            if (existingAttribute.ArgumentList is { } existingArgumentList)
            {
                argumentList = argumentList.WithArguments(
                    argumentList.Arguments.AddRange(existingArgumentList.Arguments.Where(argument => argument.NameEquals is not null)));
            }

            editor.ReplaceNode(
                existingAttribute,
                existingAttribute.WithArgumentList(argumentList).WithAdditionalAnnotations(Formatter.Annotation));
        }
        else
        {
            AttributeSyntax attribute = SyntaxFactory.Attribute(
                SyntaxFactory.ParseName($"{MSTestNamespace}.OSConditionAttribute")
                    .WithAdditionalAnnotations(Simplifier.Annotation),
                argumentList);
            AttributeListSyntax attributeList = SyntaxFactory.AttributeList(
                    SyntaxFactory.SingletonSeparatedList(attribute))
                .WithAdditionalAnnotations(Formatter.Annotation);

            MemberDeclarationSyntax updatedDeclaration = declaration switch
            {
                MethodDeclarationSyntax method => method.AddAttributeLists(attributeList),
                TypeDeclarationSyntax type => type.AddAttributeLists(attributeList),
                _ => declaration,
            };
            editor.ReplaceNode(declaration, updatedDeclaration);
        }

        return editor.GetChangedDocument().Project.Solution;
    }

    private static AttributeArgumentListSyntax CreateArgumentList(string conditionMode, string operatingSystems)
    {
        ExpressionSyntax[] operatingSystemExpressions = operatingSystems
            .Split('|')
            .Select(name => SyntaxFactory.ParseExpression($"{MSTestNamespace}.OperatingSystems.{name}")
                .WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation))
            .ToArray();
        ExpressionSyntax operatingSystemsExpression = operatingSystemExpressions.Aggregate(
            (left, right) => SyntaxFactory.BinaryExpression(SyntaxKind.BitwiseOrExpression, left, right));

        IEnumerable<AttributeArgumentSyntax> arguments = conditionMode == "Include"
            ? [SyntaxFactory.AttributeArgument(operatingSystemsExpression)]
            :
            [
                SyntaxFactory.AttributeArgument(
                    SyntaxFactory.ParseExpression($"{MSTestNamespace}.ConditionMode.Exclude")
                        .WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation)),
                SyntaxFactory.AttributeArgument(operatingSystemsExpression),
            ];

        return SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(arguments));
    }
}
