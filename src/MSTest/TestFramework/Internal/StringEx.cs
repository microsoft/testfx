// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

internal static class StringEx
{
    /// <inheritdoc cref="string.IsNullOrEmpty(string)"/>
    [SuppressMessage("ApiDesign", "RS0030:Do not used banned APIs", Justification = "Replacement API to allow nullable hints for compiler")]
    public static bool IsNullOrEmpty([NotNullWhen(returnValue: false)] string? value)
        => string.IsNullOrEmpty(value);

    /// <inheritdoc cref="string.IsNullOrWhiteSpace(string)"/>
    [SuppressMessage("ApiDesign", "RS0030:Do not used banned APIs", Justification = "Replacement API to allow nullable hints for compiler")]
    public static bool IsNullOrWhiteSpace([NotNullWhen(returnValue: false)] string? value)
        => string.IsNullOrWhiteSpace(value);
}
