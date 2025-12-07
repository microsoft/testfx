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

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// Code fixer for <see cref="UseOSConditionAttributeInsteadOfRuntimeCheckAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseOSConditionAttributeInsteadOfRuntimeCheckFixer))]
[Shared]
public sealed class UseOSConditionAttributeInsteadOfRuntimeCheckFixer : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.UseOSConditionAttributeInsteadOfRuntimeCheckRuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];

        string? isNegatedStr = diagnostic.Properties[UseOSConditionAttributeInsteadOfRuntimeCheckAnalyzer.IsNegatedKey];
        string? osPlatform = diagnostic.Properties[UseOSConditionAttributeInsteadOfRuntimeCheckAnalyzer.OSPlatformKey];

        if (isNegatedStr is null || osPlatform is null || !bool.TryParse(isNegatedStr, out bool isNegated))
        {
            return;
        }

        SyntaxNode diagnosticNode = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        // Find the containing method
        MethodDeclarationSyntax? methodDeclaration = diagnosticNode.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDeclaration is null)
        {
            return;
        }

        // Find the if statement to remove
        IfStatementSyntax? ifStatement = diagnosticNode.FirstAncestorOrSelf<IfStatementSyntax>();
        if (ifStatement is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.UseOSConditionAttributeInsteadOfRuntimeCheckFix,
                createChangedDocument: ct => AddOSConditionAttributeAsync(context.Document, methodDeclaration, ifStatement, osPlatform, isNegated, ct),
                equivalenceKey: nameof(UseOSConditionAttributeInsteadOfRuntimeCheckFixer)),
            diagnostic);
    }

    private static async Task<Document> AddOSConditionAttributeAsync(
        Document document,
        MethodDeclarationSyntax methodDeclaration,
        IfStatementSyntax ifStatement,
        string osPlatform,
        bool isNegated,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        string? operatingSystem = MapOSPlatformToOperatingSystem(osPlatform);
        if (operatingSystem is null)
        {
            return document;
        }

        MethodDeclarationSyntax? modifiedMethod = RemoveIfStatementFromMethod(methodDeclaration, ifStatement);
        if (modifiedMethod is null)
        {
            return document;
        }

        AttributeSyntax? existingAttribute = FindExistingOSConditionAttribute(methodDeclaration);
        MethodDeclarationSyntax newMethod = existingAttribute is not null
            ? UpdateMethodWithCombinedAttribute(modifiedMethod, existingAttribute, operatingSystem, isNegated)
            : AddNewAttributeToMethod(modifiedMethod, operatingSystem, isNegated);

        editor.ReplaceNode(methodDeclaration, newMethod);
        return editor.GetChangedDocument();
    }

    private static MethodDeclarationSyntax? RemoveIfStatementFromMethod(
        MethodDeclarationSyntax methodDeclaration,
        IfStatementSyntax ifStatement)
    {
        MethodDeclarationSyntax trackedMethod = methodDeclaration.TrackNodes(ifStatement);
        IfStatementSyntax? trackedIfStatement = trackedMethod.GetCurrentNode(ifStatement);

        return trackedIfStatement is not null
            ? trackedMethod.RemoveNode(trackedIfStatement, SyntaxRemoveOptions.KeepNoTrivia)
            : null;
    }

    private static AttributeSyntax? FindExistingOSConditionAttribute(MethodDeclarationSyntax methodDeclaration)
        => methodDeclaration.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a => a.Name.ToString() is "OSCondition" or "OSConditionAttribute");

    private static MethodDeclarationSyntax UpdateMethodWithCombinedAttribute(
        MethodDeclarationSyntax method,
        AttributeSyntax existingAttribute,
        string operatingSystem,
        bool isNegated)
    {
        ExistingAttributeInfo attributeInfo = ParseExistingAttribute(existingAttribute);

        // Only combine if the condition modes match
        if (CanCombineAttributes(attributeInfo.IsIncludeMode, isNegated))
        {
            string combinedOSValue = CombineOSValues(attributeInfo.OSValue, operatingSystem);
            AttributeSyntax newAttribute = CreateCombinedAttribute(combinedOSValue, isNegated);
            return ReplaceExistingAttribute(method, newAttribute);
        }

        // Different condition modes - add as separate attribute
        // (This shouldn't happen in practice since OSCondition doesn't allow multiple attributes)
        return AddNewAttributeToMethod(method, operatingSystem, isNegated);
    }

    private static ExistingAttributeInfo ParseExistingAttribute(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList is null)
        {
            return new ExistingAttributeInfo(IsIncludeMode: true, OSValue: null);
        }

        SeparatedSyntaxList<AttributeArgumentSyntax> args = attribute.ArgumentList.Arguments;

        return args.Count switch
        {
            // [OSCondition(OperatingSystems.Linux)] - Include mode
            1 => new ExistingAttributeInfo(
                IsIncludeMode: true,
                OSValue: args[0].Expression.ToString()),

            // [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
            2 => new ExistingAttributeInfo(
                IsIncludeMode: !args[0].Expression.ToString().Contains("Exclude"),
                OSValue: args[1].Expression.ToString()),

            _ => new ExistingAttributeInfo(IsIncludeMode: true, OSValue: null),
        };
    }

    private static bool CanCombineAttributes(bool existingIsIncludeMode, bool isNegated)
        => (isNegated && existingIsIncludeMode) || (!isNegated && !existingIsIncludeMode);

    private static string CombineOSValues(string? existingOSValue, string newOperatingSystem)
        => existingOSValue is not null
            ? $"{existingOSValue} | OperatingSystems.{newOperatingSystem}"
            : $"OperatingSystems.{newOperatingSystem}";

    private static AttributeSyntax CreateCombinedAttribute(string osValue, bool isNegated)
    {
        if (isNegated)
        {
            // Include mode (default)
            return SyntaxFactory.Attribute(
                SyntaxFactory.IdentifierName("OSCondition"),
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.AttributeArgument(
                            SyntaxFactory.ParseExpression(osValue)))));
        }

        // Exclude mode
        return SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("OSCondition"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.ParseExpression("ConditionMode.Exclude")),
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.ParseExpression(osValue)),
                })));
    }

    private static MethodDeclarationSyntax ReplaceExistingAttribute(
        MethodDeclarationSyntax method,
        AttributeSyntax newAttribute)
    {
        AttributeListSyntax? oldAttributeList = method.AttributeLists
            .FirstOrDefault(al => al.Attributes.Any(a => a.Name.ToString() is "OSCondition" or "OSConditionAttribute"));

        if (oldAttributeList is null)
        {
            return method;
        }

        AttributeListSyntax newAttributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(newAttribute))
            .WithTrailingTrivia(oldAttributeList.GetTrailingTrivia());

        return method.ReplaceNode(oldAttributeList, newAttributeList);
    }

    private static MethodDeclarationSyntax AddNewAttributeToMethod(
        MethodDeclarationSyntax method,
        string operatingSystem,
        bool isNegated)
    {
        AttributeListSyntax newAttributeList = CreateAttributeList(operatingSystem, isNegated);
        return method.AddAttributeLists(newAttributeList);
    }

    private static AttributeListSyntax CreateAttributeList(string operatingSystem, bool isNegated)
    {
        AttributeSyntax osConditionAttribute;
        if (isNegated)
        {
            // Include mode is the default, so we only need to specify the operating system
            osConditionAttribute = SyntaxFactory.Attribute(
                SyntaxFactory.IdentifierName("OSCondition"),
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.AttributeArgument(
                            SyntaxFactory.ParseExpression($"OperatingSystems.{operatingSystem}")))));
        }
        else
        {
            // Exclude mode must be explicitly specified
            osConditionAttribute = SyntaxFactory.Attribute(
                SyntaxFactory.IdentifierName("OSCondition"),
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.AttributeArgument(
                            SyntaxFactory.ParseExpression("ConditionMode.Exclude")),
                        SyntaxFactory.AttributeArgument(
                            SyntaxFactory.ParseExpression($"OperatingSystems.{operatingSystem}")),
                    })));
        }

        return SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(osConditionAttribute));
    }

    private static string? MapOSPlatformToOperatingSystem(string osPlatform)
        => osPlatform.ToUpperInvariant() switch
        {
            "WINDOWS" => "Windows",
            "LINUX" => "Linux",
            "OSX" => "OSX",
            "FREEBSD" => "FreeBSD",
            _ => null,
        };

    private readonly record struct ExistingAttributeInfo(bool IsIncludeMode, string? OSValue);
}
