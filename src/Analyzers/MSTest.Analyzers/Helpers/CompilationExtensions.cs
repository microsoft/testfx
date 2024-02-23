// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers.Helpers;

internal static class CompilationExtensions
{
    private static readonly BoundedCacheWithFactory<Compilation, bool> CanDiscoverInternalsCache = new();

    internal static bool CanDiscoverInternals(this Compilation compilation)
    {
        return CanDiscoverInternalsCache.GetOrCreateValue(compilation, GetCanDiscoverInternals);

        // Local functions
        static bool GetCanDiscoverInternals(Compilation compilation)
            => compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDiscoverInternalsAttribute, out var discoverInternalsAttributeSymbol)
            && compilation.Assembly.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, discoverInternalsAttributeSymbol));
    }
}
