// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0079: <inheritdoc cref="Resources.TestFilterProviderShouldBeValidTitle"/>.
/// </summary>
/// <remarks>
/// <para>
/// <c>[assembly: TestFilterProvider(typeof(MyFilter))]</c> passes the filter type as a
/// <see cref="System.Type"/>, so the compiler accepts any type at all. The adapter validates the type
/// when it materializes the filter and fails the run with <c>UTA074</c>-<c>UTA077</c>; this analyzer
/// reports the same problems at build time, where they are cheap to fix.
/// </para>
/// <para>
/// The generic <c>[assembly: TestFilterProvider&lt;MyFilter&gt;]</c> form (available when targeting
/// .NET) already enforces most of these through its <c>class, ITestFilter, new()</c> constraints, so
/// in practice only the "must not be generic" rule can fire for it. It is still analyzed, together
/// with the non-generic form, for the "at most one provider per assembly" rule: the two attributes
/// are distinct types, so the compiler's own duplicate-attribute check does not see them as
/// duplicates even though the adapter does.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestFilterProviderShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TestFilterProviderShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.TestFilterProviderShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));

    /// <inheritdoc cref="Resources.TestFilterProviderShouldBeValidMessageFormat_Generic"/>
    public static readonly DiagnosticDescriptor GenericRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.TestFilterProviderShouldBeValidRuleId,
        Title,
        new LocalizableResourceString(nameof(Resources.TestFilterProviderShouldBeValidMessageFormat_Generic), Resources.ResourceManager, typeof(Resources)),
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="Resources.TestFilterProviderShouldBeValidMessageFormat_NotInstantiable"/>
    public static readonly DiagnosticDescriptor NotInstantiableRule = GenericRule
        .WithMessage(new(nameof(Resources.TestFilterProviderShouldBeValidMessageFormat_NotInstantiable), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.TestFilterProviderShouldBeValidMessageFormat_NotATestFilter"/>
    public static readonly DiagnosticDescriptor NotATestFilterRule = GenericRule
        .WithMessage(new(nameof(Resources.TestFilterProviderShouldBeValidMessageFormat_NotATestFilter), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.TestFilterProviderShouldBeValidMessageFormat_NoParameterlessConstructor"/>
    public static readonly DiagnosticDescriptor NoParameterlessConstructorRule = GenericRule
        .WithMessage(new(nameof(Resources.TestFilterProviderShouldBeValidMessageFormat_NoParameterlessConstructor), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.TestFilterProviderShouldBeValidMessageFormat_Multiple"/>
    public static readonly DiagnosticDescriptor MultipleRule = GenericRule
        .WithMessage(new(nameof(Resources.TestFilterProviderShouldBeValidMessageFormat_Multiple), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        GenericRule,
        NotInstantiableRule,
        NotATestFilterRule,
        NoParameterlessConstructorRule,
        MultipleRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestFilterProviderAttribute, out INamedTypeSymbol? testFilterProviderAttributeSymbol)
            || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingITestFilter, out INamedTypeSymbol? testFilterSymbol))
        {
            return;
        }

        // Only the test assembly's own markers matter: the adapter reads [TestFilterProvider] from the
        // test source and ignores the attribute on referenced libraries.
        var providers = context.Compilation.Assembly.GetAttributes()
            .Where(attribute => IsTestFilterProvider(attribute.AttributeClass, testFilterProviderAttributeSymbol))
            .ToImmutableArray();

        if (providers.Length > 1)
        {
            foreach (AttributeData provider in providers)
            {
                Report(context, provider, MultipleRule);
            }
        }

        foreach (AttributeData provider in providers)
        {
            AnalyzeProvider(context, provider, testFilterProviderAttributeSymbol, testFilterSymbol);
        }
    }

    private static bool IsTestFilterProvider(INamedTypeSymbol? attributeClass, INamedTypeSymbol testFilterProviderAttributeSymbol)
        // TestFilterProviderAttribute<TFilter> derives from TestFilterProviderAttribute, and the adapter
        // treats both as the same registration, so both shapes are in scope here.
        => attributeClass is not null
            && (SymbolEqualityComparer.Default.Equals(attributeClass, testFilterProviderAttributeSymbol)
                || attributeClass.Inherits(testFilterProviderAttributeSymbol));

    private static void AnalyzeProvider(
        CompilationAnalysisContext context,
        AttributeData provider,
        INamedTypeSymbol testFilterProviderAttributeSymbol,
        INamedTypeSymbol testFilterSymbol)
    {
        if (!TryGetFilterType(provider, testFilterProviderAttributeSymbol, out ITypeSymbol? filterType)
            || filterType.TypeKind == TypeKind.Error)
        {
            // Either the attribute is malformed (the compiler already reports that) or the referenced
            // type does not bind, in which case any diagnostic we add would just be noise.
            return;
        }

        // The checks below mirror the order of the adapter's runtime validation so that the build-time
        // message matches the run-time one (UTA074, UTA075, UTA076, UTA077 respectively).
        if (filterType is not INamedTypeSymbol namedFilterType)
        {
            // Arrays, pointers and the like can never implement ITestFilter.
            Report(context, provider, NotATestFilterRule, filterType.ToDisplayString());
            return;
        }

        string filterTypeName = namedFilterType.ToDisplayString();

        // Both open (typeof(MyFilter<>)) and closed (typeof(MyFilter<int>)) generics are rejected by
        // the adapter, so IsGenericType is the right predicate rather than IsUnboundGenericType.
        if (namedFilterType.IsGenericType)
        {
            Report(context, provider, GenericRule, filterTypeName);
            return;
        }

        // Roslyn models a static class with IsStatic (not IsAbstract), but in IL it is abstract+sealed and
        // the adapter rejects it through its IsAbstract check, so it belongs in this bucket too.
        if (namedFilterType.IsAbstract || namedFilterType.IsStatic || namedFilterType.TypeKind == TypeKind.Interface)
        {
            Report(context, provider, NotInstantiableRule, filterTypeName);
            return;
        }

        if (!namedFilterType.AllInterfaces.Any(@interface => SymbolEqualityComparer.Default.Equals(@interface, testFilterSymbol)))
        {
            Report(context, provider, NotATestFilterRule, filterTypeName);
            return;
        }

        // Every struct has a public parameterless constructor even when Roslyn does not surface one, so the
        // constructor check only applies to classes.
        if (namedFilterType.TypeKind != TypeKind.Struct
            && !namedFilterType.InstanceConstructors.Any(constructor => constructor.Parameters.IsEmpty && constructor.DeclaredAccessibility == Accessibility.Public))
        {
            Report(context, provider, NoParameterlessConstructorRule, filterTypeName);
        }
    }

    private static bool TryGetFilterType(
        AttributeData provider,
        INamedTypeSymbol testFilterProviderAttributeSymbol,
        [NotNullWhen(true)] out ITypeSymbol? filterType)
    {
        if (SymbolEqualityComparer.Default.Equals(provider.AttributeClass, testFilterProviderAttributeSymbol))
        {
            filterType = provider.ConstructorArguments is [{ Kind: TypedConstantKind.Type, Value: ITypeSymbol type }] ? type : null;
            return filterType is not null;
        }

        // The generic form carries the filter type as its single type argument.
        filterType = provider.AttributeClass is { TypeArguments: [ITypeSymbol typeArgument] } ? typeArgument : null;
        return filterType is not null;
    }

    private static void Report(CompilationAnalysisContext context, AttributeData provider, DiagnosticDescriptor rule, params object[] messageArgs)
    {
        if (provider.ApplicationSyntaxReference is null)
        {
            context.ReportNoLocationDiagnostic(rule, messageArgs);
        }
        else
        {
            context.ReportDiagnostic(provider.ApplicationSyntaxReference.CreateDiagnostic(rule, context.CancellationToken, messageArgs));
        }
    }
}
