﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Analyzer.Utilities;

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers.Helpers;

internal static class CompilationExtensions
{
    /// <summary>
    /// Gets a type by its full type name and cache it at the compilation level.
    /// </summary>
    /// <param name="compilation">The compilation.</param>
    /// <param name="fullTypeName">Namespace + type name, e.g. "System.Exception".</param>
    /// <returns>The <see cref="INamedTypeSymbol"/> if found, null otherwise.</returns>
    internal static INamedTypeSymbol? GetOrCreateTypeByMetadataName(this Compilation compilation, string fullTypeName)
        => WellKnownTypeProvider.GetOrCreate(compilation).GetOrCreateTypeByMetadataName(fullTypeName);

    /// <summary>
    /// Gets a type by its full type name and cache it at the compilation level.
    /// </summary>
    /// <param name="compilation">The compilation.</param>
    /// <param name="fullTypeName">Namespace + type name, e.g. "System.Exception".</param>
    /// <returns>The <see cref="INamedTypeSymbol"/> if found, null otherwise.</returns>
    internal static bool TryGetOrCreateTypeByMetadataName(this Compilation compilation, string fullTypeName, [NotNullWhen(returnValue: true)] out INamedTypeSymbol? namedTypeSymbol)
        => WellKnownTypeProvider.GetOrCreate(compilation).TryGetOrCreateTypeByMetadataName(fullTypeName, out namedTypeSymbol);
}
