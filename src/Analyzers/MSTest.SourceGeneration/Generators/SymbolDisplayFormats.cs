// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Generators;

/// <summary>
/// Shared <see cref="SymbolDisplayFormat"/> instances used when the source generator emits
/// fully-qualified type and member names into the generated registration code.
/// </summary>
internal static class SymbolDisplayFormats
{
    /// <summary>
    /// Fully-qualified (global::-prefixed) format that keeps C# special type keywords (e.g. <c>int</c>,
    /// <c>string</c>) so emitted names both compile and compare against the framework's canonical names.
    /// </summary>
    public static readonly SymbolDisplayFormat FullyQualified =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes);
}
