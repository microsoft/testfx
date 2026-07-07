// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Diagnostics;

/// <summary>
/// Value-equatable surrogate for <see cref="Location" /> so a reported
/// <see cref="DiagnosticInfo" /> can flow through incremental-generator pipelines
/// without breaking the model-equality contract that gates step caching.
/// </summary>
internal sealed record LocationInfo(string FilePath, TextSpan SourceSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation()
        => Location.Create(FilePath, SourceSpan, LineSpan);

    public static LocationInfo? CreateFrom(SyntaxNode node)
        => CreateFrom(node.GetLocation());

    public static LocationInfo? CreateFrom(ISymbol symbol)
    {
        foreach (SyntaxReference reference in symbol.DeclaringSyntaxReferences)
        {
            // The first declaration is the canonical one; partial declarations get the first
            // physical occurrence which is good enough for diagnostic placement.
            return CreateFrom(reference.GetSyntax().GetLocation());
        }

        return null;
    }

    public static LocationInfo? CreateFrom(Location location)
        => location.SourceTree is null
            ? null
            : new LocationInfo(
                location.SourceTree.FilePath,
                location.SourceSpan,
                location.GetLineSpan().Span);
}
