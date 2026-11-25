// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0092: <inheritdoc cref="Resources.CombinatorialDataShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class CombinatorialDataShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.CombinatorialDataShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.CombinatorialDataShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly DiagnosticDescriptor MultipleProvidersRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.CombinatorialDataShouldBeValidRuleId,
        Title,
        new LocalizableResourceString(nameof(Resources.CombinatorialDataShouldBeValidMessageFormat_MultipleProviders), Resources.ResourceManager, typeof(Resources)),
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedTypeRule = MultipleProvidersRule.WithMessage(
        new(nameof(Resources.CombinatorialDataShouldBeValidMessageFormat_UnsupportedType), Resources.ResourceManager, typeof(Resources)));

    private static readonly DiagnosticDescriptor InvalidRangeRule = MultipleProvidersRule.WithMessage(
        new(nameof(Resources.CombinatorialDataShouldBeValidMessageFormat_InvalidRange), Resources.ResourceManager, typeof(Resources)));

    private static readonly DiagnosticDescriptor InvalidRandomRule = MultipleProvidersRule.WithMessage(
        new(nameof(Resources.CombinatorialDataShouldBeValidMessageFormat_InvalidRandom), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = [MultipleProvidersRule, UnsupportedTypeRule, InvalidRangeRule, InvalidRandomRule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(context =>
        {
            Compilation compilation = context.Compilation;
            INamedTypeSymbol? dataAttribute = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingCombinatorialCombinatorialDataAttribute);
            INamedTypeSymbol? valuesProvider = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingCombinatorialICombinatorialValuesProvider);
            INamedTypeSymbol? testMethodAttribute = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute);
            if (dataAttribute is null || valuesProvider is null || testMethodAttribute is null)
            {
                return;
            }

            INamedTypeSymbol? rangeAttribute = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingCombinatorialCombinatorialRangeAttribute);
            INamedTypeSymbol? randomAttribute = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingCombinatorialCombinatorialRandomDataAttribute);
            INamedTypeSymbol? cancellationToken = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingCancellationToken);
            context.RegisterSymbolAction(
                context => AnalyzeMethod(context, dataAttribute, valuesProvider, testMethodAttribute, rangeAttribute, randomAttribute, cancellationToken),
                SymbolKind.Method);
        });
    }

    private static void AnalyzeMethod(
        SymbolAnalysisContext context,
        INamedTypeSymbol dataAttribute,
        INamedTypeSymbol valuesProvider,
        INamedTypeSymbol testMethodAttribute,
        INamedTypeSymbol? rangeAttribute,
        INamedTypeSymbol? randomAttribute,
        INamedTypeSymbol? cancellationToken)
    {
        var method = (IMethodSymbol)context.Symbol;
        ImmutableArray<AttributeData> attributes = method.GetAttributes();
        if (!attributes.Any(attribute => attribute.AttributeClass.Inherits(testMethodAttribute))
            || !attributes.Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, dataAttribute)))
        {
            return;
        }

        for (int index = 0; index < method.Parameters.Length; index++)
        {
            IParameterSymbol parameter = method.Parameters[index];
            ImmutableArray<AttributeData> parameterAttributes = parameter.GetAttributes();
            if (parameterAttributes.Any(attribute => attribute.AttributeClass is null || attribute.AttributeClass.TypeKind == TypeKind.Error))
            {
                continue;
            }

            var providers = parameterAttributes
                .Where(attribute => attribute.AttributeClass!.AllInterfaces.Any(type => SymbolEqualityComparer.Default.Equals(type, valuesProvider)))
                .ToList();
            if (providers.Count > 1)
            {
                context.ReportDiagnostic(parameter.CreateDiagnostic(MultipleProvidersRule, parameter.Name));
            }
            else if (providers.Count == 0
                && !(index == method.Parameters.Length - 1
                    && parameter.RefKind == RefKind.None
                    && SymbolEqualityComparer.Default.Equals(parameter.Type, cancellationToken))
                && !HasUnresolvedType(parameter.Type)
                && !CanInferValues(parameter))
            {
                string typeName = parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                if (parameter.RefKind != RefKind.None)
                {
                    typeName += "&";
                }

                context.ReportDiagnostic(parameter.CreateDiagnostic(UnsupportedTypeRule, parameter.Name, typeName));
            }

            foreach (AttributeData provider in providers)
            {
                DiagnosticDescriptor? rule = null;
                if (SymbolEqualityComparer.Default.Equals(provider.AttributeClass, rangeAttribute) && IsInvalidRange(provider))
                {
                    rule = InvalidRangeRule;
                }
                else if (SymbolEqualityComparer.Default.Equals(provider.AttributeClass, randomAttribute) && IsInvalidRandom(provider))
                {
                    rule = InvalidRandomRule;
                }

                if (rule is not null && provider.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) is { } syntax)
                {
                    context.ReportDiagnostic(syntax.CreateDiagnostic(rule));
                }
            }
        }
    }

    private static bool HasUnresolvedType(ITypeSymbol type)
        => type.TypeKind == TypeKind.Error
            || type is ITypeParameterSymbol { TypeParameterKind: TypeParameterKind.Type }
            || (type is IArrayTypeSymbol array && HasUnresolvedType(array.ElementType))
            || (type is IPointerTypeSymbol pointer && HasUnresolvedType(pointer.PointedAtType))
            || (type is INamedTypeSymbol named && named.TypeArguments.Any(HasUnresolvedType));

    private static bool CanInferValues(IParameterSymbol parameter)
    {
        if (parameter.RefKind != RefKind.None)
        {
            return false;
        }

        ITypeSymbol type = parameter.Type;
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            type = nullable.TypeArguments[0];
        }

        return type.SpecialType is SpecialType.System_Boolean or SpecialType.System_Int32 || type.TypeKind == TypeKind.Enum;
    }

    private static bool IsInvalidRange(AttributeData attribute)
    {
        ImmutableArray<TypedConstant> arguments = attribute.ConstructorArguments;
        if (arguments.Length == 2 && arguments[0].Value is int signedFrom && arguments[1].Value is int signedCount)
        {
            return signedCount < 1 || ((long)signedFrom + signedCount - 1) > int.MaxValue;
        }

        if (arguments.Length == 2 && arguments[0].Value is uint unsignedFrom && arguments[1].Value is uint unsignedCount)
        {
            return unsignedCount < 1 || ((ulong)unsignedFrom + unsignedCount - 1) > uint.MaxValue || unsignedCount > int.MaxValue;
        }

        if (arguments.Length == 3 && arguments[0].Value is int from && arguments[1].Value is int to && arguments[2].Value is int step)
        {
            return step == 0 || (step > 0 && to < from) || (step < 0 && to > from) || ((((long)to - from) / step) + 1) > int.MaxValue;
        }

        if (arguments.Length == 3 && arguments[0].Value is uint start && arguments[1].Value is uint end && arguments[2].Value is uint increment)
        {
            ulong difference = start < end ? (ulong)end - start : (ulong)start - end;
            return increment == 0 || ((difference / increment) + 1) > int.MaxValue;
        }

        return false;
    }

    private static bool IsInvalidRandom(AttributeData attribute)
    {
        bool hasCount = TryGetNamedInt32(attribute, "Count", out int count);
        bool hasMinimum = TryGetNamedInt32(attribute, "Minimum", out int minimum);
        bool hasMaximum = TryGetNamedInt32(attribute, "Maximum", out int maximum);
        return (hasCount && count < 1)
            || (hasMinimum && hasMaximum && minimum > maximum)
            || (hasCount && hasMinimum && hasMaximum && count > ((long)maximum - minimum + 1));
    }

    private static bool TryGetNamedInt32(AttributeData attribute, string name, out int value)
    {
        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            if (argument.Key == name && argument.Value.Value is int number)
            {
                value = number;
                return true;
            }
        }

        value = 0;
        return false;
    }
}
