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
    private const int DynamicDataSourceTypeProperty = 0;
    private const int DynamicDataSourceTypeMethod = 1;

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

    internal static readonly DiagnosticDescriptor SourceTypePropertyRule = NotTestMethodRule
        .WithMessage(new(nameof(Resources.DynamicDataShouldBeValidMessageFormat_SourceTypeProperty), Resources.ResourceManager, typeof(Resources)));

    internal static readonly DiagnosticDescriptor SourceTypeMethodRule = NotTestMethodRule
        .WithMessage(new(nameof(Resources.DynamicDataShouldBeValidMessageFormat_SourceTypeMethod), Resources.ResourceManager, typeof(Resources)));

    internal static readonly DiagnosticDescriptor MemberMethodRule = NotTestMethodRule
        .WithMessage(new(nameof(Resources.DynamicDataShouldBeValidMessageFormat_MemberMethod), Resources.ResourceManager, typeof(Resources)));

    internal static readonly DiagnosticDescriptor MemberTypeRule = NotTestMethodRule
        .WithMessage(new(nameof(Resources.DynamicDataShouldBeValidMessageFormat_MemberType), Resources.ResourceManager, typeof(Resources)));

    internal static readonly DiagnosticDescriptor DataMemberSignatureRule = NotTestMethodRule
        .WithMessage(new(nameof(Resources.DynamicDataShouldBeValidMessageFormat_DataMemberSignature), Resources.ResourceManager, typeof(Resources)));

    internal static readonly DiagnosticDescriptor DisplayMethodSignatureRule = NotTestMethodRule
        .WithMessage(new(nameof(Resources.DynamicDataShouldBeValidMessageFormat_DisplayMethodSignature), Resources.ResourceManager, typeof(Resources)));

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
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDynamicDataSourceType, out INamedTypeSymbol? dynamicDataSourceTypeSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIEnumerable1, out INamedTypeSymbol? ienumerableTypeSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesITuple, out INamedTypeSymbol? itupleTypeSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReflectionMethodInfo, out INamedTypeSymbol? methodInfoTypeSymbol))
            {
                context.RegisterSymbolAction(
                   context => AnalyzeSymbol(context, testMethodAttributeSymbol, dynamicDataAttributeSymbol, dynamicDataSourceTypeSymbol,
                    ienumerableTypeSymbol, itupleTypeSymbol, methodInfoTypeSymbol),
                   SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testMethodAttributeSymbol,
        INamedTypeSymbol dynamicDataAttributeSymbol, INamedTypeSymbol dynamicDataSourceTypeSymbol, INamedTypeSymbol ienumerableTypeSymbol,
        INamedTypeSymbol itupleTypeSymbol, INamedTypeSymbol methodInfoTypeSymbol)
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
            AnalyzeAttribute(context, attribute, methodSymbol, dynamicDataSourceTypeSymbol, ienumerableTypeSymbol, itupleTypeSymbol,
                methodInfoTypeSymbol);
        }
    }

    private static void AnalyzeAttribute(SymbolAnalysisContext context, AttributeData attributeData, IMethodSymbol methodSymbol,
        INamedTypeSymbol dynamicDataSourceTypeSymbol, INamedTypeSymbol ienumerableTypeSymbol, INamedTypeSymbol itupleTypeSymbol,
        INamedTypeSymbol methodInfoTypeSymbol)
    {
        if (attributeData.ApplicationSyntaxReference?.GetSyntax() is not { } attributeSyntax)
        {
            return;
        }

        AnalyzeDataSource(context, attributeData, attributeSyntax, methodSymbol, dynamicDataSourceTypeSymbol, ienumerableTypeSymbol,
            itupleTypeSymbol);
        AnalyzeDisplayNameSource(context, attributeData, attributeSyntax, methodSymbol, methodInfoTypeSymbol);
    }

    private static void AnalyzeDataSource(SymbolAnalysisContext context, AttributeData attributeData, SyntaxNode attributeSyntax,
        IMethodSymbol methodSymbol, INamedTypeSymbol dynamicDataSourceTypeSymbol, INamedTypeSymbol ienumerableTypeSymbol,
        INamedTypeSymbol itupleTypeSymbol)
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
        if (memberName is null)
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

        ISymbol member = potentialMembers[0];

        // If the member is a property and the data source type is not set to property, report a diagnostic.
        if (member.Kind == SymbolKind.Property && dataSourceType is not DynamicDataSourceTypeProperty)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(SourceTypePropertyRule, declaringType.Name, memberName));
            return;
        }

        // If the member is a method and the data source type is not set to method, report a diagnostic.
        if (member.Kind == SymbolKind.Method && dataSourceType is not DynamicDataSourceTypeMethod)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(SourceTypeMethodRule, declaringType.Name, memberName));
            return;
        }

        if (!member.IsStatic
            || member.DeclaredAccessibility != Accessibility.Public)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(DataMemberSignatureRule, declaringType.Name, memberName));
            return;
        }

        if (member.Kind == SymbolKind.Method
            && member is IMethodSymbol method
            && (method.IsGenericMethod || method.Parameters.Length != 0))
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(DataMemberSignatureRule, declaringType.Name, memberName));
            return;
        }

        // Validate member return type.
        if (member.GetMemberType() is not INamedTypeSymbol memberType)
        {
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(memberType.ConstructedFrom, ienumerableTypeSymbol)
            || memberType.TypeArguments.Length != 1)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberTypeRule, declaringType.Name, memberName));
            return;
        }

        ITypeSymbol collectionBoundType = memberType.TypeArguments[0];
        if (!collectionBoundType.Inherits(itupleTypeSymbol)
            && (collectionBoundType is not IArrayTypeSymbol arrayTypeSymbol || arrayTypeSymbol.ElementType.SpecialType != SpecialType.System_Object))
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberTypeRule, declaringType.Name, memberName));
        }
    }

    private static void AnalyzeDisplayNameSource(SymbolAnalysisContext context, AttributeData attributeData, SyntaxNode attributeSyntax,
        IMethodSymbol methodSymbol, INamedTypeSymbol methodInfoTypeSymbol)
    {
        string? memberName = null;
        INamedTypeSymbol declaringType = methodSymbol.ContainingType;
        foreach (KeyValuePair<string, TypedConstant> namedArgument in attributeData.NamedArguments)
        {
            if (namedArgument.Value.Type is null)
            {
                continue;
            }

            if (namedArgument.Key == "DynamicDataDisplayName"
                && namedArgument.Value.Value is string name)
            {
                memberName = name;
            }
            else if (namedArgument.Key == "DynamicDataDisplayNameDeclaringType"
                && namedArgument.Value.Value is INamedTypeSymbol type)
            {
                declaringType = type;
            }
        }

        // If the member name is not available, bail out.
        if (memberName is null)
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

        ISymbol member = potentialMembers[0];

        if (member is not IMethodSymbol displayNameMethod)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberMethodRule, declaringType.Name, memberName));
            return;
        }

        // Validate signature
        if (!displayNameMethod.IsStatic
            || displayNameMethod.DeclaredAccessibility != Accessibility.Public
            || displayNameMethod.ReturnType.SpecialType != SpecialType.System_String
            || displayNameMethod.Parameters.Length != 2
            || !SymbolEqualityComparer.Default.Equals(displayNameMethod.Parameters[0].Type, methodInfoTypeSymbol)
            || displayNameMethod.Parameters[1].Type is not IArrayTypeSymbol arrayTypeSymbol
            || arrayTypeSymbol.ElementType.SpecialType != SpecialType.System_Object)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(DisplayMethodSignatureRule, declaringType.Name, memberName));
            return;
        }
    }
}
