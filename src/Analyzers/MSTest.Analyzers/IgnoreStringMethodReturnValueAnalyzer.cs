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
/// MSTEST0055: <inheritdoc cref="Resources.IgnoreStringMethodReturnValueTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class IgnoreStringMethodReturnValueAnalyzer : DiagnosticAnalyzer
{
    private static readonly string[] StringMethodsToCheck = ["Contains", "StartsWith", "EndsWith"];

    private static readonly LocalizableResourceString Title = new(nameof(Resources.IgnoreStringMethodReturnValueTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.IgnoreStringMethodReturnValueMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.IgnoreStringMethodReturnValueDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.IgnoreStringMethodReturnValueRuleId,
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
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemString, out INamedTypeSymbol? stringSymbol))
            {
                context.RegisterOperationAction(context => AnalyzeExpressionStatement(context, stringSymbol), OperationKind.ExpressionStatement);
            }
        });
    }

    private static void AnalyzeExpressionStatement(OperationAnalysisContext context, INamedTypeSymbol stringSymbol)
    {
        var expressionStatementOperation = (IExpressionStatementOperation)context.Operation;
        
        if (expressionStatementOperation.Operation is not IInvocationOperation invocationOperation)
        {
            return;
        }

        // Check if this is a call to a string method we care about
        if (!IsStringMethodCall(invocationOperation, stringSymbol))
        {
            return;
        }

        string methodName = invocationOperation.TargetMethod.Name;
        context.ReportDiagnostic(invocationOperation.CreateDiagnostic(Rule, methodName));
    }

    private static bool IsStringMethodCall(IInvocationOperation invocationOperation, INamedTypeSymbol stringSymbol)
    {
        if (invocationOperation.TargetMethod.ContainingType is null 
            || !SymbolEqualityComparer.Default.Equals(invocationOperation.TargetMethod.ContainingType, stringSymbol))
        {
            return false;
        }

        string methodName = invocationOperation.TargetMethod.Name;
        return StringMethodsToCheck.Contains(methodName);
    }
}