// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;

internal static class ManagedNameParser
{
    /// <summary>
    /// Parses a given fully qualified managed method name into its name, arity and parameter types.
    /// </summary>
    /// <param name="managedMethodName">
    /// The fully qualified managed method name to parse.
    /// The format is defined in <see href="https://github.com/microsoft/vstest/blob/main/RFCs/0017-Managed-TestCase-Properties.md#managedmethod-property">the RFC</see>.
    /// </param>
    /// <param name="methodName">
    /// When this method returns, contains the parsed method name of the <paramref name="managedMethodName"/>.
    /// This parameter is passed uninitialized; any value originally supplied in result will be overwritten.
    /// </param>
    /// <param name="arity">
    /// When this method returns, contains the parsed arity of the <paramref name="managedMethodName"/>.
    /// This parameter is passed uninitialized; any value originally supplied in result will be overwritten.
    /// </param>
    /// <param name="parameterTypes">
    /// When this method returns, contains the parsed parameter types of the <paramref name="managedMethodName"/>.
    /// If there are no parameter types in <paramref name="managedMethodName"/>, <paramref name="parameterTypes"/> is set to <see langword="null"/>.
    /// This parameter is passed uninitialized; any value originally supplied in result will be overwritten.
    /// </param>
    /// <exception cref="InvalidManagedNameException">
    /// Thrown if <paramref name="managedMethodName"/> contains spaces, incomplete, or the arity isn't numeric.
    /// </exception>
    public static void ParseManagedMethodName(string managedMethodName, out string methodName, out int arity, out string[]? parameterTypes)
    {
        int pos = ParseMethodName(managedMethodName, 0, out string? escapedMethodName, out arity);
        methodName = ManagedNameHelper.ParseEscapedString(escapedMethodName);
        pos = ParseParameterTypeList(managedMethodName, pos, out parameterTypes);
        if (pos != managedMethodName.Length)
        {
            throw new InvalidManagedNameException();
        }
    }

    private static string Capture(string managedMethodName, int start, int end)
        => managedMethodName.Substring(start, end - start);

    private static int ParseMethodName(string managedMethodName, int start, out string methodName, out int arity)
    {
        int i = start;
        bool quoted = false;
        char? previousChar = null;
        // Consume all characters that are in single quotes as is. Because F# methods wrapped in `` can have any text, like ``method name``.
        // and will be emitted into CIL  as 'method name'.
        // Make sure you ignore \', because that is how F# will escape ' if it appears in the method name.
        for (; i < managedMethodName.Length; i++)
        {
            if ((i - 1) > 0)
            {
                previousChar = managedMethodName[i - 1];
            }

            char c = managedMethodName[i];
            if ((c == '\'' && previousChar != '\\') || quoted)
            {
                quoted = (c == '\'' && previousChar != '\\') ? !quoted : quoted;
                continue;
            }

            switch (c)
            {
                case var w when char.IsWhiteSpace(w):
                    throw new InvalidManagedNameException();

                case '`':
                    methodName = Capture(managedMethodName, start, i);
                    return ParseArity(managedMethodName, i, out arity);

                case '(':
                    methodName = Capture(managedMethodName, start, i);
                    arity = 0;
                    return i;
            }
        }

        methodName = Capture(managedMethodName, start, i);
        arity = 0;
        return i;
    }

    // parse arity in the form `nn where nn is an integer value.
    private static int ParseArity(string managedMethodName, int start, out int arity)
    {
        Debug.Assert(managedMethodName[start] == '`', "Expected arity to start with a backtick.");

        int i = start + 1; // skip initial '`' char
        for (; i < managedMethodName.Length; i++)
        {
            if (managedMethodName[i] == '(')
            {
                break;
            }
        }

        return int.TryParse(Capture(managedMethodName, start + 1, i), out arity) ? i : throw new InvalidManagedNameException();
    }

    private static int ParseParameterTypeList(string managedMethodName, int start, out string[]? parameterTypes)
    {
        parameterTypes = null;
        if (start == managedMethodName.Length)
        {
            return start;
        }

        Debug.Assert(managedMethodName[start] == '(', "Expected parameter type list to start with open paren.");

        var types = new List<string>();

        int i = start + 1; // skip initial '(' char
        for (; i < managedMethodName.Length; i++)
        {
            switch (managedMethodName[i])
            {
                case ')':
                    if (types.Count != 0)
                    {
                        parameterTypes = types.ToArray();
                    }

                    return i + 1; // consume right parens

                case ',':
                    break;

                default:
                    i = ParseParameterType(managedMethodName, i, out string? parameterType);
                    types.Add(parameterType);
                    break;
            }
        }

        throw new InvalidManagedNameException();
    }

    private static int ParseParameterType(string managedMethodName, int start, out string parameterType)
    {
        parameterType = string.Empty;
        bool quoted = false;

        int i;
        for (i = start; i < managedMethodName.Length; i++)
        {
            if (managedMethodName[i] == '\'' || quoted)
            {
                quoted = managedMethodName[i] == '\'' ? !quoted : quoted;
                continue;
            }

            switch (managedMethodName[i])
            {
                case '<':
                    i = ParseGenericBrackets(managedMethodName, i + 1);
                    break;

                case '[':
                    i = ParseArrayBrackets(managedMethodName, i + 1);
                    break;

                case ',':
                case ')':
                    parameterType = Capture(managedMethodName, start, i);
                    return i - 1;

                case var w when char.IsWhiteSpace(w):
                    throw new InvalidManagedNameException();
            }
        }

        return i;
    }

    private static int ParseArrayBrackets(string managedMethodName, int start)
    {
        bool quoted = false;

        for (int i = start; i < managedMethodName.Length; i++)
        {
            if (managedMethodName[i] == '\'' || quoted)
            {
                quoted = managedMethodName[i] == '\'' ? !quoted : quoted;
                continue;
            }

            switch (managedMethodName[i])
            {
                case ']':
                    return i;
                case var w when char.IsWhiteSpace(w):
                    throw new InvalidManagedNameException();
            }
        }

        throw new InvalidManagedNameException();
    }

    private static int ParseGenericBrackets(string managedMethodName, int start)
    {
        bool quoted = false;

        for (int i = start; i < managedMethodName.Length; i++)
        {
            if (managedMethodName[i] == '\'' || quoted)
            {
                quoted = managedMethodName[i] == '\'' ? !quoted : quoted;
                continue;
            }

            switch (managedMethodName[i])
            {
                case '<':
                    i = ParseGenericBrackets(managedMethodName, i + 1);
                    break;

                case '>':
                    return i;

                case var w when char.IsWhiteSpace(w):
                    throw new InvalidManagedNameException();
            }
        }

        throw new InvalidManagedNameException();
    }
}
