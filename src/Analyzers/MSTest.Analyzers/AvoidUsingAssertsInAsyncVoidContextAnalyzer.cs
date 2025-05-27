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
/// MSTEST0040: <inheritdoc cref="Resources.AvoidUsingAssertsInAsyncVoidContextTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AvoidUsingAssertsInAsyncVoidContextAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.AvoidUsingAssertsInAsyncVoidContextTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.AvoidUsingAssertsInAsyncVoidContextMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.AvoidUsingAssertsInAsyncVoidContextDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AvoidUsingAssertsInAsyncVoidContextRuleId,
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
            INamedTypeSymbol? stringAssertSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingStringAssert);
            INamedTypeSymbol? collectionAssertSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingCollectionAssert);

            if (assertSymbol is not null || stringAssertSymbol is not null || collectionAssertSymbol is not null)
            {
                context.RegisterOperationAction(
                    context => AnalyzeOperation(
                        context, 
                        assertSymbol, 
                        stringAssertSymbol, 
                        collectionAssertSymbol), 
                    OperationKind.Invocation);
            }
        });
    }

    private static void AnalyzeOperation(
        OperationAnalysisContext context, 
        INamedTypeSymbol? assertSymbol, 
        INamedTypeSymbol? stringAssertSymbol, 
        INamedTypeSymbol? collectionAssertSymbol)
    {
        var operation = (IInvocationOperation)context.Operation;
        if (!IsAsyncVoidContext(operation, context.ContainingSymbol))
        {
            return;
        }

        var targetType = operation.TargetMethod.ContainingType;
        bool isAssertType = 
            (assertSymbol is not null && assertSymbol.Equals(targetType, SymbolEqualityComparer.Default)) ||
            (stringAssertSymbol is not null && stringAssertSymbol.Equals(targetType, SymbolEqualityComparer.Default)) ||
            (collectionAssertSymbol is not null && collectionAssertSymbol.Equals(targetType, SymbolEqualityComparer.Default));

        if (isAssertType)
        {
            context.ReportDiagnostic(operation.CreateDiagnostic(Rule));
        }
    }

    private static bool IsAsyncVoidContext(IInvocationOperation invocationOperation, ISymbol containingSymbol)
    {
        if (containingSymbol is IMethodSymbol { IsAsync: true, ReturnsVoid: true })
        {
            return true;
        }

        // For the case of anonymous functions or local functions, the ContainingSymbol is the method that contains the anonymous function.
        // So, we need to special case this.
        IOperation? operation = invocationOperation;
        while (operation is not null)
        {
            if (operation is IAnonymousFunctionOperation { Symbol.IsAsync: true, Symbol.ReturnsVoid: true } or
                ILocalFunctionOperation { Symbol.IsAsync: true, Symbol.ReturnsVoid: true })
            {
                return true;
            }

            operation = operation.Parent;
        }

        return false;
    }
}
