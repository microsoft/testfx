// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0063: <inheritdoc cref="Resources.TestClassConstructorShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestClassConstructorShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private const string HostInjectionContainingType = "Microsoft.Extensions.DependencyInjection.MSTestHostingServiceCollectionExtensions";
    private const string HostInjectionMethod = "AddMSTestTestClassInjection";
    private const string HostInjectionAssembly = "MSTest.Extensions.Hosting";
    private const string ActivatorUtilitiesConstructorAttribute = "Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructorAttribute";
    private const string HostInjectionEnabledMetadataKey = "MSTestHostTestClassInjectionEnabled";
    private const string UnsupportedModeMetadataKey = "MSTestHostTestClassInjectionUnsupportedMode";
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TestClassConstructorShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.TestClassConstructorShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.TestClassConstructorShouldBeValidMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MultiplePreferredConstructorsMessageFormat = new(nameof(Resources.TestClassConstructorHasMultipleActivatorUtilitiesConstructorsMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString DerivedTestContextMessageFormat = new(nameof(Resources.TestClassConstructorHasDerivedTestContextMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString HostInjectionNotSupportedTitle = new(nameof(Resources.MSTestHostTestClassInjectionNotSupportedTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString HostInjectionNotSupportedDescription = new(nameof(Resources.MSTestHostTestClassInjectionNotSupportedDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString HostInjectionNotSupportedMessageFormat = new(nameof(Resources.MSTestHostTestClassInjectionNotSupportedMessageFormat), Resources.ResourceManager, typeof(Resources));

    /// <inheritdoc cref="Resources.TestClassConstructorShouldBeValidTitle" />
    public static readonly DiagnosticDescriptor TestClassConstructorShouldBeValidRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.TestClassConstructorShouldBeValidRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="Resources.TestClassConstructorShouldBeValidTitle" />
    public static readonly DiagnosticDescriptor MultiplePreferredConstructorsRule =
        TestClassConstructorShouldBeValidRule.WithMessage(MultiplePreferredConstructorsMessageFormat);

    /// <inheritdoc cref="Resources.TestClassConstructorShouldBeValidTitle" />
    public static readonly DiagnosticDescriptor DerivedTestContextRule =
        TestClassConstructorShouldBeValidRule.WithMessage(DerivedTestContextMessageFormat);

    /// <inheritdoc cref="Resources.MSTestHostTestClassInjectionNotSupportedTitle" />
    public static readonly DiagnosticDescriptor MSTestHostTestClassInjectionNotSupportedRule = new(
        DiagnosticIds.MSTestHostTestClassInjectionNotSupportedRuleId,
        HostInjectionNotSupportedTitle,
        HostInjectionNotSupportedMessageFormat,
        Category.Usage.ToString(),
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        HostInjectionNotSupportedDescription,
        $"https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/{DiagnosticIds.MSTestHostTestClassInjectionNotSupportedRuleId.ToLowerInvariant()}",
        [WellKnownDiagnosticTags.CompilationEnd]);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(
            TestClassConstructorShouldBeValidRule,
            MultiplePreferredConstructorsRule,
            DerivedTestContextRule,
            MSTestHostTestClassInjectionNotSupportedRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            if (startContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol))
            {
                INamedTypeSymbol? testContextSymbol = startContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext);
                var testClasses = new ConcurrentBag<INamedTypeSymbol>();
                var hostInjectionLocations = new ConcurrentBag<Location>();

                startContext.RegisterSymbolAction(
                    context => CollectTestClass(context, testClassAttributeSymbol, testClasses),
                    SymbolKind.NamedType);
                startContext.RegisterOperationAction(
                    context => CollectHostInjection(context, hostInjectionLocations),
                    OperationKind.Invocation);
                startContext.RegisterCompilationEndAction(context =>
                {
                    bool hostInjectionEnabled = !hostInjectionLocations.IsEmpty
                        || context.Compilation.SourceModule.ReferencedAssemblySymbols.Any(HasHostInjectionEnabledMetadata);
                    foreach (INamedTypeSymbol testClass in testClasses)
                    {
                        AnalyzeSymbol(context, testClass, testContextSymbol, hostInjectionEnabled);
                    }

                    if (hostInjectionEnabled
                        && GetUnsupportedBuildMode(context.Compilation) is string unsupportedBuildMode)
                    {
                        if (hostInjectionLocations.IsEmpty)
                        {
                            Location location = testClasses.FirstOrDefault()?.Locations.FirstOrDefault() ?? Location.None;
                            context.ReportDiagnostic(Diagnostic.Create(
                                MSTestHostTestClassInjectionNotSupportedRule,
                                location,
                                unsupportedBuildMode));
                        }
                        else
                        {
                            foreach (Location location in hostInjectionLocations)
                            {
                                context.ReportDiagnostic(Diagnostic.Create(
                                    MSTestHostTestClassInjectionNotSupportedRule,
                                    location,
                                    unsupportedBuildMode));
                            }
                        }
                    }
                });
            }
        });
    }

    private static void CollectTestClass(
        SymbolAnalysisContext context,
        INamedTypeSymbol testClassAttributeSymbol,
        ConcurrentBag<INamedTypeSymbol> testClasses)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
        if (namedTypeSymbol.TypeKind == TypeKind.Class
            && !namedTypeSymbol.IsAbstract
            && !namedTypeSymbol.IsStatic
            && namedTypeSymbol.IsTestClass(testClassAttributeSymbol))
        {
            testClasses.Add(namedTypeSymbol);
        }
    }

    private static void CollectHostInjection(
        OperationAnalysisContext context,
        ConcurrentBag<Location> hostInjectionLocations)
    {
        var invocation = (IInvocationOperation)context.Operation;
        IMethodSymbol targetMethod = invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod;
        if (targetMethod.Name == HostInjectionMethod
            && targetMethod.ContainingAssembly.Name == HostInjectionAssembly
            && targetMethod.ContainingType.ToDisplayString() == HostInjectionContainingType
            && targetMethod.Parameters is [IParameterSymbol { Type.SpecialType: SpecialType.None } parameter]
            && parameter.Type.ToDisplayString() == "Microsoft.Extensions.DependencyInjection.IServiceCollection"
            && targetMethod.ReturnType.ToDisplayString() == "Microsoft.Extensions.DependencyInjection.IServiceCollection")
        {
            Location location = invocation.Syntax.GetLocation();
            hostInjectionLocations.Add(location);
        }
    }

    private static void AnalyzeSymbol(
        CompilationAnalysisContext context,
        INamedTypeSymbol namedTypeSymbol,
        INamedTypeSymbol? testContextSymbol,
        bool hostInjectionEnabled)
    {
        bool hasValidConstructor = false;
        List<IMethodSymbol>? publicConstructors = hostInjectionEnabled ? [] : null;

        foreach (IMethodSymbol constructor in namedTypeSymbol.InstanceConstructors)
        {
            // Check if constructor is public
            if (constructor.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            if (hostInjectionEnabled)
            {
                publicConstructors!.Add(constructor);
                hasValidConstructor = true;
                continue;
            }

            // Check if parameterless
            if (constructor.Parameters.Length == 0)
            {
                hasValidConstructor = true;
                break;
            }

            // Check if single parameter of type TestContext
            if (constructor.Parameters.Length == 1
                && testContextSymbol is not null
                && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, testContextSymbol))
            {
                hasValidConstructor = true;
                break;
            }
        }

        if (hostInjectionEnabled && hasValidConstructor)
        {
            IMethodSymbol[] preferredConstructors =
            [
                .. publicConstructors!.Where(static constructor =>
                    constructor.GetAttributes().Any(static attribute =>
                        attribute.AttributeClass?.ToDisplayString() == ActivatorUtilitiesConstructorAttribute)),
            ];
            if (preferredConstructors.Length > 1)
            {
                context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(
                    MultiplePreferredConstructorsRule,
                    namedTypeSymbol.Name));
                return;
            }

            if (testContextSymbol is not null)
            {
                foreach (IParameterSymbol parameter in publicConstructors.SelectMany(static constructor => constructor.Parameters))
                {
                    if (!SymbolEqualityComparer.Default.Equals(parameter.Type, testContextSymbol)
                        && IsDerivedFrom(parameter.Type, testContextSymbol))
                    {
                        context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(
                            DerivedTestContextRule,
                            namedTypeSymbol.Name,
                            parameter.Type.ToDisplayString()));
                        return;
                    }
                }
            }
        }

        if (!hasValidConstructor)
        {
            context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(TestClassConstructorShouldBeValidRule, namedTypeSymbol.Name));
        }
    }

    private static bool IsDerivedFrom(ITypeSymbol type, INamedTypeSymbol baseType)
    {
        for (INamedTypeSymbol? current = (type as INamedTypeSymbol)?.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetUnsupportedBuildMode(Compilation compilation)
        => compilation.Assembly.GetAttributes()
            .Where(static attribute =>
                attribute.AttributeClass?.ToDisplayString() == "System.Reflection.AssemblyMetadataAttribute"
                && attribute.ConstructorArguments.Length == 2
                && attribute.ConstructorArguments[0].Value as string == UnsupportedModeMetadataKey)
            .Select(static attribute => attribute.ConstructorArguments[1].Value as string)
            .FirstOrDefault(static value => value is not null);

    private static bool HasHostInjectionEnabledMetadata(IAssemblySymbol assembly)
        => assembly.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == "System.Reflection.AssemblyMetadataAttribute"
            && attribute.ConstructorArguments.Length == 2
            && attribute.ConstructorArguments[0].Value as string == HostInjectionEnabledMetadataKey
            && attribute.ConstructorArguments[1].Value as string == "true");
}
