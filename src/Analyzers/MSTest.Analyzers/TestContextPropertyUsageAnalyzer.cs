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
/// MSTEST0047: <inheritdoc cref="Resources.TestContextPropertyUsageTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestContextPropertyUsageAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TestContextPropertyUsageTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.TestContextPropertyUsageMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.TestContextPropertyUsageDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.TestContextPropertyUsageRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    // Properties that cannot be accessed in assembly initialize, class initialize, class cleanup, or assembly cleanup
    private static readonly ImmutableHashSet<string> RestrictedInAllFixtureMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "TestData",
        "TestDisplayName",
        "DataRow",
        "DataConnection", 
        "TestName",
        "ManagedMethod");

    // Properties that cannot be accessed in assembly initialize or assembly cleanup
    private static readonly ImmutableHashSet<string> RestrictedInAssemblyMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "FullyQualifiedTestClassName",
        "ManagedType");

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext, out INamedTypeSymbol? testContextSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyInitializeAttribute, out INamedTypeSymbol? assemblyInitializeAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyCleanupAttribute, out INamedTypeSymbol? assemblyCleanupAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassInitializeAttribute, out INamedTypeSymbol? classInitializeAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassCleanupAttribute, out INamedTypeSymbol? classCleanupAttributeSymbol))
            {
                return;
            }

            context.RegisterOperationAction(context => AnalyzePropertyReference(context, testContextSymbol, assemblyInitializeAttributeSymbol, assemblyCleanupAttributeSymbol, classInitializeAttributeSymbol, classCleanupAttributeSymbol), OperationKind.PropertyReference);
        });
    }

    private static void AnalyzePropertyReference(
        OperationAnalysisContext context,
        INamedTypeSymbol testContextSymbol,
        INamedTypeSymbol assemblyInitializeAttributeSymbol,
        INamedTypeSymbol assemblyCleanupAttributeSymbol,
        INamedTypeSymbol classInitializeAttributeSymbol,
        INamedTypeSymbol classCleanupAttributeSymbol)
    {
        var propertyReference = (IPropertyReferenceOperation)context.Operation;

        // Check if the property is a TestContext property
        if (!SymbolEqualityComparer.Default.Equals(propertyReference.Property.ContainingType, testContextSymbol))
        {
            return;
        }

        string propertyName = propertyReference.Property.Name;

        // Check if we're in a forbidden context
        IMethodSymbol? containingMethod = context.ContainingSymbol as IMethodSymbol;
        if (containingMethod is null)
        {
            return;
        }

        // Check for assembly initialize/cleanup methods
        bool isAssemblyInitialize = containingMethod.HasAttribute(assemblyInitializeAttributeSymbol);
        bool isAssemblyCleanup = containingMethod.HasAttribute(assemblyCleanupAttributeSymbol);
        bool isClassInitialize = containingMethod.HasAttribute(classInitializeAttributeSymbol);
        bool isClassCleanup = containingMethod.HasAttribute(classCleanupAttributeSymbol);

        bool isInFixtureMethod = isAssemblyInitialize || isAssemblyCleanup || isClassInitialize || isClassCleanup;
        bool isInAssemblyMethod = isAssemblyInitialize || isAssemblyCleanup;

        // Check if the property is restricted in the current context
        if (isInFixtureMethod && RestrictedInAllFixtureMethods.Contains(propertyName))
        {
            context.ReportDiagnostic(propertyReference.CreateDiagnostic(Rule, propertyName, GetMethodType(isAssemblyInitialize, isAssemblyCleanup, isClassInitialize, isClassCleanup)));
        }
        else if (isInAssemblyMethod && RestrictedInAssemblyMethods.Contains(propertyName))
        {
            context.ReportDiagnostic(propertyReference.CreateDiagnostic(Rule, propertyName, GetMethodType(isAssemblyInitialize, isAssemblyCleanup, isClassInitialize, isClassCleanup)));
        }
    }

    private static string GetMethodType(bool isAssemblyInitialize, bool isAssemblyCleanup, bool isClassInitialize, bool isClassCleanup)
    {
        if (isAssemblyInitialize) return "AssemblyInitialize";
        if (isAssemblyCleanup) return "AssemblyCleanup";
        if (isClassInitialize) return "ClassInitialize";
        if (isClassCleanup) return "ClassCleanup";
        return "unknown";
    }
}