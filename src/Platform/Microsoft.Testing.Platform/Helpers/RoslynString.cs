// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "This is the replacement helper")]
[ExcludeFromCodeCoverage]
internal static class RoslynString
{
    /// <inheritdoc cref="string.IsNullOrEmpty(string)"/>
    public static bool IsNullOrEmpty([NotNullWhen(returnValue: false)] string? value)
        => string.IsNullOrEmpty(value);

#if !NET20
    /// <inheritdoc cref="string.IsNullOrWhiteSpace(string)"/>
    public static bool IsNullOrWhiteSpace([NotNullWhen(returnValue: false)] string? value)
        => string.IsNullOrWhiteSpace(value);
#endif
}
