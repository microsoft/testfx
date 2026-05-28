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
/// MSTEST0065: <inheritdoc cref="Resources.AvoidAssertAreEqualOnCollectionsTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AvoidAssertAreEqualOnCollectionsAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.AvoidAssertAreEqualOnCollectionsTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.AvoidAssertAreEqualOnCollectionsMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.AvoidAssertAreEqualOnCollectionsDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AvoidAssertAreEqualOnCollectionsRuleId,
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
            INamedTypeSymbol? genericEnumerableSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIEnumerable1);
            if (assertSymbol is null || genericEnumerableSymbol is null)
            {
                return;
            }

            context.RegisterOperationAction(context => AnalyzeInvocation(context, assertSymbol, genericEnumerableSymbol), OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, INamedTypeSymbol assertSymbol, INamedTypeSymbol genericEnumerableSymbol)
    {
        var invocation = (IInvocationOperation)context.Operation;
        IMethodSymbol targetMethod = invocation.TargetMethod;
        if (!targetMethod.IsGenericMethod ||
            targetMethod.TypeArguments.Length != 1 ||
            targetMethod.Name is not ("AreEqual" or "AreNotEqual") ||
            !SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, assertSymbol))
        {
            return;
        }

        ITypeSymbol comparedType = targetMethod.TypeArguments[0];
        if (!ShouldReport(comparedType, genericEnumerableSymbol))
        {
            return;
        }

        // When either argument is the null literal, the user is performing a null check rather than
        // a collection equality check, so suggesting CollectionAssert.AreEqual / Assert.AreSequenceEqual
        // would be misleading.
        // When null is in the expected/notExpected position, MSTEST0037 (UseProperAssertMethods) already
        // triggers and proposes the correct Assert.IsNull / Assert.IsNotNull replacement.
        // When null is in the actual position, MSTEST0037 does not currently fire, but suppressing
        // MSTEST0065 is still correct because CollectionAssert guidance is not what the user wants.
        // The first parameter is "expected" on Assert.AreEqual and "notExpected" on Assert.AreNotEqual.
        string firstParameterName = targetMethod.Name == "AreEqual" ? "expected" : "notExpected";
        if (HasNullLiteralArgument(invocation, firstParameterName) || HasNullLiteralArgument(invocation, "actual"))
        {
            return;
        }

        string methodName = $"Assert.{targetMethod.Name}";
        string comparedTypeDisplay = comparedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(invocation.CreateDiagnostic(Rule, methodName, comparedTypeDisplay));
    }

    private static bool ShouldReport(ITypeSymbol comparedType, INamedTypeSymbol genericEnumerableSymbol)
        => comparedType.SpecialType != SpecialType.System_String
            && ImplementsGenericEnumerable(comparedType, genericEnumerableSymbol);

    private static bool HasNullLiteralArgument(IInvocationOperation invocation, string parameterName)
    {
        IArgumentOperation? argument = invocation.Arguments.FirstOrDefault(arg => arg.Parameter?.Name == parameterName);
        return argument?.Value.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } };
    }

    private static bool ImplementsGenericEnumerable(ITypeSymbol type, INamedTypeSymbol genericEnumerableSymbol)
    {
        if (type is INamedTypeSymbol namedType && SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, genericEnumerableSymbol))
        {
            return true;
        }

        if (type.AllInterfaces.Any(interfaceType => SymbolEqualityComparer.Default.Equals(interfaceType.OriginalDefinition, genericEnumerableSymbol)))
        {
            return true;
        }

        if (type is ITypeParameterSymbol typeParameter)
        {
            foreach (ITypeSymbol constraintType in typeParameter.ConstraintTypes)
            {
                if (ImplementsGenericEnumerable(constraintType, genericEnumerableSymbol))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
