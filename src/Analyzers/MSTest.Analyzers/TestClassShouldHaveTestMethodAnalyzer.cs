// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0016: <inheritdoc cref="Resources.TestClassShouldHaveTestMethodTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestClassShouldHaveTestMethodAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TestClassShouldHaveTestMethodTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.TestClassShouldHaveTestMethodDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.TestClassShouldHaveTestMethodMessageFormat), Resources.ResourceManager, typeof(Resources));

    /// <inheritdoc cref="Resources.TestClassShouldHaveTestMethodTitle" />
    public static readonly DiagnosticDescriptor TestClassShouldHaveTestMethodRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.TestClassShouldHaveTestMethodRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Design,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc cref="Resources.TestClassShouldHaveTestMethodMessageFormat_BaseClassHasAssemblyAttributes" />
    public static readonly DiagnosticDescriptor TestClassShouldHaveTestMethodRule_BaseClassHasAssemblyAttributes = TestClassShouldHaveTestMethodRule
        .WithMessage(new LocalizableResourceString(nameof(Resources.TestClassShouldHaveTestMethodMessageFormat_BaseClassHasAssemblyAttributes), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(TestClassShouldHaveTestMethodRule, TestClassShouldHaveTestMethodRule_BaseClassHasAssemblyAttributes);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol))
            {
                INamedTypeSymbol? testMethodAttributeSymbol = context.Compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute);
                INamedTypeSymbol? assemblyInitializationAttributeSymbol = context.Compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyInitializeAttribute);
                INamedTypeSymbol? assemblyCleanupAttributeSymbol = context.Compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyCleanupAttribute);
                INamedTypeSymbol? globalTestInitializeAttributeSymbol = context.Compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingGlobalTestInitializeAttribute);
                INamedTypeSymbol? globalTestCleanupAttributeSymbol = context.Compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingGlobalTestCleanupAttribute);
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testClassAttributeSymbol, testMethodAttributeSymbol, assemblyInitializationAttributeSymbol, assemblyCleanupAttributeSymbol, globalTestInitializeAttributeSymbol, globalTestCleanupAttributeSymbol),
                    SymbolKind.NamedType);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testClassAttributeSymbol, INamedTypeSymbol? testMethodAttributeSymbol,
        INamedTypeSymbol? assemblyInitializationAttributeSymbol, INamedTypeSymbol? assemblyCleanupAttributeSymbol, INamedTypeSymbol? globalTestInitializeAttributeSymbol, INamedTypeSymbol? globalTestCleanupAttributeSymbol)
    {
        var classSymbol = (INamedTypeSymbol)context.Symbol;

        bool isTestClass = false;
        foreach (AttributeData classAttribute in classSymbol.GetAttributes())
        {
            if (classAttribute.AttributeClass.Inherits(testClassAttributeSymbol))
            {
                isTestClass = true;
                break;
            }
        }

        if (!isTestClass)
        {
            return;
        }

        bool hasAssemblyAttributeInCurrentClass = false;
        bool hasAssemblyAttributeInBaseClass = false;
        INamedTypeSymbol? baseClassWithAssemblyAttribute = null;
        bool hasTestMethod = false;

        INamedTypeSymbol? currentType = classSymbol;
        bool isCurrentClass = true;
        do
        {
            foreach (ISymbol classMember in currentType.GetMembers())
            {
                foreach (AttributeData attribute in classMember.GetAttributes())
                {
                    if (attribute.AttributeClass.Inherits(testMethodAttributeSymbol))
                    {
                        hasTestMethod = true;
                    }

                    if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, assemblyInitializationAttributeSymbol)
                        || SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, assemblyCleanupAttributeSymbol)
                        || SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, globalTestInitializeAttributeSymbol)
                        || SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, globalTestCleanupAttributeSymbol))
                    {
                        if (isCurrentClass)
                        {
                            hasAssemblyAttributeInCurrentClass = true;
                        }
                        else
                        {
                            hasAssemblyAttributeInBaseClass = true;
                            baseClassWithAssemblyAttribute ??= currentType;
                        }
                    }
                }
            }

            currentType = currentType.BaseType;
            isCurrentClass = false;
        }
        while (currentType is not null);

        if (hasTestMethod)
        {
            return;
        }

        // Static class with assembly attributes in the current class is valid
        if (classSymbol.IsStatic && hasAssemblyAttributeInCurrentClass)
        {
            return;
        }

        // Non-static class that inherits assembly attributes from base class - suggest making it static
        if (!classSymbol.IsStatic && hasAssemblyAttributeInBaseClass && baseClassWithAssemblyAttribute is not null)
        {
            context.ReportDiagnostic(classSymbol.CreateDiagnostic(TestClassShouldHaveTestMethodRule_BaseClassHasAssemblyAttributes, classSymbol.Name, baseClassWithAssemblyAttribute.Name));

            return;
        }

        // All other cases: class without test methods
        context.ReportDiagnostic(classSymbol.CreateDiagnostic(TestClassShouldHaveTestMethodRule, classSymbol.Name));
    }
}
