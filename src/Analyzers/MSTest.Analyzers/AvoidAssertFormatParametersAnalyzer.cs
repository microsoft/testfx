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
/// MSTEST0053: <inheritdoc cref="Resources.AvoidAssertFormatParametersTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AvoidAssertFormatParametersAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.AvoidAssertFormatParametersTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.AvoidAssertFormatParametersMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AvoidAssertFormatParametersRuleId,
        Title,
        MessageFormat,
        null,
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

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert, out INamedTypeSymbol? assertSymbol) &&
                context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingCollectionAssert, out INamedTypeSymbol? collectionAssertSymbol) &&
                context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingStringAssert, out INamedTypeSymbol? stringAssertSymbol))
            {
                context.RegisterOperationAction(context => AnalyzeOperation(context, assertSymbol, collectionAssertSymbol, stringAssertSymbol), OperationKind.Invocation);
            }
        });
    }

    private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol? assertSymbol, INamedTypeSymbol? collectionAssertSymbol, INamedTypeSymbol? stringAssertSymbol)
    {
        var invocationOperation = (IInvocationOperation)context.Operation;

        // Check if this is a call to Assert, CollectionAssert, or StringAssert
        if (!IsTargetAssertType(invocationOperation.TargetMethod.ContainingType, assertSymbol, collectionAssertSymbol, stringAssertSymbol))
        {
            return;
        }

        // Check if this method call has the format string + params pattern
        if (HasFormatStringParamsPattern(invocationOperation))
        {
            context.ReportDiagnostic(invocationOperation.CreateDiagnostic(Rule, invocationOperation.TargetMethod.Name));
        }
    }

    private static bool IsTargetAssertType(INamedTypeSymbol? containingType, INamedTypeSymbol? assertSymbol, INamedTypeSymbol? collectionAssertSymbol, INamedTypeSymbol? stringAssertSymbol)
        => SymbolEqualityComparer.Default.Equals(containingType, assertSymbol) ||
            SymbolEqualityComparer.Default.Equals(containingType, collectionAssertSymbol) ||
            SymbolEqualityComparer.Default.Equals(containingType, stringAssertSymbol);

    private static bool HasFormatStringParamsPattern(IInvocationOperation invocationOperation)
    {
        ImmutableArray<IParameterSymbol> parameters = invocationOperation.TargetMethod.Parameters;

        // Look for the pattern: ([other params...], string message, params object[] parameters)
        // The last two parameters should be string message and params object[]
        if (parameters.Length < 2)
        {
            return false;
        }

        IParameterSymbol lastParam = parameters[parameters.Length - 1];
        IParameterSymbol secondLastParam = parameters[parameters.Length - 2];

        // Check if last parameter is params object[]
        bool hasParamsArray = lastParam.IsParams &&
                             lastParam.Type is IArrayTypeSymbol arrayType &&
                             arrayType.ElementType.SpecialType == SpecialType.System_Object;

        // Check if second-to-last parameter is string with StringSyntax attribute
        bool hasFormatString = secondLastParam.Type?.SpecialType == SpecialType.System_String &&
                              HasStringFormatSyntaxAttribute(secondLastParam);

        return hasParamsArray && hasFormatString;
    }

    private static bool HasStringFormatSyntaxAttribute(IParameterSymbol parameter)
    {
        foreach (AttributeData attribute in parameter.GetAttributes())
        {
            if (attribute.AttributeClass?.Name == "StringSyntaxAttribute" &&
                attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value?.ToString() == "CompositeFormat")
            {
                return true;
            }
        }

        return false;
    }
}
