﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.MSBuild;

internal static class NamespaceHelpers
{
    internal static string ToSafeNamespace(string value)
    {
        const char invalidCharacterReplacement = '_';

        value = value.Trim();

        StringBuilder safeValueStr = new(value.Length);

        for (int i = 0; i < value.Length; i++)
        {
            if (i < value.Length - 1 && char.IsSurrogatePair(value[i], value[i + 1]))
            {
                safeValueStr.Append(invalidCharacterReplacement);
                // Skip both chars that make up this symbol.
                i++;
                continue;
            }

            bool isFirstCharacterOfIdentifier = safeValueStr.Length == 0 || safeValueStr[safeValueStr.Length - 1] == '.';
            bool isValidFirstCharacter = UnicodeCharacterUtilities.IsIdentifierStartCharacter(value[i]);
            bool isValidPartCharacter = UnicodeCharacterUtilities.IsIdentifierPartCharacter(value[i]);

            if (isFirstCharacterOfIdentifier && !isValidFirstCharacter && isValidPartCharacter)
            {
                // This character cannot be at the beginning, but is good otherwise. Prefix it with something valid.
                safeValueStr.Append(invalidCharacterReplacement);
                safeValueStr.Append(value[i]);
            }
            else if ((isFirstCharacterOfIdentifier && isValidFirstCharacter) ||
                    (!isFirstCharacterOfIdentifier && isValidPartCharacter) ||
                    (safeValueStr.Length > 0 && i < value.Length - 1 && value[i] == '.'))
            {
                // This character is allowed to be where it is.
                safeValueStr.Append(value[i]);
            }
            else
            {
                safeValueStr.Append(invalidCharacterReplacement);
            }
        }

        return safeValueStr.ToString();
    }
}
