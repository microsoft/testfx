// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class DynamicDataShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.DynamicDataShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.DynamicDataShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.DynamicDataShouldBeValidMessageFormat_OnTestMethod), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor NotTestMethodRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.DynamicDataShouldBeValidRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor MemberNotFoundRule = NotTestMethodRule
        .WithMessage(new(nameof(Resources.DynamicDataShouldBeValidMessageFormat_MemberNotFound), Resources.ResourceManager, typeof(Resources)));

    internal static readonly DiagnosticDescriptor FoundTooManyMembersRule = NotTestMethodRule
        .WithMessage(new(nameof(Resources.DynamicDataShouldBeValidMessageFormat_TooManyMembers), Resources.ResourceManager, typeof(Resources)));

    internal static readonly DiagnosticDescriptor MemberPropertyRule = NotTestMethodRule
        .WithMessage(new(nameof(Resources.DynamicDataShouldBeValidMessageFormat_MemberProperty), Resources.ResourceManager, typeof(Resources)));

    internal static readonly DiagnosticDescriptor MemberMethodRule = NotTestMethodRule
        .WithMessage(new(nameof(Resources.DynamicDataShouldBeValidMessageFormat_MemberMethod), Resources.ResourceManager, typeof(Resources)));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(NotTestMethodRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDynamicDataAttribute, out INamedTypeSymbol? dynamicDataAttributeSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDynamicDataSourceType, out INamedTypeSymbol? dynamicDataSourceTypeSymbol))
            {
                context.RegisterSymbolAction(
                   context => AnalyzeSymbol(context, testMethodAttributeSymbol, dynamicDataAttributeSymbol, dynamicDataSourceTypeSymbol),
                   SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testMethodAttributeSymbol, INamedTypeSymbol dynamicDataAttributeSymbol, INamedTypeSymbol dynamicDataSourceTypeSymbol)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        bool isTestMethod = false;
        List<AttributeData> dynamicDataAttributes = new();
        foreach (AttributeData methodAttribute in methodSymbol.GetAttributes())
        {
            // Current method should be a test method or should inherit from the TestMethod attribute.
            // If it is, the current analyzer will trigger no diagnostic so it exits.
            if (methodAttribute.AttributeClass.Inherits(testMethodAttributeSymbol))
            {
                isTestMethod = true;
            }

            if (SymbolEqualityComparer.Default.Equals(methodAttribute.AttributeClass, dynamicDataAttributeSymbol))
            {
                dynamicDataAttributes.Add(methodAttribute);
            }
        }

        // Check if attribute is set on a test method.
        if (!isTestMethod)
        {
            if (dynamicDataAttributes.Count > 0)
            {
                context.ReportDiagnostic(methodSymbol.CreateDiagnostic(NotTestMethodRule));
            }

            return;
        }

        // Check each data row attribute.
        foreach (AttributeData attribute in dynamicDataAttributes)
        {
            AnalyzeAttribute(context, attribute, methodSymbol, dynamicDataSourceTypeSymbol);
        }
    }

    private static void AnalyzeAttribute(SymbolAnalysisContext context, AttributeData attributeData, IMethodSymbol methodSymbol, INamedTypeSymbol dynamicDataSourceTypeSymbol)
    {
        string? memberName = null;
        int dataSourceType = 0;
        INamedTypeSymbol declaringType = methodSymbol.ContainingType;
        foreach (TypedConstant argument in attributeData.ConstructorArguments)
        {
            if (argument.Type is null)
            {
                continue;
            }

            if (argument.Type.SpecialType == SpecialType.System_String
                && argument.Value is string name)
            {
                memberName = name;
            }
            else if (SymbolEqualityComparer.Default.Equals(argument.Type, dynamicDataSourceTypeSymbol)
                && argument.Value is int dataType)
            {
                dataSourceType = dataType;
            }
            else if (argument.Value is INamedTypeSymbol type)
            {
                declaringType = type;
            }
        }

        // If the member name is not available, bail out.
        if (memberName is null
            || attributeData.ApplicationSyntaxReference?.GetSyntax() is not { } attributeSyntax)
        {
            return;
        }

        // If we cannot find the member on the given type, report a diagnostic.
        if (declaringType.GetMembers(memberName) is { Length: 0 } potentialMembers)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberNotFoundRule, declaringType.Name, memberName));
            return;
        }

        // If there are multiple members with the same name, report a diagnostic. This is not a supported scenario.
        if (potentialMembers.Length > 1)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(FoundTooManyMembersRule, declaringType.Name, memberName));
            return;
        }

        // If the member is a property and the data source type is not set to property, report a diagnostic.
        if (potentialMembers[0].Kind == SymbolKind.Property && dataSourceType is not 0)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberPropertyRule, declaringType.Name, memberName));
            return;
        }

        // If the member is a method and the data source type is not set to method, report a diagnostic.
        if (potentialMembers[0].Kind == SymbolKind.Method && dataSourceType is not 1)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberMethodRule, declaringType.Name, memberName));
            return;
        }

        // Validate member return type.
        var a = 1;
    }
}
