// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Linq;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0056: <inheritdoc cref="Resources.EmptyTestMethodTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class EmptyTestMethodAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.EmptyTestMethodTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.EmptyTestMethodDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.EmptyTestMethodMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor EmptyTestMethodRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.EmptyTestMethodRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Design,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(EmptyTestMethodRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol))
            {
                context.RegisterOperationAction(
                    context => AnalyzeMethodBody(context, testMethodAttributeSymbol),
                    OperationKind.MethodBody);
            }
        });
    }

    private static void AnalyzeMethodBody(OperationAnalysisContext context, INamedTypeSymbol testMethodAttributeSymbol)
    {
        var methodBodyOperation = (IMethodBodyOperation)context.Operation;
        
        // Get the method symbol from the semantic model
        IMethodSymbol? method = methodBodyOperation.SemanticModel.GetDeclaredSymbol(methodBodyOperation.Syntax, context.CancellationToken) as IMethodSymbol;

        if (method is null)
        {
            return;
        }

        // Check if this method has the TestMethod attribute (or derived attributes)
        bool isTestMethod = method.GetAttributes().Any(attr => attr.AttributeClass.Inherits(testMethodAttributeSymbol));
        if (!isTestMethod)
        {
            return;
        }

        // Check if the method body is empty or contains only trivial statements
        if (IsMethodBodyEmpty(methodBodyOperation.BlockBody))
        {
            context.ReportDiagnostic(method.CreateDiagnostic(EmptyTestMethodRule, method.Name));
        }
    }

    private static bool IsMethodBodyEmpty(IBlockOperation? blockOperation)
    {
        if (blockOperation is null)
        {
            return true;
        }

        // Count meaningful operations
        int meaningfulOperationCount = 0;
        foreach (IOperation operation in blockOperation.Operations)
        {
            if (IsMeaningfulOperation(operation))
            {
                meaningfulOperationCount++;
            }
        }

        return meaningfulOperationCount == 0;
    }

    private static bool IsMeaningfulOperation(IOperation operation)
    {
        return operation.Kind switch
        {
            // Skip implicit return statements and labeled operations (like in the existing analyzer)
            OperationKind.Return when operation.IsImplicit => false,
            OperationKind.Labeled when operation.IsImplicit => false,
            
            // Skip labeled statements that are just wrappers (check the wrapped operation)
            OperationKind.Labeled => IsMeaningfulOperation(((ILabeledOperation)operation).Operation),
            
            // Skip empty statements
            OperationKind.Empty => false,
            
            // All other operations are considered meaningful
            _ => true
        };
    }
}