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
    private static readonly LocalizableResourceString Description = new(nameof(Resources.AvoidAssertAreSameWithValueTypesDescription), Resources.ResourceManager, typeof(Resources));

    internal const string ReplacementKey = nameof(ReplacementKey);

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AvoidAssertAreSameWithValueTypesRuleId,
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
        if ((targetMethod.Name != "AreSame" && targetMethod.Name != "AreNotSame") ||
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

        ITypeSymbol? expectedType = argExpected.Value.WalkDownConversion().Type;
        ITypeSymbol? actualType = argActual.Value.WalkDownConversion().Type;

        if (expectedType?.IsValueType == true ||
            actualType?.IsValueType == true)
        {
            string suggestedReplacement = targetMethod.Name == "AreSame" ? "AreEqual" : "AreNotEqual";
            ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
                .Add(ReplacementKey, suggestedReplacement);
            context.ReportDiagnostic(operation.CreateDiagnostic(Rule, properties, targetMethod.Name, suggestedReplacement));
        }
    }
}
