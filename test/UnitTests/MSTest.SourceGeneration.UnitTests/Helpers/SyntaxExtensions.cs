// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace Microsoft.Testing.Framework.SourceGeneration.UnitTests.Helpers;

internal static class SyntaxExtensions
{
    public static string ShowWhitespace(this SourceText text) => text.ToString().ShowWhitespace();

    /// <summary>
    /// Show spaces and tabs as '·' and '→', replace "\r", but keep newlines, so the resulting text
    /// is still formatted it would be in a file but the lines are not OS specific and the whitespace is easy to see.
    /// </summary>
    public static string ShowWhitespace(this string text)
    {
        if (text.Contains('·') || text.Contains('→'))
        {
            throw new ArgumentException("Provided text contains '·' or '→' characters, " +
                $"which {nameof(ShowWhitespace)} uses to show whitespace. " +
                "Did you copy paste it from the test result and forgot to remove those replacements?");
        }

        IDictionary<string, string> map = new Dictionary<string, string>
        {
            { "\r", string.Empty },

            // Tabs output just 1 '→' on purpose, to break the code layout and be easier to spot.
            // We don't want tabs in our code.
            { "\t", "→" },
            { " ", "·" },
        };

        var regex = new Regex(string.Join('|', map.Keys));
        return regex.Replace(text, m => map[m.Value]);
    }

    /// <summary>
    /// Remove every doubled space, which gives you text that still spans multiple lines
    /// but the amount of whitespace is greatly reduced. This helps when you are not sure if your content
    /// is incorrect or it is just whitespace that is incorrect.
    /// </summary>
    public static string ShowReducedWhitespace(this SourceText text) => ShowReducedWhitespace(text.ToString());

    /// <summary>
    /// Remove every doubled space, which gives you text that still spans multiple lines
    /// but the amount of whitespace is greatly reduced. This helps when you are not sure if your content
    /// is incorrect or it is just whitespace that is incorrect.
    /// </summary>
    public static string ShowReducedWhitespace(this string text) => Regex.Replace(text, " {2,}", string.Empty).ShowWhitespace();
}
