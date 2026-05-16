// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;

internal partial class TestDataConnectionSql
{
    /// <summary>
    /// Take a possibly qualified name with at least minimal quoting
    /// and return a fully quoted string
    /// Take care to only convert names that are of a recognized form.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <returns>A fully quoted string.</returns>
    public string PrepareNameForSql(string tableName)
    {
        string[]? parts = SplitName(tableName);

        if (parts is { Length: > 0 })
        {
            // Seems to be well formed, so make sure we end up fully quoted
            return JoinAndQuoteName(parts, true);
        }
        else
        {
            // Just use what they gave us, literally, since we do not really understand the format
            return tableName;
        }
    }

    /// <summary>
    /// Take a possibly qualified name and break it down into an
    /// array of identifiers unquoting any quoted names.
    /// </summary>
    /// <param name="name">A string.</param>
    /// <returns>An array of unquoted parts, or null if the name fails to conform.</returns>
    public string[]? SplitName(string name)
    {
        List<string> parts = [];

        int here = 0;
        int end = name.Length;
        char firstDelimiter = ' '; // initialize since code analysis is not smart enough
        char currentDelimiter;
        char catalogSeparatorChar = CatalogSeparatorChar;
        char schemaSeparatorChar = SchemaSeparatorChar;

        while (here < end)
        {
            int next = FindIdentifierEnd(name, here);
            string identifier = name.Substring(here, next - here);

            if (StringEx.IsNullOrEmpty(identifier))
            {
                // Not well formed, split failed
                return null;
            }

            if (identifier.StartsWith(QuotePrefix, StringComparison.Ordinal))
            {
                identifier = UnquoteIdentifier(identifier);
            }

            parts.Add(identifier);

            if (next < end)
            {
                currentDelimiter = name[next];
                switch (parts.Count)
                {
                    case 1:
                        // We infer there will be at least 2 parts
                        firstDelimiter = currentDelimiter;
                        if (firstDelimiter != catalogSeparatorChar
                            && firstDelimiter != schemaSeparatorChar)
                        {
                            // Not well formed, split failed
                            return null;
                        }

                        break;

                    case 2:
                        // We infer there will be at least 3 parts
                        if (firstDelimiter != catalogSeparatorChar
                            || currentDelimiter != schemaSeparatorChar)
                        {
                            // Not well formed, split failed
                            return null;
                        }

                        break;

                    default:
                        // We infer there will be at least 4 or more parts
                        // so not well formed, split failed
                        return null;
                }

                // Skip delimiter
                here = next + 1;
            }
            else
            {
                // We have found the end
                if (parts.Count == 2 && firstDelimiter != schemaSeparatorChar)
                {
                    // Not well formed, split failed
                    return null;
                }

                return [.. parts];
            }
        }

        // Ended in a delimiter, or no parts at all, either is invalid
        return null;
    }

    /// <summary>
    /// Take a list of unquoted name parts and join them into a
    /// qualified name. Either minimally quote (to the extent required
    /// to reliably split the name again) or fully quote, therefore made suitable
    /// for a database query.
    /// </summary>
    /// <param name="parts">Name parts.</param>
    /// <param name="fullyQuote">Should full quote.</param>
    /// <returns>A qualified name.</returns>
    public string JoinAndQuoteName(string[] parts, bool fullyQuote)
    {
        int partCount = parts.Length;
        StringBuilder result = new();

        DebugEx.Assert(partCount is > 0 and < 4, "partCount should be 1,2 or 3.");

        int currentPart = 0;
        if (partCount > 2)
        {
            result.Append(MaybeQuote(parts[currentPart++], fullyQuote));
            result.Append(CommandBuilder.CatalogSeparator);
        }

        if (partCount > 1)
        {
            result.Append(MaybeQuote(parts[currentPart++], fullyQuote));
            result.Append(CommandBuilder.SchemaSeparator);
        }

        result.Append(MaybeQuote(parts[currentPart], fullyQuote));
        return result.ToString();
    }

    private string MaybeQuote(string identifier, bool force) => force || FindSeparators(identifier, 0) != -1 ? QuoteIdentifier(identifier) : identifier;

    /// <summary>
    /// Find the first separator in a string.
    /// </summary>
    /// <param name="text">The string.</param>
    /// <param name="from">Index.</param>
    /// <returns>Location of the separator.</returns>
    private int FindSeparators(string text, int from) => text.IndexOfAny([SchemaSeparatorChar, CatalogSeparatorChar], from);

    /// <summary>
    /// Given a string and a position in that string, assumed
    /// to be the start of an identifier, find the end of that
    /// identifier. Take into account quoting rules.
    /// </summary>
    /// <param name="text">The string.</param>
    /// <param name="start">start index.</param>
    /// <returns>Position in string after end of identifier (may be off end of string).</returns>
    private int FindIdentifierEnd(string text, int start)
    {
        // These routine assumes prefixes and suffixes
        // are single characters
        string prefix = QuotePrefix;
        DebugEx.Assert(prefix.Length == 1, "prefix length should be 1.");
        char prefixChar = prefix[0];

        int end = text.Length;
        if (text[start] == prefixChar)
        {
            // Identifier is quoted. Repeatedly look for
            // suffix character, until not found,
            // the character after is end of string,
            // or not another suffix character

            // Skip opening quote
            int here = start + 1;

            string suffix = QuoteSuffix;
            DebugEx.Assert(suffix.Length == 1, "suffix length should be 1.");
            char suffixChar = suffix[0];

            while (here < end)
            {
                here = text.IndexOf(suffixChar, here);
                if (here == -1)
                {
                    // If this happens the string is malformed, since we had an
                    // opening quote without a closing one, but we can survive this
                    break;
                }

                // Skip the quote we just found
                here++;

                // If this the end?
                if (here == end || text[here] != suffixChar)
                {
                    // Well formed end of identifier
                    return here;
                }

                // We have a double quote, skip the second one, then keep looking
                here++;
            }

            // If we fall off end of loop,
            // we didn't find the matching close quote
            // Best thing to do is to just return the whole string
            return end;
        }
        else
        {
            // In the case of an unquoted strings, the processing is much
            // simpler... the end is end of string, or the first
            // of several possible separators.
            int separatorPosition = FindSeparators(text, start);
            return separatorPosition == -1 ? end : separatorPosition;
        }
    }
}

#endif
