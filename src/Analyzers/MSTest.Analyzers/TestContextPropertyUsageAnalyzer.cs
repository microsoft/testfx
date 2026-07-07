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
/// MSTEST0048: <inheritdoc cref="Resources.TestContextPropertyUsageTitle"/>.
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
        "TestName");

    // Properties that cannot be accessed in assembly initialize or assembly cleanup
    private static readonly ImmutableHashSet<string> RestrictedInAssemblyMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "FullyQualifiedTestClassName");

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

            IPropertySymbol? propertiesSymbol = testContextSymbol.GetMembers("Properties").OfType<IPropertySymbol>().FirstOrDefault();

            context.RegisterOperationAction(context => AnalyzePropertyReference(context, testContextSymbol, propertiesSymbol, assemblyInitializeAttributeSymbol, assemblyCleanupAttributeSymbol, classInitializeAttributeSymbol, classCleanupAttributeSymbol), OperationKind.PropertyReference);
        });
    }

    private static void AnalyzePropertyReference(
        OperationAnalysisContext context,
        INamedTypeSymbol testContextSymbol,
        IPropertySymbol? propertiesSymbol,
        INamedTypeSymbol assemblyInitializeAttributeSymbol,
        INamedTypeSymbol assemblyCleanupAttributeSymbol,
        INamedTypeSymbol classInitializeAttributeSymbol,
        INamedTypeSymbol classCleanupAttributeSymbol)
    {
        var propertyReference = (IPropertyReferenceOperation)context.Operation;

        string? propertyName;
        if (SymbolEqualityComparer.Default.Equals(propertyReference.Property.ContainingType, testContextSymbol))
        {
            // Direct typed property access, e.g. testContext.TestName.
            propertyName = propertyReference.Property.Name;
        }
        else if (TryGetRestrictedPropertiesIndexerKey(propertyReference, propertiesSymbol) is { } key)
        {
            // Indirect access through the string-keyed bag, e.g. testContext.Properties["TestName"].
            propertyName = key;
        }
        else
        {
            return;
        }

        // Check if we're in a forbidden context
        if (context.ContainingSymbol is not IMethodSymbol containingMethod)
        {
            return;
        }

        // Check for assembly initialize/cleanup methods
        bool isAssemblyInitialize = containingMethod.HasAttribute(assemblyInitializeAttributeSymbol);
        bool isAssemblyCleanup = containingMethod.HasAttribute(assemblyCleanupAttributeSymbol);
        bool isClassInitialize = containingMethod.HasAttribute(classInitializeAttributeSymbol);
        bool isClassCleanup = containingMethod.HasAttribute(classCleanupAttributeSymbol);

        bool isInAssemblyMethod = isAssemblyInitialize || isAssemblyCleanup;
        bool isInFixtureMethod = isInAssemblyMethod || isClassInitialize || isClassCleanup;

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

    private static string? TryGetRestrictedPropertiesIndexerKey(IPropertyReferenceOperation propertyReference, IPropertySymbol? propertiesSymbol)
    {
        // We only care about indexer access such as testContext.Properties["TestName"].
        if (propertiesSymbol is null
            || !propertyReference.Property.IsIndexer
            || propertyReference.Arguments.Length != 1
            || propertyReference.Instance is not IPropertyReferenceOperation instanceReference
            || !IsTestContextPropertiesReference(instanceReference.Property, propertiesSymbol))
        {
            return null;
        }

        // The key must be a compile-time constant string.
        return propertyReference.Arguments[0].Value.ConstantValue is { HasValue: true, Value: string key }
            ? key
            : null;
    }

    private static bool IsTestContextPropertiesReference(IPropertySymbol property, IPropertySymbol propertiesSymbol)
    {
        // Match TestContext.Properties as well as overrides of it in derived contexts.
        for (IPropertySymbol? current = property; current is not null; current = current.OverriddenProperty)
        {
            if (SymbolEqualityComparer.Default.Equals(current, propertiesSymbol))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetMethodType(bool isAssemblyInitialize, bool isAssemblyCleanup, bool isClassInitialize, bool isClassCleanup)
        => (isAssemblyInitialize, isAssemblyCleanup, isClassInitialize, isClassCleanup) switch
        {
            (true, _, _, _) => "AssemblyInitialize",
            (_, true, _, _) => "AssemblyCleanup",
            (_, _, true, _) => "ClassInitialize",
            (_, _, _, true) => "ClassCleanup",
            _ => "unknown",
        };
}
