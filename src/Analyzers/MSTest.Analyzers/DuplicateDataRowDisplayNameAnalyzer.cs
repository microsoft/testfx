// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0087: <inheritdoc cref="Resources.DuplicateDataRowDisplayNameTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class DuplicateDataRowDisplayNameAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.DuplicateDataRowDisplayNameRuleId,
        new LocalizableResourceString(nameof(Resources.DuplicateDataRowDisplayNameTitle), Resources.ResourceManager, typeof(Resources)),
        new LocalizableResourceString(nameof(Resources.DuplicateDataRowDisplayNameMessageFormat), Resources.ResourceManager, typeof(Resources)),
        null,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDataRowAttribute, out INamedTypeSymbol? dataRowAttribute))
            {
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, dataRowAttribute),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol dataRowAttribute)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        var displayNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (AttributeData attribute in methodSymbol.GetAttributes())
        {
            if (!dataRowAttribute.Equals(attribute.AttributeClass, SymbolEqualityComparer.Default))
            {
                continue;
            }

            foreach (KeyValuePair<string, TypedConstant> namedArgument in attribute.NamedArguments)
            {
                if (namedArgument.Key != "DisplayName"
                    || namedArgument.Value.Value is not string displayName
                    || string.IsNullOrWhiteSpace(displayName)
                    || displayNames.Add(displayName))
                {
                    continue;
                }

                if (attribute.ApplicationSyntaxReference is { } syntaxReference)
                {
                    context.ReportDiagnostic(syntaxReference.CreateDiagnostic(Rule, context.CancellationToken, displayName));
                }
                else
                {
                    context.ReportDiagnostic(methodSymbol.CreateDiagnostic(Rule, displayName));
                }
            }
        }
    }
}
