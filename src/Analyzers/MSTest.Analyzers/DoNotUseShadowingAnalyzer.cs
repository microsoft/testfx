// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0036: <inheritdoc cref="Resources.DoNotUseShadowingTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class DoNotUseShadowingAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.DoNotUseShadowingTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.DoNotUseShadowingDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.DoNotUseShadowingMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor DoNotUseShadowingRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.DoNotUseShadowingRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Design,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(DoNotUseShadowingRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol))
            {
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testClassAttributeSymbol),
                    SymbolKind.NamedType);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testClassAttributeSymbol)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
        if (namedTypeSymbol.TypeKind != TypeKind.Class
            || !namedTypeSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, testClassAttributeSymbol)))
        {
            return;
        }

        INamedTypeSymbol? currentType = namedTypeSymbol.BaseType;
        var baseClassesMembers = new List<ISymbol>();
        while (currentType != null)
        {
            baseClassesMembers.AddRange(currentType.GetMembers());
            currentType = currentType.BaseType;
        }

        if (baseClassesMembers.Count == 0)
        {
            return;
        }

        foreach (ISymbol member in namedTypeSymbol.GetMembers())
        {
            foreach (ISymbol baseMember in baseClassesMembers)
            {
                ISymbol originalMemberDefinition = member.OriginalDefinition;
                ISymbol originalBaseMemberSymbol = baseMember.OriginalDefinition;

                // Compare the original definitions
                if (!SymbolEqualityComparer.Default.Equals(originalMemberDefinition, originalBaseMemberSymbol))
                {
                    context.ReportDiagnostic(member.CreateDiagnostic(DoNotUseShadowingRule, member.Name));
                    return;
                }
            }
        }
    }
}
