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

        // Attributes applied in the compilation being built have a source location we can point at.
        foreach (AttributeData attribute in context.Compilation.Assembly.GetAttributes()
            .Where(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, assemblyFixtureProviderAttributeSymbol)))
        {
            if (attribute.ApplicationSyntaxReference is null)
            {
                context.ReportNoLocationDiagnostic(Rule);
            }
            else
            {
                context.ReportDiagnostic(attribute.ApplicationSyntaxReference.CreateDiagnostic(Rule, context.CancellationToken));
            }
        }

        // The documented/default usage places [AssemblyFixtureProvider] on a referenced fixture library.
        // Those attributes are declared outside this compilation (no source location), but the referencing
        // Native AOT test project is exactly where the runtime guard silently skips discovery, so report a
        // no-location diagnostic for each referenced assembly that carries the marker.
        foreach (IAssemblySymbol referencedAssembly in context.Compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (referencedAssembly.GetAttributes()
                .Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, assemblyFixtureProviderAttributeSymbol)))
            {
                context.ReportNoLocationDiagnostic(Rule);
            }
        }
    }
}
