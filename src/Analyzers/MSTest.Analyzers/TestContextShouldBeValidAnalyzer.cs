// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestContextShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TestContextShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.TestContextShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString NotFieldMessageFormat = new(nameof(Resources.TestContextShouldBeValidMessageFormat_NotField), Resources.ResourceManager, typeof(Resources));
    internal static readonly DiagnosticDescriptor NotFieldRule = new(
        DiagnosticIds.TestContextShouldBeValidRuleId,
        Title,
        NotFieldMessageFormat,
        Categories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        Description,
        $"https://github.com/microsoft/testfx/blob/main/docs/analyzers/{DiagnosticIds.TestContextShouldBeValidRuleId}.md");

    private static readonly LocalizableResourceString PublicMessageFormat = new(nameof(Resources.TestContextShouldBeValidMessageFormat_Public), Resources.ResourceManager, typeof(Resources));
    internal static readonly DiagnosticDescriptor PublicRule = new(
        DiagnosticIds.TestContextShouldBeValidRuleId,
        Title,
        PublicMessageFormat,
        Categories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        Description,
        $"https://github.com/microsoft/testfx/blob/main/docs/analyzers/{DiagnosticIds.TestContextShouldBeValidRuleId}.md");

    private static readonly LocalizableResourceString PublicOrInternalMessageFormat = new(nameof(Resources.TestContextShouldBeValidMessageFormat_PublicOrInternal), Resources.ResourceManager, typeof(Resources));
    internal static readonly DiagnosticDescriptor PublicOrInternalRule = new(
        DiagnosticIds.TestContextShouldBeValidRuleId,
        Title,
        PublicOrInternalMessageFormat,
        Categories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        Description,
        $"https://github.com/microsoft/testfx/blob/main/docs/analyzers/{DiagnosticIds.TestContextShouldBeValidRuleId}.md");

    private static readonly LocalizableResourceString NotStaticMessageFormat = new(nameof(Resources.TestContextShouldBeValidMessageFormat_NotStatic), Resources.ResourceManager, typeof(Resources));
    internal static readonly DiagnosticDescriptor NotStaticRule = new(
        DiagnosticIds.TestContextShouldBeValidRuleId,
        Title,
        NotStaticMessageFormat,
        Categories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        Description,
        $"https://github.com/microsoft/testfx/blob/main/docs/analyzers/{DiagnosticIds.TestContextShouldBeValidRuleId}.md");

    private static readonly LocalizableResourceString NotReadonlyMessageFormat = new(nameof(Resources.TestContextShouldBeValidMessageFormat_NotReadonly), Resources.ResourceManager, typeof(Resources));
    internal static readonly DiagnosticDescriptor NotReadonlyRule = new(
        DiagnosticIds.TestContextShouldBeValidRuleId,
        Title,
        NotReadonlyMessageFormat,
        Categories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        Description,
        $"https://github.com/microsoft/testfx/blob/main/docs/analyzers/{DiagnosticIds.TestContextShouldBeValidRuleId}.md");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(PublicRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext, out var testContextSymbol))
            {
                bool canDiscoverInternals = context.Compilation.CanDiscoverInternals();
                context.RegisterSymbolAction(context => AnalyzeSymbol(context, testContextSymbol, canDiscoverInternals), SymbolKind.Field, SymbolKind.Property);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testContextSymbol, bool canDiscoverInternals)
    {
        if (context.Symbol is IFieldSymbol fieldSymbol)
        {
            AnalyzeFieldSymbol(context, fieldSymbol, testContextSymbol);
            return;
        }

        if (context.Symbol is IPropertySymbol propertySymbol)
        {
            AnalyzePropertySymbol(context, testContextSymbol, canDiscoverInternals, propertySymbol);
            return;
        }

        throw ApplicationStateGuard.Unreachable();
    }

    private static void AnalyzePropertySymbol(SymbolAnalysisContext context, INamedTypeSymbol testContextSymbol, bool canDiscoverInternals, IPropertySymbol propertySymbol)
    {
        if (propertySymbol.GetMethod is null
            || !string.Equals(propertySymbol.Name, "TestContext", StringComparison.OrdinalIgnoreCase)
            || !SymbolEqualityComparer.Default.Equals(testContextSymbol, propertySymbol.GetMethod.ReturnType))
        {
            return;
        }

        if (propertySymbol.GetResultantVisibility() is { } resultantVisibility)
        {
            if (!canDiscoverInternals && resultantVisibility != SymbolVisibility.Public)
            {
                context.ReportDiagnostic(propertySymbol.CreateDiagnostic(PublicRule));
            }
            else if (canDiscoverInternals && resultantVisibility == SymbolVisibility.Private)
            {
                context.ReportDiagnostic(propertySymbol.CreateDiagnostic(PublicOrInternalRule));
            }
        }

        if (propertySymbol.IsStatic)
        {
            context.ReportDiagnostic(propertySymbol.CreateDiagnostic(NotStaticRule));
        }

        if (propertySymbol.SetMethod is null)
        {
            context.ReportDiagnostic(propertySymbol.CreateDiagnostic(NotReadonlyRule));
        }
    }

    private static void AnalyzeFieldSymbol(SymbolAnalysisContext context, IFieldSymbol fieldSymbol, INamedTypeSymbol testContextSymbol)
    {
        if (string.Equals(fieldSymbol.Name, "TestContext", StringComparison.OrdinalIgnoreCase)
            && SymbolEqualityComparer.Default.Equals(testContextSymbol, fieldSymbol.Type))
        {
            context.ReportDiagnostic(fieldSymbol.CreateDiagnostic(NotFieldRule));
        }
    }
}
