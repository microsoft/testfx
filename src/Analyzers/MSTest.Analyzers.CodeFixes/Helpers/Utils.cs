// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers.Helpers;

internal class Utils
{
    public static bool IsDiscoverInternalsAttributePresent(SemanticModel semanticModel)
    {
        Compilation compilation = semanticModel.Compilation;
        IAssemblySymbol assemblySymbol = compilation.Assembly;
        var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(semanticModel.Compilation);
        INamedTypeSymbol? discoverInternalsAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDiscoverInternalsAttribute);
        foreach (AttributeData attribute in assemblySymbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, discoverInternalsAttribute))
            {
                return true;
            }
        }

        return false;
    }
}
