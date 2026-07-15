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

            // May be null on very old target frameworks; the equality-opt-out check simply becomes a no-op then.
            INamedTypeSymbol? equatableSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIEquatable1);

            context.RegisterOperationAction(context => AnalyzeInvocation(context, assertSymbol, genericEnumerableSymbol, equatableSymbol), OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, INamedTypeSymbol assertSymbol, INamedTypeSymbol genericEnumerableSymbol, INamedTypeSymbol? equatableSymbol)
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

        // Determine the type to report on. Prefer the generic type argument when it itself is a collection
        // (the historical behavior, which gives the cleanest diagnostic display). Otherwise, fall back to the
        // un-converted static type of the `expected`/`actual` arguments at the call site so we still catch
        // patterns where the caller widened to a non-collection type (e.g. `Assert.AreEqual<object>(arr1, arr2)`
        // or `Assert.AreEqual((object)arr1, (object)arr2)`) which would otherwise silently use reference equality.
        ITypeSymbol comparedType = targetMethod.TypeArguments[0];
        ITypeSymbol? reportedType = ShouldReport(comparedType, genericEnumerableSymbol, equatableSymbol)
            ? comparedType
            : GetCollectionArgumentType(invocation, firstParameterName, genericEnumerableSymbol, equatableSymbol)
                ?? GetCollectionArgumentType(invocation, "actual", genericEnumerableSymbol, equatableSymbol);

        if (reportedType is null)
        {
            return;
        }

        string methodName = $"Assert.{targetMethod.Name}";
        string comparedTypeDisplay = reportedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(invocation.CreateDiagnostic(Rule, methodName, comparedTypeDisplay));
    }

    private static ITypeSymbol? GetCollectionArgumentType(IInvocationOperation invocation, string parameterName, INamedTypeSymbol genericEnumerableSymbol, INamedTypeSymbol? equatableSymbol)
    {
        IArgumentOperation? argument = invocation.Arguments.FirstOrDefault(arg => arg.Parameter?.Name == parameterName);

        // Use WalkDownBuiltInConversion so we only peel off built-in conversions (boxing, reference,
        // implicit numeric widening, etc.). A user-defined conversion can convert a collection-typed
        // operand to a non-collection type (or vice versa), so the call-site static type — the result
        // of the user-defined conversion — is what the user wrote and what we should reason about.
        ITypeSymbol? argumentType = argument?.Value.WalkDownBuiltInConversion().Type;
        return argumentType is not null && ShouldReport(argumentType, genericEnumerableSymbol, equatableSymbol)
            ? argumentType
            : null;
    }

    private static bool ShouldReport(ITypeSymbol comparedType, INamedTypeSymbol genericEnumerableSymbol, INamedTypeSymbol? equatableSymbol)
        => comparedType.SpecialType != SpecialType.System_String
            && ImplementsGenericEnumerable(comparedType, genericEnumerableSymbol)
            // When the compared type declares its own equality (implements IEquatable&lt;itself&gt; or overrides
            // object.Equals), the author has deliberately opted into a custom, non-reference equality. Assert.AreEqual
            // then honors that intent via EqualityComparer&lt;T&gt;.Default, so suggesting a sequence/structural comparison
            // would be second-guessing a deliberate decision (see issue #9971). We only warn on collection types that
            // fall back to reference equality — the actual footgun this rule exists to catch.
            && !DeclaresOwnEquality(comparedType, equatableSymbol);

    // Type parameters can't declare their own equality; their constraints drive ImplementsGenericEnumerable,
    // but a bare `where T : IEnumerable<...>` is still the reference-equality footgun we want to flag.
    private static bool DeclaresOwnEquality(ITypeSymbol type, INamedTypeSymbol? equatableSymbol)
        => type is INamedTypeSymbol namedType
            && ((equatableSymbol is not null &&
                    namedType.AllInterfaces.Any(i =>
                        SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, equatableSymbol) &&
                        i.TypeArguments.Length == 1 &&
                        SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], namedType)))
                || OverridesObjectEquals(namedType));

    private static bool OverridesObjectEquals(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers(nameof(Equals)))
            {
                if (member is IMethodSymbol { IsOverride: true, Parameters.Length: 1, ReturnType.SpecialType: SpecialType.System_Boolean } method &&
                    method.Parameters[0].Type.SpecialType == SpecialType.System_Object)
                {
                    return true;
                }
            }
        }

        return false;
    }

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
