// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Diagnostics;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Helpers;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Generators;

/// <summary>
/// Predicates and signature helpers used by <see cref="TestClassModelBuilder"/> to decide which members are
/// eligible for the emitted model, to report unsupported shapes, and to key members while walking the
/// inheritance chain.
/// </summary>
internal static class TestMemberValidationHelper
{
    // Restricted to accessibilities the emitted helper class (a separate static type
    // declared in MSTest.SourceGenerated, not a derived type) can legally call.
    // 'protected' and 'private protected' members require the caller to be a derived
    // type, so they are excluded. Internal access is available only for members declared
    // in the consuming assembly.
    internal static bool IsAccessibleFromConsumer(ISymbol symbol, IAssemblySymbol consumingAssembly)
        => SymbolReferenceabilityHelper.IsMemberAccessibleFrom(
            symbol.DeclaredAccessibility,
            symbol.ContainingAssembly,
            consumingAssembly);

    internal static bool IsTestMethodAttributePresent(ImmutableArray<AttributeData> attributes)
    {
        foreach (AttributeData attribute in attributes)
        {
            for (INamedTypeSymbol? attributeClass = attribute.AttributeClass;
                 attributeClass is not null;
                 attributeClass = attributeClass.BaseType)
            {
                if (attributeClass.ToDisplayString(SymbolDisplayFormats.FullyQualified) == "global::" + MSTestAttributeNames.TestMethod)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Reports AOTSG0004 (generic method) and AOTSG0005 (by-ref parameter) when applicable.
    // Returns true if the member must be excluded from the emitted model.
    internal static bool TryReportUnsupportedMethod(IMethodSymbol method, string owningClassFqn, List<DiagnosticInfo> diagnostics)
    {
        bool unsupported = false;

        // AOTSG0004 only applies to ordinary methods. Constructors cannot be generic so
        // method.IsGenericMethod is false for them.
        if (method.IsGenericMethod)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.GenericTestMethod,
                LocationInfo.CreateFrom(method),
                owningClassFqn,
                method.Name));
            unsupported = true;
        }

        foreach (IParameterSymbol parameter in method.Parameters.Where(static p => p.RefKind != RefKind.None))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.ByRefParameter,
                LocationInfo.CreateFrom(parameter),
                owningClassFqn,
                method.MethodKind == MethodKind.Constructor ? "ctor" : method.Name,
                parameter.Name));
            unsupported = true;
        }

        return unsupported;
    }

    internal static bool IsSupportedTestClassConstructor(IMethodSymbol constructor)
    {
        ImmutableArray<IParameterSymbol> parameters = constructor.Parameters;
        return parameters.Length == 0
            || (parameters.Length == 1
                && parameters[0].Type.ToDisplayString(SymbolDisplayFormats.FullyQualified) == "global::" + MSTestAttributeNames.UnitTestingNamespace + ".TestContext");
    }

    // Mirrors TypeEnumerator's MethodInfo.ToString()-based discovery identity. In particular,
    // generic parameter names remain significant because reflection formats them into that string.
    internal static bool HaveSameRuntimeDiscoverySignature(IMethodSymbol left, IMethodSymbol right)
    {
        if (!string.Equals(left.Name, right.Name, StringComparison.Ordinal)
            || left.Arity != right.Arity
            || left.Parameters.Length != right.Parameters.Length
            || (left.IsStatic && !right.IsStatic)
            || !AreSignatureTypesEquivalent(left.ReturnType, right.ReturnType))
        {
            return false;
        }

        for (int index = 0; index < left.Parameters.Length; index++)
        {
            IParameterSymbol leftParameter = left.Parameters[index];
            IParameterSymbol rightParameter = right.Parameters[index];
            if ((leftParameter.RefKind == RefKind.None) != (rightParameter.RefKind == RefKind.None)
                || !AreSignatureTypesEquivalent(leftParameter.Type, rightParameter.Type))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreSignatureTypesEquivalent(ITypeSymbol left, ITypeSymbol right)
    {
        if (left is IDynamicTypeSymbol)
        {
            return right is IDynamicTypeSymbol || right.SpecialType == SpecialType.System_Object;
        }

        if (right is IDynamicTypeSymbol)
        {
            return left.SpecialType == SpecialType.System_Object;
        }

        if (left is ITypeParameterSymbol leftTypeParameter && right is ITypeParameterSymbol rightTypeParameter)
        {
            return leftTypeParameter.TypeParameterKind == rightTypeParameter.TypeParameterKind
                && string.Equals(leftTypeParameter.Name, rightTypeParameter.Name, StringComparison.Ordinal);
        }

        if (left is IArrayTypeSymbol leftArray && right is IArrayTypeSymbol rightArray)
        {
            return leftArray.Rank == rightArray.Rank
                && AreSignatureTypesEquivalent(leftArray.ElementType, rightArray.ElementType);
        }

        if (left is INamedTypeSymbol leftNamed && right is INamedTypeSymbol rightNamed)
        {
            if (leftNamed.TypeArguments.Length != rightNamed.TypeArguments.Length
                || !SymbolEqualityComparer.Default.Equals(leftNamed.OriginalDefinition, rightNamed.OriginalDefinition)
                || (leftNamed.ContainingType is null) != (rightNamed.ContainingType is null)
                || (leftNamed.ContainingType is not null
                    && !AreSignatureTypesEquivalent(leftNamed.ContainingType, rightNamed.ContainingType!)))
            {
                return false;
            }

            for (int index = 0; index < leftNamed.TypeArguments.Length; index++)
            {
                if (!AreSignatureTypesEquivalent(leftNamed.TypeArguments[index], rightNamed.TypeArguments[index]))
                {
                    return false;
                }
            }

            return true;
        }

        return SymbolEqualityComparer.Default.Equals(left, right);
    }
}
