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
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fix provider for <see cref="TestMethodAttributeShouldPropagateSourceInformationAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TestMethodAttributeShouldPropagateSourceInformationFixer))]
[Shared]
public sealed class TestMethodAttributeShouldPropagateSourceInformationFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(DiagnosticIds.TestMethodAttributeShouldPropagateSourceInformationRuleId);

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

        SyntaxToken identifierToken = root.FindToken(diagnosticSpan.Start);
        if (identifierToken.Parent is ConstructorDeclarationSyntax constructorDeclaration)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.AddCallerInfoParametersFix,
                    createChangedDocument: c => AddCallerInfoParametersAsync(context.Document, constructorDeclaration, root),
                    equivalenceKey: nameof(TestMethodAttributeShouldPropagateSourceInformationFixer)),
                diagnostic);
        }
        else if (identifierToken.Parent is BaseTypeDeclarationSyntax typeDeclarationSyntax)
        {
            // There is no explicit constructor. We only have the implicit parameterless constructor. We will the constructor.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.AddCallerInfoParametersFix,
                    createChangedDocument: c => AddCallerInfoParametersAsync(context.Document, typeDeclarationSyntax, root),
                    equivalenceKey: nameof(TestMethodAttributeShouldPropagateSourceInformationFixer)),
                diagnostic);
        }
    }

    private static ParameterSyntax? GetNewParameter(int existingIndex, string attributeName)
    {
        if (existingIndex > -1)
        {
            // Parameter already exists. We don't create it.
            return null;
        }

        (string parameterName, SyntaxKind typeKind, ExpressionSyntax defaultValueExpression) = attributeName switch
        {
            "CallerFilePath" => ("callerFilePath", SyntaxKind.StringKeyword, GetDefaultExpressionForCallerFilePath()),
            "CallerLineNumber" => ("callerLineNumber", SyntaxKind.IntKeyword, GetDefaultExpressionForCallerLineNumber()),
            _ => throw ApplicationStateGuard.Unreachable(),
        };

        IdentifierNameSyntax attributeIdentifier = SyntaxFactory.IdentifierName(attributeName)
            .WithAdditionalAnnotations(Simplifier.AddImportsAnnotation, new SyntaxAnnotation("SymbolId", $"System.Runtime.CompilerServices.{attributeName}Attribute"));
        SyntaxList<AttributeListSyntax> attributeLists = SyntaxFactory.SingletonList(
            SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(attributeIdentifier))));

        PredefinedTypeSyntax type = SyntaxFactory.PredefinedType(SyntaxFactory.Token(typeKind));
        SyntaxToken identifier = SyntaxFactory.Identifier(parameterName);
        EqualsValueClauseSyntax equalsValueClause = SyntaxFactory.EqualsValueClause(defaultValueExpression);
        return SyntaxFactory.Parameter(attributeLists, default, type, identifier, equalsValueClause);

        ExpressionSyntax GetDefaultExpressionForCallerFilePath()
            => SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(string.Empty));

        ExpressionSyntax GetDefaultExpressionForCallerLineNumber()
            => SyntaxFactory.PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1)));
    }

    private static Task<Document> AddCallerInfoParametersAsync(Document document, ConstructorDeclarationSyntax constructorDeclaration, SyntaxNode root)
    {
        int callerFilePathIndex = GetCallerFilePathParameterIndex(constructorDeclaration);
        int callerLineNumberIndex = GetCallerLineNumberParameterIndex(constructorDeclaration);
        ParameterSyntax? newCallerFilePathParameter = GetNewParameter(callerFilePathIndex, "CallerFilePath");
        ParameterSyntax? newCallerLineNumberParameter = GetNewParameter(callerLineNumberIndex, "CallerLineNumber");

        ParameterListSyntax updatedParameterList = constructorDeclaration.ParameterList;
        if (newCallerFilePathParameter is not null && newCallerLineNumberParameter is not null)
        {
            // We want to add both parameters. We just add to the end.
            updatedParameterList = constructorDeclaration.ParameterList.AddParameters(newCallerFilePathParameter, newCallerLineNumberParameter);
        }
        else if (newCallerFilePathParameter is null && newCallerLineNumberParameter is not null)
        {
            // We want to add only CallerLineNumber parameter.
            // We find where the existing file path parameter is, and add the line number parameter after it.
            // This is not a common scenario. Usually, either both exist (in which case analyzer won't issue a diagnostic), or both don't exist.
            updatedParameterList = constructorDeclaration.ParameterList.WithParameters(
                constructorDeclaration.ParameterList.Parameters.Insert(callerFilePathIndex + 1, newCallerLineNumberParameter));
        }
        else if (newCallerFilePathParameter is not null && newCallerLineNumberParameter is null)
        {
            // We want to add only CallerFilePath parameter.
            // We find where the existing line number parameter is, and add the line number parameter before it.
            // This is not a common scenario. Usually, either both exist (in which case analyzer won't issue a diagnostic), or both don't exist.
            updatedParameterList = constructorDeclaration.ParameterList.WithParameters(
                constructorDeclaration.ParameterList.Parameters.Insert(callerLineNumberIndex, newCallerFilePathParameter));
        }

        ConstructorDeclarationSyntax newConstructor = constructorDeclaration.WithParameterList(updatedParameterList);

        if (newConstructor.Initializer is not null)
        {
            newConstructor = UpdateConstructorInitializer(newConstructor, newCallerFilePathParameter is not null, newCallerLineNumberParameter is not null);
        }
        else if (newConstructor.Initializer is null)
        {
            var baseArguments = new List<ArgumentSyntax>();
            if (newCallerFilePathParameter is not null)
            {
                baseArguments.Add(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("callerFilePath")));
            }

            if (newCallerLineNumberParameter is not null)
            {
                baseArguments.Add(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("callerLineNumber")));
            }

            ConstructorInitializerSyntax baseInitializer = SyntaxFactory.ConstructorInitializer(
                SyntaxKind.BaseConstructorInitializer,
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(baseArguments)));
            newConstructor = newConstructor.WithInitializer(baseInitializer);
        }

        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(constructorDeclaration, newConstructor)));
    }

    private static Task<Document> AddCallerInfoParametersAsync(Document document, BaseTypeDeclarationSyntax typeDeclarationSyntax, SyntaxNode root)
    {
        var generator = SyntaxGenerator.GetGenerator(document);
        SyntaxNode constructor = generator.ConstructorDeclaration(
            typeDeclarationSyntax.Identifier.ValueText,
            [GetNewParameter(-1, "CallerFilePath"), GetNewParameter(-1, "CallerLineNumber")],
            Accessibility.Public,
            DeclarationModifiers.None,
            [SyntaxFactory.IdentifierName("callerFilePath"), SyntaxFactory.IdentifierName("callerLineNumber")]);

        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(typeDeclarationSyntax, generator.InsertMembers(typeDeclarationSyntax, 0, constructor))));
    }

    private static int GetCallerFilePathParameterIndex(ConstructorDeclarationSyntax constructor)
    {
        for (int i = 0; i < constructor.ParameterList.Parameters.Count; i++)
        {
            if (constructor.ParameterList.Parameters[i].AttributeLists.SelectMany(al => al.Attributes)
                .Any(attr => attr.Name.ToString() is "CallerFilePath" or "CallerFilePathAttribute"))
            {
                return i;
            }
        }

        return -1;
    }

    private static int GetCallerLineNumberParameterIndex(ConstructorDeclarationSyntax constructor)
    {
        for (int i = 0; i < constructor.ParameterList.Parameters.Count; i++)
        {
            if (constructor.ParameterList.Parameters[i].AttributeLists.SelectMany(al => al.Attributes)
                .Any(attr => attr.Name.ToString() is "CallerLineNumber" or "CallerLineNumberAttribute"))
            {
                return i;
            }
        }

        return -1;
    }

    private static ConstructorDeclarationSyntax UpdateConstructorInitializer(
        ConstructorDeclarationSyntax constructor,
        bool addCallerFilePath,
        bool addCallerLineNumber)
    {
        if (constructor.Initializer is null)
        {
            throw ApplicationStateGuard.Unreachable();
        }

        if (addCallerFilePath && addCallerLineNumber)
        {
            return constructor.WithInitializer(constructor.Initializer.WithArgumentList(constructor.Initializer.ArgumentList.AddArguments(
                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("callerFilePath")),
                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("callerLineNumber")))));
        }

        if (addCallerFilePath)
        {
            for (int i = 0; i < constructor.Initializer.ArgumentList.Arguments.Count; i++)
            {
                if (constructor.Initializer.ArgumentList.Arguments[i].Expression is IdentifierNameSyntax identifier &&
                    identifier.Identifier.ValueText == "callerLineNumber")
                {
                    return constructor.WithInitializer(
                        constructor.Initializer.WithArgumentList(
                            constructor.Initializer.ArgumentList.WithArguments(
                                constructor.Initializer.ArgumentList.Arguments.Insert(i, SyntaxFactory.Argument(SyntaxFactory.IdentifierName("callerFilePath"))))));
                }
            }
        }

        if (addCallerLineNumber)
        {
            for (int i = 0; i < constructor.Initializer.ArgumentList.Arguments.Count; i++)
            {
                if (constructor.Initializer.ArgumentList.Arguments[i].Expression is IdentifierNameSyntax identifier &&
                    identifier.Identifier.ValueText == "callerFilePath")
                {
                    return constructor.WithInitializer(
                        constructor.Initializer.WithArgumentList(
                            constructor.Initializer.ArgumentList.WithArguments(
                                constructor.Initializer.ArgumentList.Arguments.Insert(i + 1, SyntaxFactory.Argument(SyntaxFactory.IdentifierName("callerLineNumber"))))));
                }
            }
        }

        return constructor;
    }
}
