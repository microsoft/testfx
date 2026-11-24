// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0007: <inheritdoc cref="Resources.UseAttributeOnTestMethodAnalyzerTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class UseAttributeOnTestMethodAnalyzer : DiagnosticAnalyzer
{
    private const string OwnerAttributeShortName = "Owner";
    private const string PriorityAttributeShortName = "Priority";
    private const string DescriptionAttributeShortName = "Description";
    private const string TestPropertyAttributeShortName = "TestProperty";
    private const string WorkItemAttributeShortName = "WorkItem";
    private const string ConditionBaseAttributeShortName = "ConditionBaseAttribute";
    private const string ResourceLockAttributeShortName = "ResourceLock";
    private const string DoNotParallelizeAttributeShortName = "DoNotParallelize";

    /// <summary>
    /// Diagnostic property set when adding <c>[TestMethod]</c> is not a valid fix, for example on fixture methods.
    /// </summary>
    internal const string NoAddTestMethodFixKey = "NoAddTestMethodFix";

    private static readonly LocalizableResourceString Description = new(nameof(Resources.UseAttributeOnTestMethodAnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    /// <inheritdoc cref="Resources.UseAttributeOnTestMethodAnalyzerTitle" />
    public static readonly DiagnosticDescriptor OwnerRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseAttributeOnTestMethodRuleId,
        title: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources), OwnerAttributeShortName),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources), OwnerAttributeShortName),
        description: Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="Resources.UseAttributeOnTestMethodAnalyzerTitle" />
    public static readonly DiagnosticDescriptor PriorityRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseAttributeOnTestMethodRuleId,
        title: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources), PriorityAttributeShortName),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources), PriorityAttributeShortName),
        description: Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="Resources.UseAttributeOnTestMethodAnalyzerTitle" />
    public static readonly DiagnosticDescriptor DescriptionRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseAttributeOnTestMethodRuleId,
        title: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources), DescriptionAttributeShortName),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources), DescriptionAttributeShortName),
        description: Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="Resources.UseAttributeOnTestMethodAnalyzerTitle" />
    public static readonly DiagnosticDescriptor TestPropertyRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseAttributeOnTestMethodRuleId,
        title: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources), TestPropertyAttributeShortName),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources), TestPropertyAttributeShortName),
        description: Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="Resources.UseAttributeOnTestMethodAnalyzerTitle" />
    public static readonly DiagnosticDescriptor WorkItemRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseAttributeOnTestMethodRuleId,
        title: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources), WorkItemAttributeShortName),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources), WorkItemAttributeShortName),
        description: Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="Resources.UseAttributeOnTestMethodAnalyzerTitle" />
    public static readonly DiagnosticDescriptor ConditionBaseRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseAttributeOnTestMethodRuleId,
        title: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources), ConditionBaseAttributeShortName),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources), ConditionBaseAttributeShortName),
        description: Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="Resources.UseAttributeOnTestMethodAnalyzerTitle" />
    public static readonly DiagnosticDescriptor ResourceLockOnFixtureRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseAttributeOnTestMethodRuleId,
        title: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources), ResourceLockAttributeShortName),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerFixtureMessageFormat), Resources.ResourceManager, typeof(Resources), ResourceLockAttributeShortName),
        description: Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="Resources.UseAttributeOnTestMethodAnalyzerTitle" />
    public static readonly DiagnosticDescriptor DoNotParallelizeOnFixtureRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseAttributeOnTestMethodRuleId,
        title: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources), DoNotParallelizeAttributeShortName),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerFixtureMessageFormat), Resources.ResourceManager, typeof(Resources), DoNotParallelizeAttributeShortName),
        description: Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly ImmutableDictionary<string, string?> NoAddTestMethodFixProperties =
        ImmutableDictionary<string, string?>.Empty.Add(NoAddTestMethodFixKey, null);

    // Scheduling attributes are only read from test methods (and classes/assemblies) during discovery, so on a
    // fixture method they are silently ignored. They are only reported on fixture methods: on other non-test
    // methods, an inherited declaration may still flow to an overriding test method.
    private static readonly List<(string AttributeFullyQualifiedName, DiagnosticDescriptor Rule)> FixtureRuleTuples =
    [
        (WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingResourceLockAttribute, ResourceLockOnFixtureRule),
        (WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDoNotParallelizeAttribute, DoNotParallelizeOnFixtureRule),
    ];

    private static readonly string[] FixtureAttributeFullyQualifiedNames =
    [
        WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyInitializeAttribute,
        WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyCleanupAttribute,
        WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassInitializeAttribute,
        WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassCleanupAttribute,
        WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestInitializeAttribute,
        WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestCleanupAttribute,
        WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingGlobalTestInitializeAttribute,
        WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingGlobalTestCleanupAttribute,
    ];

    // IMPORTANT: Remember to add any new rule to the rule tuple.
    // IMPORTANT: The order is important. For example, Owner, Priority, and Description are also TestProperty. We report the first violation and then bail-out.
    //            It may be a better idea to consolidate OwnerRule, PriorityRule, DescriptionRule, and TestPropertyRule into a single rule.
    private static readonly List<(string AttributeFullyQualifiedName, DiagnosticDescriptor Rule)> RuleTuples =
    [
        (WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingOwnerAttribute, OwnerRule),
        (WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingPriorityAttribute, PriorityRule),
        (WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDescriptionAttribute, DescriptionRule),
        (WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestPropertyAttribute, TestPropertyRule),
        (WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingWorkItemAttribute, WorkItemRule),
        (WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingConditionBaseAttribute, ConditionBaseRule),
    ];

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            OwnerRule,
            PriorityRule,
            DescriptionRule,
            TestPropertyRule,
            WorkItemRule,
            ResourceLockOnFixtureRule,
            DoNotParallelizeOnFixtureRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            // No need to register any action if we don't find the TestMethodAttribute symbol since
            // the current analyzer checks if certain test attributes are applied on test methods. No
            // test methods, nothing to check.
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(
                WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute,
                out INamedTypeSymbol? testMethodAttributeSymbol))
            {
                return;
            }

            // Get a list of attributes and associated rules that are found in the current compilation
            // context.
            List<(INamedTypeSymbol AttributeSymbol, DiagnosticDescriptor Rule)> attributeRuleTuples = [];
            foreach ((string attributeFullyQualifiedName, DiagnosticDescriptor rule) in RuleTuples)
            {
                if (context.Compilation.TryGetOrCreateTypeByMetadataName(attributeFullyQualifiedName, out INamedTypeSymbol? attributeSymbol))
                {
                    attributeRuleTuples.Add((attributeSymbol, rule));
                }
            }

            List<(INamedTypeSymbol AttributeSymbol, DiagnosticDescriptor Rule)> fixtureAttributeRuleTuples = [];
            List<INamedTypeSymbol> fixtureAttributeSymbols = [];
            foreach ((string attributeFullyQualifiedName, DiagnosticDescriptor rule) in FixtureRuleTuples)
            {
                if (context.Compilation.TryGetOrCreateTypeByMetadataName(attributeFullyQualifiedName, out INamedTypeSymbol? attributeSymbol))
                {
                    fixtureAttributeRuleTuples.Add((attributeSymbol, rule));
                }
            }

            if (fixtureAttributeRuleTuples.Count > 0)
            {
                foreach (string fixtureAttributeFullyQualifiedName in FixtureAttributeFullyQualifiedNames)
                {
                    if (context.Compilation.TryGetOrCreateTypeByMetadataName(fixtureAttributeFullyQualifiedName, out INamedTypeSymbol? fixtureAttributeSymbol))
                    {
                        fixtureAttributeSymbols.Add(fixtureAttributeSymbol);
                    }
                }
            }

            // Should there be any test attributes present in the current assembly, we call upon
            // further analysis to make sure they are used as intended, see registered action for
            // more info.
            if (attributeRuleTuples.Count > 0 || (fixtureAttributeRuleTuples.Count > 0 && fixtureAttributeSymbols.Count > 0))
            {
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testMethodAttributeSymbol, attributeRuleTuples, fixtureAttributeSymbols, fixtureAttributeRuleTuples),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(
        SymbolAnalysisContext context,
        INamedTypeSymbol testMethodAttributeSymbol,
        IEnumerable<(INamedTypeSymbol AttributeSymbol, DiagnosticDescriptor Rule)> attributeRuleTuples,
        List<INamedTypeSymbol> fixtureAttributeSymbols,
        List<(INamedTypeSymbol AttributeSymbol, DiagnosticDescriptor Rule)> fixtureAttributeRuleTuples)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        bool isFixtureMethod = false;
        List<(AttributeData AttributeData, DiagnosticDescriptor Rule)> attributes = [];
        List<(AttributeData AttributeData, DiagnosticDescriptor Rule)>? fixtureOnlyAttributes = null;
        foreach (AttributeData methodAttribute in methodSymbol.GetAttributes())
        {
            // Current method should be a test method or should inherit from the TestMethod attribute.
            // If it is, the current analyzer will trigger no diagnostic so it exits.
            if (methodAttribute.AttributeClass.Inherits(testMethodAttributeSymbol))
            {
                return;
            }

            if (!isFixtureMethod)
            {
                foreach (INamedTypeSymbol fixtureAttributeSymbol in fixtureAttributeSymbols)
                {
                    if (methodAttribute.AttributeClass.Inherits(fixtureAttributeSymbol))
                    {
                        isFixtureMethod = true;
                        break;
                    }
                }
            }

            foreach ((INamedTypeSymbol attributeSymbol, DiagnosticDescriptor rule) in fixtureAttributeRuleTuples)
            {
                if (methodAttribute.AttributeClass.Inherits(attributeSymbol))
                {
                    (fixtureOnlyAttributes ??= []).Add((methodAttribute, rule));
                    break;
                }
            }

            // Get all test attributes decorating the current method.
            foreach ((INamedTypeSymbol attributeSymbol, DiagnosticDescriptor rule) in attributeRuleTuples)
            {
                if (methodAttribute.AttributeClass.Inherits(attributeSymbol))
                {
                    attributes.Add((methodAttribute, rule));
                    break;
                }
            }
        }

        // If there's any test attributes decorating a non-test method we report diagnostics on the
        // said test attribute.
        foreach ((AttributeData AttributeData, DiagnosticDescriptor Rule) attribute in attributes)
        {
            if (attribute.AttributeData.ApplicationSyntaxReference?.GetSyntax() is { } syntax)
            {
                if (ReferenceEquals(attribute.Rule, ConditionBaseRule) && attribute.AttributeData.AttributeClass is { Name: { } attributeName })
                {
                    context.ReportDiagnostic(syntax.CreateDiagnostic(attribute.Rule, attributeName));
                }
                else
                {
                    context.ReportDiagnostic(syntax.CreateDiagnostic(attribute.Rule));
                }
            }
        }

        if (!isFixtureMethod || fixtureOnlyAttributes is null)
        {
            return;
        }

        // Making a fixture method a test is never the intended fix, and moving the attribute to the class would
        // change which tests are serialized and how long the lock is held, so no automatic fix is offered.
        foreach ((AttributeData attributeData, DiagnosticDescriptor rule) in fixtureOnlyAttributes)
        {
            if (attributeData.ApplicationSyntaxReference?.GetSyntax() is { } syntax)
            {
                context.ReportDiagnostic(syntax.CreateDiagnostic(rule, properties: NoAddTestMethodFixProperties));
            }
        }
    }
}
