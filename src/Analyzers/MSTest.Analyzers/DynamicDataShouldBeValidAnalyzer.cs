// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0018: <inheritdoc cref="Resources.DynamicDataShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class DynamicDataShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private const int DynamicDataSourceTypeProperty = 0;
    private const int DynamicDataSourceTypeMethod = 1;
    private const int DynamicDataSourceTypeAutoDetect = 2;
    private const int DynamicDataSourceTypeField = 3;

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

    internal static readonly DiagnosticDescriptor SourceTypeFieldRule = NotTestMethodRule
        .WithMessage(new(nameof(Resources.DynamicDataShouldBeValidMessageFormat_SourceTypeField), Resources.ResourceManager, typeof(Resources)));

    internal static readonly DiagnosticDescriptor SourceTypeNotPropertyOrMethodRule = NotTestMethodRule
        .WithMessage(new(nameof(Resources.DynamicDataShouldBeValidMessageFormat_SourceTypeNotPropertyMethodOrField), Resources.ResourceManager, typeof(Resources)));

    internal static readonly DiagnosticDescriptor MemberMethodRule = NotTestMethodRule
        .WithMessage(new(nameof(Resources.DynamicDataShouldBeValidMessageFormat_MemberMethod), Resources.ResourceManager, typeof(Resources)));

    internal static readonly DiagnosticDescriptor MemberTypeRule = NotTestMethodRule
        .WithMessage(new(nameof(Resources.DynamicDataShouldBeValidMessageFormat_MemberType), Resources.ResourceManager, typeof(Resources)));

    internal static readonly DiagnosticDescriptor DataMemberSignatureRule = NotTestMethodRule
        .WithMessage(new(nameof(Resources.DynamicDataShouldBeValidMessageFormat_DataMemberSignature), Resources.ResourceManager, typeof(Resources)));

    internal static readonly DiagnosticDescriptor DisplayMethodSignatureRule = NotTestMethodRule
        .WithMessage(new(nameof(Resources.DynamicDataShouldBeValidMessageFormat_DisplayMethodSignature), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        NotTestMethodRule,
        MemberNotFoundRule,
        FoundTooManyMembersRule,
        SourceTypePropertyRule,
        SourceTypeMethodRule,
        SourceTypeNotPropertyOrMethodRule,
        MemberMethodRule,
        MemberTypeRule,
        DataMemberSignatureRule,
        DisplayMethodSignatureRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDynamicDataAttribute, out INamedTypeSymbol? dynamicDataAttributeSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDynamicDataSourceType, out INamedTypeSymbol? dynamicDataSourceTypeSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReflectionMethodInfo, out INamedTypeSymbol? methodInfoTypeSymbol))
            {
                context.RegisterSymbolAction(
                   context => AnalyzeSymbol(context, testMethodAttributeSymbol, dynamicDataAttributeSymbol, dynamicDataSourceTypeSymbol, methodInfoTypeSymbol),
                   SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testMethodAttributeSymbol,
        INamedTypeSymbol dynamicDataAttributeSymbol, INamedTypeSymbol dynamicDataSourceTypeSymbol, INamedTypeSymbol methodInfoTypeSymbol)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        bool isTestMethod = false;
        bool hasDynamicDataAttribute = false;
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
                hasDynamicDataAttribute = true;
                AnalyzeAttribute(context, methodAttribute, methodSymbol, dynamicDataSourceTypeSymbol, methodInfoTypeSymbol);
            }
        }

        // Check if attribute is set on a test method.
        if (!isTestMethod && hasDynamicDataAttribute)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(NotTestMethodRule));
        }
    }

    private static void AnalyzeAttribute(SymbolAnalysisContext context, AttributeData attributeData, IMethodSymbol methodSymbol,
        INamedTypeSymbol dynamicDataSourceTypeSymbol, INamedTypeSymbol methodInfoTypeSymbol)
    {
        if (attributeData.ApplicationSyntaxReference?.GetSyntax() is not { } attributeSyntax)
        {
            return;
        }

        AnalyzeDataSource(context, attributeData, attributeSyntax, methodSymbol, dynamicDataSourceTypeSymbol);
        AnalyzeDisplayNameSource(context, attributeData, attributeSyntax, methodSymbol, methodInfoTypeSymbol);
    }

    private static (ISymbol? Member, bool AreTooMany) TryGetMember(INamedTypeSymbol declaringType, string memberName)
    {
        INamedTypeSymbol? currentType = declaringType;
        while (currentType is not null)
        {
            (ISymbol? Member, bool AreTooMany) result = TryGetMemberCore(currentType, memberName);
            if (result.Member is not null || result.AreTooMany)
            {
                return result;
            }

            // Only continue to look at base types if the member is not found on the current type and we are not hit by "too many methods" rule.
            currentType = currentType.BaseType;
        }

        return (null, false);

        static (ISymbol? Member, bool AreTooMany) TryGetMemberCore(INamedTypeSymbol declaringType, string memberName)
        {
            // If we cannot find the member on the given type, report a diagnostic.
            if (declaringType.GetMembers(memberName) is { Length: 0 } potentialMembers)
            {
                return (null, false);
            }

            ISymbol? potentialProperty = potentialMembers.FirstOrDefault(m => m.Kind == SymbolKind.Property);
            if (potentialProperty is not null)
            {
                return (potentialProperty, false);
            }

            IEnumerable<ISymbol> candidateMethods = potentialMembers.Where(m => m.Kind == SymbolKind.Method);
            if (candidateMethods.Count() > 1)
            {
                // If there are multiple methods with the same name, report a diagnostic. This is not a supported scenario.
                return (null, true);
            }

            return (candidateMethods.FirstOrDefault() ?? potentialMembers[0], false);
        }
    }

    private static void AnalyzeDataSource(SymbolAnalysisContext context, AttributeData attributeData, SyntaxNode attributeSyntax,
        IMethodSymbol methodSymbol, INamedTypeSymbol dynamicDataSourceTypeSymbol)
    {
        string? memberName = null;
        int dataSourceType = DynamicDataSourceTypeAutoDetect;
        int argumentsCount = 0;
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
            else if (argument.Kind != TypedConstantKind.Array &&
                argument.Value is INamedTypeSymbol type)
            {
                declaringType = type;
            }
            else if (argument.Kind == TypedConstantKind.Array)
            {
                argumentsCount = argument.Values.Length;
            }
        }

        // If the member name is not available, bail out.
        if (memberName is null)
        {
            return;
        }

        (ISymbol? member, bool areTooMany) = TryGetMember(declaringType, memberName);

        if (areTooMany)
        {
            // If there are multiple methods with the same name and all are parameterless, report a diagnostic. This is not a supported scenario.
            // Note: This is likely to happen only when they differ in arity (for example, one is non-generic and the other is generic).
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(FoundTooManyMembersRule, declaringType.Name, memberName));
            return;
        }

        if (member is null)
        {
            // If we cannot find the member on the given type, report a diagnostic.
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(MemberNotFoundRule, declaringType.Name, memberName));
            return;
        }

        switch (member.Kind)
        {
            case SymbolKind.Property:
                // If the member is a property and the data source type is not set to property or auto detect, report a diagnostic.
                if (dataSourceType is not (DynamicDataSourceTypeProperty or DynamicDataSourceTypeAutoDetect))
                {
                    context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(SourceTypePropertyRule, declaringType.Name, memberName));
                    return;
                }

                break;
            case SymbolKind.Method:
                // If the member is a method and the data source type is not set to method or auto detect, report a diagnostic.
                if (dataSourceType is not (DynamicDataSourceTypeMethod or DynamicDataSourceTypeAutoDetect))
                {
                    context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(SourceTypeMethodRule, declaringType.Name, memberName));
                    return;
                }

                break;
            case SymbolKind.Field:
                // If the member is a field and the data source type is not set to field or auto detect, report a diagnostic.
                if (dataSourceType is not (DynamicDataSourceTypeField or DynamicDataSourceTypeAutoDetect))
                {
                    context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(SourceTypeFieldRule, declaringType.Name, memberName));
                    return;
                }

                break;
            default:
                context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(SourceTypeNotPropertyOrMethodRule, declaringType.Name, memberName));
                return;
        }

        if (!member.IsStatic)
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(DataMemberSignatureRule, declaringType.Name, memberName));
            return;
        }

        if (member.Kind == SymbolKind.Method
            && member is IMethodSymbol method
            && (method.IsGenericMethod || method.Parameters.Length != argumentsCount))
        {
            context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(DataMemberSignatureRule, declaringType.Name, memberName));
            return;
        }

        // Validate member return type.
        ITypeSymbol? memberTypeSymbol = member.GetMemberType();
        if (memberTypeSymbol is IArrayTypeSymbol)
        {
            return;
        }

        if (memberTypeSymbol is not INamedTypeSymbol memberNamedType
            || memberNamedType.SpecialType == SpecialType.System_String
            || (memberNamedType.SpecialType != SpecialType.System_Collections_IEnumerable
                && !memberNamedType.AllInterfaces.Any(i => i.SpecialType == SpecialType.System_Collections_IEnumerable)))
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
