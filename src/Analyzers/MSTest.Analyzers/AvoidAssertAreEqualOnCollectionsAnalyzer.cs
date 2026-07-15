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

            // May be null on very old target frameworks; when it is, only the IEquatable&lt;self&gt; check becomes a
            // no-op — the object.Equals override detection still runs.
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

        // Opt out when the compared type declares its own equality (implements IEquatable&lt;itself&gt; or overrides
        // object.Equals). The author has then deliberately chosen a custom, non-reference equality that Assert.AreEqual
        // honors via EqualityComparer&lt;T&gt;.Default, so suggesting a sequence/structural comparison would second-guess
        // a deliberate decision (see issue #9971). Two things are important here:
        //   * The check must be based on the *selected generic type argument* T, not the argument's runtime collection
        //     type. Widening to a base type (e.g. Assert.AreEqual&lt;object&gt;(collection, collection)) discards the
        //     collection's IEquatable&lt;self&gt; and falls back to reference equality — exactly the footgun the rule targets.
        //   * It must not apply when the caller supplies an explicit comparer, because then EqualityComparer&lt;T&gt;.Default
        //     (and therefore the type's own equality) is not used at all.
        if (!HasComparerArgument(invocation) && DeclaresOwnEquality(comparedType, equatableSymbol))
        {
            return;
        }

        ITypeSymbol? reportedType = ShouldReport(comparedType, genericEnumerableSymbol)
            ? comparedType
            : GetCollectionArgumentType(invocation, firstParameterName, genericEnumerableSymbol)
                ?? GetCollectionArgumentType(invocation, "actual", genericEnumerableSymbol);

        if (reportedType is null)
        {
            return;
        }

        string methodName = $"Assert.{targetMethod.Name}";
        string comparedTypeDisplay = reportedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        context.ReportDiagnostic(invocation.CreateDiagnostic(Rule, methodName, comparedTypeDisplay));
    }

    private static bool HasComparerArgument(IInvocationOperation invocation)
    {
        foreach (IArgumentOperation argument in invocation.Arguments)
        {
            if (argument.Parameter?.Name == "comparer")
            {
                return true;
            }
        }

        return false;
    }

    private static ITypeSymbol? GetCollectionArgumentType(IInvocationOperation invocation, string parameterName, INamedTypeSymbol genericEnumerableSymbol)
    {
        IArgumentOperation? argument = invocation.Arguments.FirstOrDefault(arg => arg.Parameter?.Name == parameterName);

        // Use WalkDownBuiltInConversion so we only peel off built-in conversions (boxing, reference,
        // implicit numeric widening, etc.). A user-defined conversion can convert a collection-typed
        // operand to a non-collection type (or vice versa), so the call-site static type — the result
        // of the user-defined conversion — is what the user wrote and what we should reason about.
        ITypeSymbol? argumentType = argument?.Value.WalkDownBuiltInConversion().Type;
        return argumentType is not null && ShouldReport(argumentType, genericEnumerableSymbol)
            ? argumentType
            : null;
    }

    private static bool ShouldReport(ITypeSymbol comparedType, INamedTypeSymbol genericEnumerableSymbol)
        => comparedType.SpecialType != SpecialType.System_String
            && ImplementsGenericEnumerable(comparedType, genericEnumerableSymbol);

    private static bool DeclaresOwnEquality(ITypeSymbol type, INamedTypeSymbol? equatableSymbol)
    {
        switch (type)
        {
            case INamedTypeSymbol namedType:
                return ImplementsSelfEquatable(namedType, namedType, equatableSymbol) || OverridesObjectEquals(namedType);

            // EqualityComparer&lt;T&gt;.Default honors a `where T : IEquatable<T>` constraint, and a class constraint that
            // overrides object.Equals is inherited by every T. A bare `where T : IEnumerable<...>` constraint has
            // neither, so it stays the reference-equality footgun we still want to flag.
            //
            // The equality target stays `typeParameter` (T) while we traverse constraints. We must NOT recurse with the
            // constraint as the new target: for `where T : ISelf` with `ISelf : IEquatable<ISelf>`, a concrete T need
            // not implement IEquatable&lt;T&gt;, so EqualityComparer&lt;T&gt;.Default would still use reference equality.
            case ITypeParameterSymbol typeParameter:
                foreach (ITypeSymbol constraintType in typeParameter.ConstraintTypes)
                {
                    // Only named constraints are inspected. A transitive type-parameter constraint
                    // (`where T : U where U : SomeSelfEquatingType`) is intentionally not followed: doing so risks
                    // over-suppressing, and over-reporting a warning is the safer error for this rule than silently
                    // missing the reference-equality footgun.
                    if (constraintType is INamedTypeSymbol namedConstraint &&
                        (ImplementsSelfEquatable(namedConstraint, typeParameter, equatableSymbol) || OverridesObjectEquals(namedConstraint)))
                    {
                        return true;
                    }
                }

                return false;

            default:
                return false;
        }
    }

    // Returns true when `candidate` is or implements IEquatable&lt;equalityType&gt;, i.e. the equality contract that
    // EqualityComparer&lt;equalityType&gt;.Default would dispatch to. `equalityType` is the type whose default comparer
    // is used (the compared type or the type parameter); `candidate` is the type (or constraint) we inspect.
    private static bool ImplementsSelfEquatable(INamedTypeSymbol candidate, ITypeSymbol equalityType, INamedTypeSymbol? equatableSymbol)
    {
        if (equatableSymbol is null)
        {
            return false;
        }

        // The candidate can be the IEquatable&lt;T&gt; interface itself (e.g. a `where T : IEquatable<T>` constraint)
        // or a type that lists it among its implemented interfaces (e.g. `class C : IEquatable<C>`).
        if (IsEquatableOf(candidate, equalityType, equatableSymbol))
        {
            return true;
        }

        foreach (INamedTypeSymbol implemented in candidate.AllInterfaces)
        {
            if (IsEquatableOf(implemented, equalityType, equatableSymbol))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEquatableOf(INamedTypeSymbol type, ITypeSymbol equalityType, INamedTypeSymbol equatableSymbol)
        => SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, equatableSymbol)
            && type.TypeArguments.Length == 1
            && SymbolEqualityComparer.Default.Equals(type.TypeArguments[0], equalityType);

    private static bool OverridesObjectEquals(INamedTypeSymbol type)
    {
        // Stop before object AND ValueType: System.ValueType overrides object.Equals, so walking into it would treat
        // every struct that implements IEnumerable<T> as declaring its own equality even when it has no Equals of its
        // own (its backing array/list field would then be compared by reference). An explicit struct override is found
        // on the concrete type before we reach ValueType.
        for (INamedTypeSymbol? current = type;
            current is not null && current.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType or SpecialType.System_Enum);
            current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers(nameof(Equals)))
            {
                if (member is IMethodSymbol { IsOverride: true, Parameters.Length: 1, ReturnType.SpecialType: SpecialType.System_Boolean } method &&
                    method.Parameters[0].Type.SpecialType == SpecialType.System_Object &&
                    OverrideRootIsObjectEquals(method))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // `IsOverride` only proves the method overrides *some* virtual slot. A base class can declare
    // `new virtual bool Equals(object)`, and a derived override of that member is not an override of
    // object.Equals — EqualityComparer&lt;T&gt;.Default still dispatches the unchanged object.Equals slot
    // (reference equality). Follow the override chain to its root and require it to be object.Equals.
    private static bool OverrideRootIsObjectEquals(IMethodSymbol method)
    {
        IMethodSymbol root = method;
        while (root.OverriddenMethod is { } overridden)
        {
            root = overridden;
        }

        return root.ContainingType?.SpecialType == SpecialType.System_Object;
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
