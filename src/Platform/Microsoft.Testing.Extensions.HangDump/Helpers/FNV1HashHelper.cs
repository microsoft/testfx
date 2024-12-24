// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

[ExcludeFromCodeCoverage]
internal static class FNV_1aHashHelper
{
    /// <summary>
    /// Computes a hash of the string using the FNV-1a algorithm.
    /// Used by Roslyn.
    /// </summary>
    public static uint ComputeStringHash(string input) =>
        input.Aggregate(2166136261u, (current, ch) => (ch ^ current) * 16777619);
}
