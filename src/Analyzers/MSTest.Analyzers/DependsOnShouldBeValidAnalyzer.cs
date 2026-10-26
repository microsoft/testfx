// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0078: <inheritdoc cref="Resources.DependsOnShouldBeValidTitle"/>.
/// </summary>
/// <remarks>
/// <para>
/// The runtime deliberately treats a dependency that matches no test as a non-fatal warning, so that
/// <c>--filter</c> and single-test runs keep working (see RFC 022). That makes build time the right place
/// to catch the references that are statically decidable - a typo, a rename, a target that is not a test -
/// which is what the <c>nameof</c>/<c>typeof</c> shape of the attribute is for.
/// </para>
/// <para>
/// Every check bails out when the answer depends on information the compilation does not have. The most
/// important case is a test method declared on a non-test base class: its dependencies are resolved against
/// each <em>derived</em> test class at run time, so a reference that names no member of the base class is
/// not necessarily broken and is left alone.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed partial class DependsOnShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.DependsOnShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.DependsOnShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_MethodNotFound"/>
    public static readonly DiagnosticDescriptor MethodNotFoundRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.DependsOnShouldBeValidRuleId,
        Title,
        new LocalizableResourceString(nameof(Resources.DependsOnShouldBeValidMessageFormat_MethodNotFound), Resources.ResourceManager, typeof(Resources)),
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_NotATestMethod"/>
    public static readonly DiagnosticDescriptor NotATestMethodRule = MethodNotFoundRule
        .WithMessage(new(nameof(Resources.DependsOnShouldBeValidMessageFormat_NotATestMethod), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_NotATestClass"/>
    public static readonly DiagnosticDescriptor NotATestClassRule = MethodNotFoundRule
        .WithMessage(new(nameof(Resources.DependsOnShouldBeValidMessageFormat_NotATestClass), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_OtherAssembly"/>
    public static readonly DiagnosticDescriptor OtherAssemblyRule = MethodNotFoundRule
        .WithMessage(new(nameof(Resources.DependsOnShouldBeValidMessageFormat_OtherAssembly), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_AbstractTarget"/>
    public static readonly DiagnosticDescriptor AbstractTargetRule = MethodNotFoundRule
        .WithMessage(new(nameof(Resources.DependsOnShouldBeValidMessageFormat_AbstractTarget), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_SelfReference"/>
    public static readonly DiagnosticDescriptor SelfReferenceRule = MethodNotFoundRule
        .WithMessage(new(nameof(Resources.DependsOnShouldBeValidMessageFormat_SelfReference), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_Cycle"/>
    public static readonly DiagnosticDescriptor CycleRule = MethodNotFoundRule
        .WithMessage(new(nameof(Resources.DependsOnShouldBeValidMessageFormat_Cycle), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_NoEffect"/>
    public static readonly DiagnosticDescriptor NoEffectRule = MethodNotFoundRule
        .WithMessage(new(nameof(Resources.DependsOnShouldBeValidMessageFormat_NoEffect), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc cref="Resources.DependsOnShouldBeValidMessageFormat_NoEffectOnClass"/>
    public static readonly DiagnosticDescriptor NoEffectOnClassRule = MethodNotFoundRule
        .WithMessage(new(nameof(Resources.DependsOnShouldBeValidMessageFormat_NoEffectOnClass), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        MethodNotFoundRule,
        NotATestMethodRule,
        NotATestClassRule,
        OtherAssemblyRule,
        AbstractTargetRule,
        SelfReferenceRule,
        CycleRule,
        NoEffectRule,
        NoEffectOnClassRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDependsOnAttribute, out INamedTypeSymbol? dependsOnAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol))
            {
                return;
            }

            var symbols = new AnalysisSymbols(
                dependsOnAttributeSymbol,
                testMethodAttributeSymbol,
                testClassAttributeSymbol,
                DiscoversInternals(context.Compilation));
            context.RegisterSymbolAction(context => AnalyzeMethod(context, symbols), SymbolKind.Method);
            context.RegisterSymbolAction(context => AnalyzeNamedType(context, symbols), SymbolKind.NamedType);
        });
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, AnalysisSymbols symbols)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;
        List<AttributeData> attributes = GetDependsOnAttributes(typeSymbol, symbols);

        // '[DependsOn]' is not inherited (AttributeUsage(Inherited = false)) and is only read off a class
        // discovery actually runs tests for, so an application on a type that is not a runnable test class -
        // a shared base class whose derived types are the test classes, or an abstract '[TestClass]' whose
        // tests are enumerated under each concrete derived class - never produces an edge.
        if (!IsRunnableTestClass(typeSymbol, symbols))
        {
            ReportNoEffect(context, attributes, typeSymbol.Name, NoEffectOnClassRule);
            return;
        }

        foreach (AttributeData attribute in attributes)
        {
            AnalyzeTarget(context, attribute, symbols, declaringClass: typeSymbol, declaringMethod: null);
        }

        // Every cycle walk starts here, once per test the class effectively runs - including the ones it only
        // inherits. Starting them from AnalyzeMethod instead would key the node off the class that *declares*
        // the method, which is not the class the edge resolves against, and would need a de-duplication rule
        // that no name-based check can get right in the presence of overloads.
        //
        // A class with a duplicated method signature in its hierarchy is skipped: discovery's duplicate
        // fallback then collapses *every* same-named test to the declaration closest to the class, including
        // genuine overloads, and modelling that here would mean baking a run-time quirk into the analyzer.
        // Going quiet is the safe answer, and MSTEST0036 already reports the shadowing that triggers it.
        if (HasDuplicateSignature(typeSymbol, symbols))
        {
            return;
        }

        foreach (string testMethodName in EnumerateTestMethodNames(typeSymbol, symbols))
        {
            AnalyzeCycle(context, symbols, new TestNode(typeSymbol, testMethodName));
        }
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, AnalysisSymbols symbols)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        List<AttributeData> attributes = GetDependsOnAttributes(methodSymbol, symbols);
        if (!methodSymbol.IsTestMethod(symbols.TestMethodAttribute))
        {
            ReportNoEffect(context, attributes, methodSymbol.Name, NoEffectRule);
            return;
        }

        foreach (AttributeData attribute in attributes)
        {
            AnalyzeTarget(context, attribute, symbols, methodSymbol.ContainingType, methodSymbol);
        }
    }

    private static void ReportNoEffect(SymbolAnalysisContext context, List<AttributeData> attributes, string memberName, DiagnosticDescriptor rule)
    {
        foreach (AttributeData attribute in attributes)
        {
            if (attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) is { } attributeSyntax)
            {
                context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(rule, memberName));
            }
        }
    }

    private sealed record AnalysisSymbols(
        INamedTypeSymbol DependsOnAttribute,
        INamedTypeSymbol TestMethodAttribute,
        INamedTypeSymbol TestClassAttribute,
        bool DiscoverInternals);
}
