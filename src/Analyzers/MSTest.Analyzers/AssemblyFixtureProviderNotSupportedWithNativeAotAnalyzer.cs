// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0072: <inheritdoc cref="Resources.AssemblyFixtureProviderNotSupportedWithNativeAotTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AssemblyFixtureProviderNotSupportedWithNativeAotAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.AssemblyFixtureProviderNotSupportedWithNativeAotTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.AssemblyFixtureProviderNotSupportedWithNativeAotMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.AssemblyFixtureProviderNotSupportedWithNativeAotDescription), Resources.ResourceManager, typeof(Resources));

    /// <inheritdoc cref="Resources.AssemblyFixtureProviderNotSupportedWithNativeAotTitle" />
    public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AssemblyFixtureProviderNotSupportedWithNativeAotRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        // [AssemblyFixtureProvider] discovery walks the runtime assembly reference graph, which is only
        // supported when the runtime can generate dynamic code. Under Native AOT the feature is skipped
        // at runtime, so warn the user that the attribute has no effect there. Only report when the
        // project opts into Native AOT.
        if (!(context.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.PublishAot", out string? publishAot)
            && bool.TryParse(publishAot, out bool publishAotValue)
            && publishAotValue))
        {
            return;
        }

        INamedTypeSymbol? assemblyFixtureProviderAttributeSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyFixtureProviderAttribute);
        if (assemblyFixtureProviderAttributeSymbol is null)
        {
            return;
        }

        foreach (AttributeData attribute in context.Compilation.Assembly.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, assemblyFixtureProviderAttributeSymbol)
                && attribute.ApplicationSyntaxReference is not null)
            {
                context.ReportDiagnostic(attribute.ApplicationSyntaxReference.CreateDiagnostic(Rule, context.CancellationToken));
            }
        }
    }
}
