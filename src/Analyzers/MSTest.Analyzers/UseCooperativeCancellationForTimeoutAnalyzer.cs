// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Linq;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0045: <inheritdoc cref="Resources.UseCooperativeCancellationForTimeoutTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class UseCooperativeCancellationForTimeoutAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.UseCooperativeCancellationForTimeoutTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.UseCooperativeCancellationForTimeoutDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.UseCooperativeCancellationForTimeoutMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor UseCooperativeCancellationForTimeoutRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseCooperativeCancellationForTimeoutRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Design,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(UseCooperativeCancellationForTimeoutRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTimeoutAttribute, out INamedTypeSymbol? timeoutAttributeSymbol))
            {
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, timeoutAttributeSymbol),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol timeoutAttributeSymbol)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        AttributeData? timeoutAttribute = null;
        
        foreach (AttributeData attribute in methodSymbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, timeoutAttributeSymbol))
            {
                timeoutAttribute = attribute;
                break;
            }
        }

        // Only analyze methods with timeout attributes
        if (timeoutAttribute == null)
        {
            return;
        }

        // Check if CooperativeCancellation property is explicitly set to true
        bool hasCooperativeCancellationSetToTrue = false;
        if (timeoutAttribute.NamedArguments.Length > 0)
        {
            foreach (var namedArgument in timeoutAttribute.NamedArguments)
            {
                if (namedArgument.Key == "CooperativeCancellation" && namedArgument.Value.Value is bool boolValue && boolValue)
                {
                    hasCooperativeCancellationSetToTrue = true;
                    break;
                }
            }
        }

        // Report diagnostic if CooperativeCancellation is not explicitly set to true
        if (!hasCooperativeCancellationSetToTrue)
        {
            if (timeoutAttribute.ApplicationSyntaxReference?.GetSyntax() is { } syntax)
            {
                context.ReportDiagnostic(syntax.CreateDiagnostic(UseCooperativeCancellationForTimeoutRule));
            }
        }
    }
}