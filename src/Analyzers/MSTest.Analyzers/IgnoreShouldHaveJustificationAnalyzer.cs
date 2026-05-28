// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0066: <inheritdoc cref="Resources.IgnoreShouldHaveJustificationTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class IgnoreShouldHaveJustificationAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.IgnoreShouldHaveJustificationTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.IgnoreShouldHaveJustificationMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.IgnoreShouldHaveJustificationDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.IgnoreShouldHaveJustificationRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Design,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingIgnoreAttribute, out INamedTypeSymbol? ignoreAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol))
            {
                return;
            }

            context.RegisterSymbolAction(
                ctx => AnalyzeMethod(ctx, ignoreAttributeSymbol, testMethodAttributeSymbol),
                SymbolKind.Method);

            context.RegisterSymbolAction(
                ctx => AnalyzeType(ctx, ignoreAttributeSymbol, testClassAttributeSymbol),
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, INamedTypeSymbol ignoreAttributeSymbol, INamedTypeSymbol testMethodAttributeSymbol)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        AttributeData? ignoreAttribute = null;
        bool isTestMethod = false;
        foreach (AttributeData methodAttribute in methodSymbol.GetAttributes())
        {
            if (methodAttribute.AttributeClass.Inherits(testMethodAttributeSymbol))
            {
                isTestMethod = true;
            }

            if (SymbolEqualityComparer.Default.Equals(methodAttribute.AttributeClass, ignoreAttributeSymbol))
            {
                ignoreAttribute = methodAttribute;
            }
        }

        if (!isTestMethod || ignoreAttribute is null)
        {
            return;
        }

        ReportIfMissingJustification(context, ignoreAttribute, methodSymbol.Name);
    }

    private static void AnalyzeType(SymbolAnalysisContext context, INamedTypeSymbol ignoreAttributeSymbol, INamedTypeSymbol testClassAttributeSymbol)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;

        AttributeData? ignoreAttribute = null;
        bool isTestClass = false;
        foreach (AttributeData typeAttribute in typeSymbol.GetAttributes())
        {
            if (typeAttribute.AttributeClass.Inherits(testClassAttributeSymbol))
            {
                isTestClass = true;
            }

            if (SymbolEqualityComparer.Default.Equals(typeAttribute.AttributeClass, ignoreAttributeSymbol))
            {
                ignoreAttribute = typeAttribute;
            }
        }

        if (!isTestClass || ignoreAttribute is null)
        {
            return;
        }

        ReportIfMissingJustification(context, ignoreAttribute, typeSymbol.Name);
    }

    private static void ReportIfMissingJustification(SymbolAnalysisContext context, AttributeData ignoreAttribute, string targetName)
    {
        if (HasJustification(ignoreAttribute))
        {
            return;
        }

        if (ignoreAttribute.ApplicationSyntaxReference is { } syntaxReference)
        {
            context.ReportDiagnostic(syntaxReference.CreateDiagnostic(Rule, context.CancellationToken, targetName));
        }
        else
        {
            context.ReportDiagnostic(context.Symbol.CreateDiagnostic(Rule, targetName));
        }
    }

    private static bool HasJustification(AttributeData ignoreAttribute)
    {
        // First positional argument of IgnoreAttribute(string? message) is the justification when provided.
        if (ignoreAttribute.ConstructorArguments.Length > 0
            && ignoreAttribute.ConstructorArguments[0].Value is string positionalMessage
            && !string.IsNullOrWhiteSpace(positionalMessage))
        {
            return true;
        }

        // The IgnoreMessage property (inherited from ConditionBaseAttribute) can also carry the
        // justification when applied via named-argument syntax: [Ignore(IgnoreMessage = "...")].
        foreach (KeyValuePair<string, TypedConstant> namedArgument in ignoreAttribute.NamedArguments)
        {
            if (namedArgument.Key == "IgnoreMessage"
                && namedArgument.Value.Value is string namedMessage
                && !string.IsNullOrWhiteSpace(namedMessage))
            {
                return true;
            }
        }

        return false;
    }
}
