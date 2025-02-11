// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Framework.SourceGeneration;

internal static class DiagnosticExtensions
{
    public static Diagnostic CreateDiagnostic(this IEnumerable<Location> locations, DiagnosticDescriptor rule,
        params object[] args)
        => locations.CreateDiagnostic(rule, null, args);

    public static Diagnostic CreateDiagnostic(this IEnumerable<Location> locations, DiagnosticDescriptor rule,
        ImmutableDictionary<string, string?>? properties, params object[] args)
    {
        var inSource = locations.Where(l => l.IsInSource).ToImmutableArray();
        return !inSource.Any()
            ? Diagnostic.Create(rule, null, args)
            : Diagnostic.Create(
                rule,
                location: inSource.First(),
                additionalLocations: inSource.Skip(1),
                properties: properties,
                messageArgs: args);
    }

    public static Diagnostic CreateDiagnostic(this ISymbol symbol, DiagnosticDescriptor rule, params object[] args)
        => symbol.Locations.CreateDiagnostic(rule, args);
}
