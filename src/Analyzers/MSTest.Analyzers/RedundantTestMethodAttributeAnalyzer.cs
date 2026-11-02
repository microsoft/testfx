// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0086: <inheritdoc cref="Resources.RedundantTestMethodAttributeTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed partial class RedundantTestMethodAttributeAnalyzer : DiagnosticAnalyzer
{
    private const int AllOperatingSystems = (1 << 4) - 1;
    private const int AllArchitectures = (1 << 10) - 1;

    private static readonly LocalizableResourceString Title = new(nameof(Resources.RedundantTestMethodAttributeTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.RedundantTestMethodAttributeMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.RedundantTestMethodAttributeDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.RedundantTestMethodAttributeRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
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
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute,
                    out INamedTypeSymbol? testMethodAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute,
                    out INamedTypeSymbol? testClassAttributeSymbol))
            {
                return;
            }

            var symbols = new KnownAttributeSymbols(
                context.Compilation,
                testMethodAttributeSymbol,
                testClassAttributeSymbol);

            context.RegisterSymbolAction(
                context => AnalyzeMethod(context, symbols),
                SymbolKind.Method);
        });
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, KnownAttributeSymbols symbols)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        ImmutableArray<AttributeData> methodAttributes = methodSymbol.GetAttributes();
        ImmutableArray<AttributeData> classAttributes = methodSymbol.ContainingType.GetAttributes();
        if (!methodAttributes.Any(attribute => attribute.AttributeClass.Inherits(symbols.TestMethod))
            || !classAttributes.Any(attribute => attribute.AttributeClass.Inherits(symbols.TestClass)))
        {
            return;
        }

        ImmutableArray<AttributeData> inheritedClassAttributes = GetClassAttributesIncludingBaseTypes(methodSymbol.ContainingType);
        foreach (AttributeData methodAttribute in methodAttributes)
        {
            if (methodAttribute.ApplicationSyntaxReference is not { } syntaxReference
                || !IsRedundant(
                    methodAttribute,
                    classAttributes,
                    inheritedClassAttributes,
                    methodSymbol,
                    symbols))
            {
                continue;
            }

            string attributeName = FormatAttributeName(methodAttribute.AttributeClass);
            context.ReportDiagnostic(syntaxReference.CreateDiagnostic(Rule, context.CancellationToken, attributeName, methodSymbol.Name));
        }
    }

    private static ImmutableArray<AttributeData> GetClassAttributesIncludingBaseTypes(INamedTypeSymbol type)
    {
        ImmutableArray<AttributeData>.Builder attributes = ImmutableArray.CreateBuilder<AttributeData>();
        for (INamedTypeSymbol? currentType = type; currentType is not null; currentType = currentType.BaseType)
        {
            attributes.AddRange(currentType.GetAttributes());
        }

        return attributes.ToImmutable();
    }

    private static string FormatAttributeName(INamedTypeSymbol? attributeClass)
    {
        if (attributeClass is null)
        {
            return "Attribute";
        }

        const string attributeSuffix = "Attribute";
        string name = attributeClass.Name;
        return name.EndsWith(attributeSuffix, StringComparison.Ordinal)
            ? $"[{name.Substring(0, name.Length - attributeSuffix.Length)}]"
            : $"[{name}]";
    }

    private static bool IsRedundant(
        AttributeData methodAttribute,
        ImmutableArray<AttributeData> classAttributes,
        ImmutableArray<AttributeData> inheritedClassAttributes,
        IMethodSymbol methodSymbol,
        KnownAttributeSymbols symbols)
    {
        INamedTypeSymbol? attributeClass = methodAttribute.AttributeClass;
        bool declaringClassIsSealed = methodSymbol.ContainingType.IsSealed;
        return attributeClass switch
        {
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.OSCondition)
                => declaringClassIsSealed
                && IsFlagConditionRedundant(
                methodAttribute,
                FindAttribute(classAttributes, symbols.OSCondition),
                AllOperatingSystems,
                excludeAllowsUnknownValues: true),
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.ArchitectureCondition)
                => declaringClassIsSealed
                && IsFlagConditionRedundant(
                methodAttribute,
                FindAttribute(classAttributes, symbols.ArchitectureCondition),
                AllArchitectures,
                excludeAllowsUnknownValues: false),
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.CICondition)
                => declaringClassIsSealed
                && IsCIConditionRedundant(methodAttribute, FindAttribute(classAttributes, symbols.CICondition)),
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.DoNotParallelize)
                => FindAttribute(inheritedClassAttributes, symbols.DoNotParallelize) is not null,
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.ResourceLock)
                => IsResourceLockRedundant(methodAttribute, inheritedClassAttributes, symbols.ResourceLock),
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.Retry)
                => declaringClassIsSealed
                && IsRetryRedundant(methodAttribute, FindAttribute(classAttributes, symbols.Retry)),
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.Ignore)
                => declaringClassIsSealed
                && IsIgnoreRedundant(methodAttribute, FindAttribute(classAttributes, symbols.Ignore)),
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.TestCategory)
                => HasEquivalentAttribute(methodAttribute, inheritedClassAttributes, symbols.TestCategory),
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.TestProperty)
                => HasEquivalentAttribute(methodAttribute, inheritedClassAttributes, symbols.TestProperty),
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.DeploymentItem)
                => IsDeploymentItemRedundant(methodAttribute, inheritedClassAttributes, symbols.DeploymentItem),
            _ when SymbolEqualityComparer.Default.Equals(attributeClass, symbols.DependsOn)
                => declaringClassIsSealed
                && IsDependsOnRedundant(methodAttribute, classAttributes, methodSymbol, symbols.DependsOn),
            _ => false,
        };
    }

    private static AttributeData? FindAttribute(
        ImmutableArray<AttributeData> attributes,
        INamedTypeSymbol? attributeSymbol)
        => attributeSymbol is null
            ? null
            : attributes.FirstOrDefault(
                attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol));
}
