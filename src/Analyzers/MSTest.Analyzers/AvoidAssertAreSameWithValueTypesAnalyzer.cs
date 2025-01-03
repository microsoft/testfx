// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;
using MSTest.Analyzers.RoslynAnalyzerHelpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0025: <inheritdoc cref="Resources.AvoidAssertAreSameWithValueTypesTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AvoidAssertAreSameWithValueTypesAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.AvoidAssertAreSameWithValueTypesTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.AvoidAssertAreSameWithValueTypesMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AvoidAssertAreSameWithValueTypesRuleId,
        Title,
        MessageFormat,
        null,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(context =>
        {
            Compilation compilation = context.Compilation;
            INamedTypeSymbol? assertSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert);
            if (assertSymbol is not null)
            {
                context.RegisterOperationAction(context => AnalyzeOperation(context, assertSymbol), OperationKind.Invocation);
            }
        });
    }

    private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol assertSymbol)
    {
        var operation = (IInvocationOperation)context.Operation;
        IMethodSymbol targetMethod = operation.TargetMethod;
        if (targetMethod.Name != "AreSame" ||
            !assertSymbol.Equals(operation.TargetMethod.ContainingType, SymbolEqualityComparer.Default))
        {
            return;
        }

        IArgumentOperation? argExpected = operation.Arguments.FirstOrDefault(arg => arg.Parameter?.Ordinal == 0);
        IArgumentOperation? argActual = operation.Arguments.FirstOrDefault(arg => arg.Parameter?.Ordinal == 1);
        if (argExpected is null || argActual is null)
        {
            return;
        }

        if (argExpected.Value.WalkDownConversion().Type?.IsValueType == true ||
            argActual.Value.WalkDownConversion().Type?.IsValueType == true)
        {
            context.ReportDiagnostic(operation.CreateDiagnostic(Rule));
        }
    }
}
