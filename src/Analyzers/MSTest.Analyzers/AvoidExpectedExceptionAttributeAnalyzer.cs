// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0006: <inheritdoc cref="Resources.AvoidExpectedExceptionAttributeTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AvoidExpectedExceptionAttributeAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.AvoidExpectedExceptionAttributeTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.AvoidExpectedExceptionAttributeDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.AvoidExpectedExceptionAttributeMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal const string AllowDerivedTypesKey = nameof(AllowDerivedTypesKey);

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AvoidExpectedExceptionAttributeRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Design,
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

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingExpectedExceptionBaseAttribute, out INamedTypeSymbol? expectedExceptionBaseAttributeSymbol))
            {
                context.RegisterSymbolAction(context => AnalyzeSymbol(context, expectedExceptionBaseAttributeSymbol), SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol expectedExceptionBaseAttributeSymbol)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        if (methodSymbol.GetAttributes().FirstOrDefault(
            attr => attr.AttributeClass.Inherits(expectedExceptionBaseAttributeSymbol)) is { } expectedExceptionBaseAttribute)
        {
            bool allowsDerivedTypes = expectedExceptionBaseAttribute.NamedArguments.FirstOrDefault(n => n.Key == "AllowDerivedTypes").Value.Value is true;

            // Assert.ThrowsException checks the exact Exception type. So, we cannot offer a fix to ThrowsException if the user sets AllowDerivedTypes to true.
            context.ReportDiagnostic(
                allowsDerivedTypes
                ? methodSymbol.CreateDiagnostic(Rule, properties: ImmutableDictionary<string, string?>.Empty.Add(AllowDerivedTypesKey, null))
                : methodSymbol.CreateDiagnostic(Rule));
        }
    }
}
