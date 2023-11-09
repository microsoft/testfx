// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.Helpers;

[ExcludeFromCodeCoverage]
internal static class FNV_1aHashHelper
{
    /// <summary>
    /// Computes a hash of the string using the FNV-1a algorithm.
    /// Used by Roslyn.
    /// </summary>
    public static uint ComputeStringHash(string s)
    {
        uint num = default;
        if (s != null)
        {
            num = 2166136261u;
            int num2 = 0;
            while (num2 < s.Length)
            {
                num = (s[num2] ^ num) * 16777619;
                num2++;
            }
        }

        return num;
    }
}
