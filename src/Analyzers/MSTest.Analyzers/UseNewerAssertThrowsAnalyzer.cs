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
/// MSTEST0039: Use newer 'Assert.Throws' methods.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
internal sealed class UseNewerAssertThrowsAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.UseNewerAssertThrowsTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.UseNewerAssertThrowsMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseNewerAssertThrowsRuleId,
        Title,
        MessageFormat,
        null,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert, out INamedTypeSymbol? assertTypeSymbol))
            {
                return;
            }

            INamedTypeSymbol? funcType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemFunc1);
            context.RegisterOperationAction(context => AnalyzeInvocationOperation(context, assertTypeSymbol, funcType), OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocationOperation(OperationAnalysisContext context, INamedTypeSymbol assertTypeSymbol, INamedTypeSymbol? funcType)
    {
        var operation = (IInvocationOperation)context.Operation;
        IMethodSymbol targetMethod = operation.TargetMethod;
        if (!SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, assertTypeSymbol) ||
            targetMethod.Name is not ("ThrowsException" or "ThrowsExceptionAsync"))
        {
            return;
        }

        ImmutableArray<Location> additionalLocations = ImmutableArray<Location>.Empty;

        // The old synchronous ThrowsException method has an overload that takes a Func<object> as the action.
        // The new synchronous ThrowsExactly method does not have this overload, and only Action overload is available.
        // Hence, the codefix should be aware of that to adjust accordingly.
        // For example, 'Assert.ThrowsException(() => 5)' should be fixed to Assert.ThrowsExactly(() => _ = 5).
        // Also, Assert.ThrowsException usage could be a long body with some return statements, which would be invalid for ThrowsExactly as there is only Action overload.
        // The codefix should adjust any "return whatever;" statements to "_ = whatever;" followed by "return;"
        // The codefix will know that it needs to adjust something if there is an additional location, which will be pointing to the action argument.
        if (!targetMethod.Name.EndsWith("Async", StringComparison.Ordinal) &&
            targetMethod.Parameters[0].Type.OriginalDefinition.Equals(funcType, SymbolEqualityComparer.Default) &&
            operation.Arguments.FirstOrDefault(arg => arg.Parameter?.Ordinal == 0)?.Syntax.GetLocation() is { } additionalLocation)
        {
            additionalLocations = ImmutableArray.Create(additionalLocation);
        }

        context.ReportDiagnostic(operation.CreateDiagnostic(Rule, additionalLocations, properties: null));
    }
}
