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
/// MSTEST0045: Use 'Assert' instead of 'StringAssert'.
/// </summary>
/// <remarks>
/// The analyzer captures StringAssert method calls and suggests using equivalent Assert methods:
/// <list type="bullet">
/// <item>
/// <description>
/// <code>StringAssert.Contains(value, substring)</code> → <code>Assert.Contains(substring, value)</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>StringAssert.StartsWith(value, substring)</code> → <code>Assert.StartsWith(substring, value)</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>StringAssert.EndsWith(value, substring)</code> → <code>Assert.EndsWith(substring, value)</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>StringAssert.Matches(value, pattern)</code> → <code>Assert.Matches(pattern, value)</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>StringAssert.DoesNotMatch(value, pattern)</code> → <code>Assert.DoesNotMatch(pattern, value)</code>
/// </description>
/// </item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
internal sealed class StringAssertToAssertAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Key to retrieve the proper assert method name from the properties bag.
    /// </summary>
    internal const string ProperAssertMethodNameKey = nameof(ProperAssertMethodNameKey);

    private static readonly LocalizableResourceString Title = new(nameof(Resources.StringAssertToAssertTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.StringAssertToAssertMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.StringAssertToAssertRuleId,
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
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingStringAssert, out INamedTypeSymbol? stringAssertTypeSymbol))
            {
                return;
            }

            context.RegisterOperationAction(context => AnalyzeInvocationOperation(context, stringAssertTypeSymbol), OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocationOperation(OperationAnalysisContext context, INamedTypeSymbol stringAssertTypeSymbol)
    {
        var operation = (IInvocationOperation)context.Operation;
        IMethodSymbol targetMethod = operation.TargetMethod;

        if (!SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, stringAssertTypeSymbol))
        {
            return;
        }

        // Map StringAssert methods to their equivalent Assert methods
        string? assertMethodName = targetMethod.Name switch
        {
            "Contains" => "Contains",
            "StartsWith" => "StartsWith",
            "EndsWith" => "EndsWith",
            "Matches" => "MatchesRegex",
            "DoesNotMatch" => "DoesNotMatchRegex",
            _ => null,
        };

        if (assertMethodName == null)
        {
            return;
        }

        // StringAssert methods all have at least 2 arguments that need to be swapped
        if (operation.Arguments.Length < 2)
        {
            return;
        }

        ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty.Add(ProperAssertMethodNameKey, assertMethodName);

        context.ReportDiagnostic(context.Operation.CreateDiagnostic(
            Rule,
            properties: properties,
            assertMethodName,
            targetMethod.Name));
    }
}
