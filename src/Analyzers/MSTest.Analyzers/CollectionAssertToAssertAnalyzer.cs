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
/// MSTEST0068: Use 'Assert' instead of 'CollectionAssert'.
/// </summary>
/// <remarks>
/// The analyzer captures <c>CollectionAssert</c> method calls and suggests using equivalent
/// <c>Assert</c> methods. The mapping is:
/// <list type="bullet">
/// <item><description><c>CollectionAssert.AreEqual(a, b)</c> → <c>Assert.AreSequenceEqual(a, b)</c></description></item>
/// <item><description><c>CollectionAssert.AreNotEqual(a, b)</c> → <c>Assert.AreNotSequenceEqual(a, b)</c></description></item>
/// <item><description><c>CollectionAssert.AreEquivalent(a, b)</c> → <c>Assert.AreSequenceEqual(a, b, SequenceOrder.InAnyOrder)</c></description></item>
/// <item><description><c>CollectionAssert.AreNotEquivalent(a, b)</c> → <c>Assert.AreNotSequenceEqual(a, b, SequenceOrder.InAnyOrder)</c></description></item>
/// <item><description><c>CollectionAssert.AllItemsAreNotNull(a)</c> → <c>Assert.AreAllNotNull(a)</c></description></item>
/// <item><description><c>CollectionAssert.AllItemsAreUnique(a)</c> → <c>Assert.AreAllDistinct(a)</c></description></item>
/// <item><description><c>CollectionAssert.AllItemsAreInstancesOfType(coll, typeof(T))</c> → <c>Assert.AreAllOfType&lt;T&gt;(coll)</c> (uses the generic <c>Assert</c> overload when the type expression is a <c>typeof</c>; otherwise falls back to <c>Assert.AreAllOfType(t, coll)</c> with an argument-order swap)</description></item>
/// <item><description><c>CollectionAssert.Contains(coll, x)</c> → <c>Assert.Contains(x, coll)</c> (argument order swap)</description></item>
/// <item><description><c>CollectionAssert.DoesNotContain(coll, x)</c> → <c>Assert.DoesNotContain(x, coll)</c> (argument order swap)</description></item>
/// </list>
/// Overloads of <c>AreEqual</c>/<c>AreNotEqual</c> that take an <c>IComparer</c> are skipped because
/// <c>Assert.AreSequenceEqual</c> expects an <c>IEqualityComparer&lt;T&gt;</c> (different semantics).
/// Overloads of <c>AreEquivalent</c>/<c>AreNotEquivalent</c> that take an <c>IEqualityComparer&lt;T&gt;</c>
/// are also skipped (out of scope for this analyzer). <c>IsSubsetOf</c>/<c>IsNotSubsetOf</c> have no
/// direct <c>Assert</c> equivalent today and are not handled.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class CollectionAssertToAssertAnalyzer : AssertToAssertAnalyzerBase
{
    /// <summary>
    /// Key used by the code-fix to recover the rewrite strategy from the diagnostic properties.
    /// Values are <see cref="FixKindSimple"/>, <see cref="FixKindSwapTwoArgs"/>, <see cref="FixKindAddInAnyOrder"/>, or <see cref="FixKindInstanceOfType"/>.
    /// </summary>
    internal const string FixKindKey = nameof(FixKindKey);

    internal const string FixKindSimple = "Simple";
    internal const string FixKindSwapTwoArgs = "SwapTwoArgs";
    internal const string FixKindAddInAnyOrder = "AddInAnyOrder";
    internal const string FixKindInstanceOfType = "InstanceOfType";

    private static readonly LocalizableResourceString Title = new(nameof(Resources.CollectionAssertToAssertTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.CollectionAssertToAssertMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.CollectionAssertToAssertRuleId,
        Title,
        MessageFormat,
        null,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc />
    protected override DiagnosticDescriptor DiagnosticRule => Rule;

    /// <inheritdoc />
    protected override string SourceAssertTypeMetadataName
        => WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingCollectionAssert;

    /// <inheritdoc />
    protected override void AnalyzeInvocationOperation(OperationAnalysisContext context, INamedTypeSymbol sourceAssertTypeSymbol)
    {
        if (!TryGetTargetMethod(context, sourceAssertTypeSymbol, out IInvocationOperation operation, out IMethodSymbol targetMethod))
        {
            return;
        }

        (string AssertMethodName, string FixKind)? mapping = targetMethod.Name switch
        {
            "AreEqual" when !HasIComparerParameter(targetMethod) => ("AreSequenceEqual", FixKindSimple),
            "AreNotEqual" when !HasIComparerParameter(targetMethod) => ("AreNotSequenceEqual", FixKindSimple),
            "AreEquivalent" when !targetMethod.IsGenericMethod && !HasIComparerParameter(targetMethod) => ("AreSequenceEqual", FixKindAddInAnyOrder),
            "AreNotEquivalent" when !targetMethod.IsGenericMethod && !HasIComparerParameter(targetMethod) => ("AreNotSequenceEqual", FixKindAddInAnyOrder),
            "AllItemsAreNotNull" => ("AreAllNotNull", FixKindSimple),
            "AllItemsAreUnique" => ("AreAllDistinct", FixKindSimple),
            "AllItemsAreInstancesOfType" => ("AreAllOfType", FixKindInstanceOfType),
            "Contains" => ("Contains", FixKindSwapTwoArgs),
            "DoesNotContain" => ("DoesNotContain", FixKindSwapTwoArgs),
            _ => null,
        };

        if (mapping is not { } map)
        {
            return;
        }

        // Sanity-check the operand count for the rewrite strategies that require it.
        if ((map.FixKind is FixKindSwapTwoArgs or FixKindAddInAnyOrder or FixKindInstanceOfType)
            && operation.Arguments.Length < 2)
        {
            return;
        }

        ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
            .Add(ProperAssertMethodNameKey, map.AssertMethodName)
            .Add(FixKindKey, map.FixKind);

        context.ReportDiagnostic(context.Operation.CreateDiagnostic(
            Rule,
            properties: properties,
            map.AssertMethodName,
            targetMethod.Name));
    }

    private static bool HasIComparerParameter(IMethodSymbol method)
    {
        foreach (IParameterSymbol parameter in method.Parameters)
        {
            ITypeSymbol type = parameter.Type;
            if (type is INamedTypeSymbol named
                && (named.Name == "IComparer" || named.Name == "IEqualityComparer")
                && named.ContainingNamespace?.ToDisplayString() is "System.Collections" or "System.Collections.Generic")
            {
                return true;
            }
        }

        return false;
    }
}
