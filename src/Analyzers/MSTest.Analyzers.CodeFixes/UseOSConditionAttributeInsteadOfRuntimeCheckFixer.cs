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
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(DiagnosticIds.UseOSConditionAttributeInsteadOfRuntimeCheckRuleId);

    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

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
                createChangedDocument: ct => AddOSConditionAttributeAsync(context.Document, root, methodDeclaration, ifStatement, osPlatform, isNegated, ct),
                equivalenceKey: nameof(UseOSConditionAttributeInsteadOfRuntimeCheckFixer)),
            diagnostic);
    }

    private static async Task<Document> AddOSConditionAttributeAsync(
        Document document,
        SyntaxNode root,
        MethodDeclarationSyntax methodDeclaration,
        IfStatementSyntax ifStatement,
        string osPlatform,
        bool isNegated,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Map OSPlatform to OperatingSystems enum
        string? operatingSystem = MapOSPlatformToOperatingSystem(osPlatform);
        if (operatingSystem is null)
        {
            return document;
        }

        // Determine the condition mode:
        // - If isNegated is false (checking if IS on platform, then early return), we want to EXCLUDE this platform
        // - If isNegated is true (checking if NOT on platform, then early return), we want to INCLUDE this platform only
        // Actually:
        // if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return; => Include Windows only
        // if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return; => Exclude Windows
        string conditionMode = isNegated ? "ConditionMode.Include" : "ConditionMode.Exclude";

        // Create the attribute
        AttributeSyntax osConditionAttribute = SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("OSCondition"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.ParseExpression(conditionMode)),
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.ParseExpression($"OperatingSystems.{operatingSystem}")),
                })));

        // Check if the method already has an OSCondition attribute
        bool hasOSConditionAttribute = methodDeclaration.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => a.Name.ToString() is "OSCondition" or "OSConditionAttribute");

        if (!hasOSConditionAttribute)
        {
            // Add the attribute to the method
            AttributeListSyntax newAttributeList = SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(osConditionAttribute))
                .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

            MethodDeclarationSyntax newMethod = methodDeclaration.AddAttributeLists(newAttributeList);
            editor.ReplaceNode(methodDeclaration, newMethod);
        }

        // Remove the if statement
        editor.RemoveNode(ifStatement);

        return editor.GetChangedDocument();
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
}
