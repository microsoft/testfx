// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0001: <inheritdoc cref="Resources.UseParallelizeAttributeAnalyzerTitle"/>.
/// MSTEST0058: <inheritdoc cref="Resources.DoNotUseParallelizeAndDoNotParallelizeTogetherTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class UseParallelizeAttributeAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.UseParallelizeAttributeAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.UseParallelizeAttributeAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.UseParallelizeAttributeAnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString BothAttributesTitle = new(nameof(Resources.DoNotUseParallelizeAndDoNotParallelizeTogetherTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString BothAttributesMessageFormat = new(nameof(Resources.DoNotUseParallelizeAndDoNotParallelizeTogetherMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString BothAttributesDescription = new(nameof(Resources.DoNotUseParallelizeAndDoNotParallelizeTogetherDescription), Resources.ResourceManager, typeof(Resources));

    /// <inheritdoc cref="Resources.UseParallelizeAttributeAnalyzerTitle" />
    public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseParallelizedAttributeRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Performance,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="Resources.DoNotUseParallelizeAndDoNotParallelizeTogetherTitle" />
    public static readonly DiagnosticDescriptor DoNotUseBothAttributesRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.DoNotUseParallelizeAndDoNotParallelizeTogetherRuleId,
        BothAttributesTitle,
        BothAttributesMessageFormat,
        BothAttributesDescription,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule, DoNotUseBothAttributesRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        bool hasTestAdapter = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.IsMSTestTestAdapterReferenced", out string? isAdapterReferenced) &&
            bool.TryParse(isAdapterReferenced, out bool isAdapterReferencedValue) &&
            isAdapterReferencedValue;

        if (!hasTestAdapter)
        {
            // We shouldn't produce a diagnostic if only the test framework is referenced, but not the adapter.
            return;
        }

        INamedTypeSymbol? parallelizeAttributeSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingParallelizeAttribute);
        INamedTypeSymbol? doNotParallelizeAttributeSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDoNotParallelizeAttribute);

        bool hasParallelizeAttribute = false;
        bool hasDoNotParallelizeAttribute = false;
        foreach (AttributeData attribute in context.Compilation.Assembly.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, parallelizeAttributeSymbol))
            {
                hasParallelizeAttribute = true;
            }

            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, doNotParallelizeAttributeSymbol))
            {
                hasDoNotParallelizeAttribute = true;
            }
        }

        if (hasParallelizeAttribute && hasDoNotParallelizeAttribute)
        {
            // Both attributes are present - this is an error
            context.ReportNoLocationDiagnostic(DoNotUseBothAttributesRule);
        }
        else if (!hasParallelizeAttribute && !hasDoNotParallelizeAttribute)
        {
            // We cannot provide any good location for assembly level missing attributes
            context.ReportNoLocationDiagnostic(Rule);
        }
    }
}
