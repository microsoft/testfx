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
}
