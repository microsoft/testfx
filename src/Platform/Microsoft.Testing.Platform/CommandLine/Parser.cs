// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.CommandLine;

internal static class CommandLineParser
{
    /// <summary>
    /// Options parser support:
    ///     * Only - and -- prefix for options https://learn.microsoft.com/dotnet/standard/commandline/syntax#options
    ///     * Multiple option arguments https://learn.microsoft.com/dotnet/standard/commandline/syntax#multiple-arguments
    ///     * Use '=' or ':' as the delimiter between an option name and its argument. See https://learn.microsoft.com/dotnet/standard/commandline/syntax#option-argument-delimiters
    ///     * escape with \
    ///     * surrounding with ""
    ///     * surrounding with ''
    ///
    /// Options parser doesn't support
    ///     * Default argument/commands/verb
    ///     * Alias https://learn.microsoft.com/dotnet/standard/commandline/syntax#aliases
    ///     * Case sensitivity https://learn.microsoft.com/dotnet/standard/commandline/syntax#case-sensitivity
    ///     * -- token https://learn.microsoft.com/dotnet/standard/commandline/syntax#the----token
    ///
    /// https://pubs.opengroup.org/onlinepubs/9699919799/utilities/V3_chap02.html#tag_18_02_03
    /// https://learn.microsoft.com/cpp/c-language/parsing-c-command-line-arguments?view=msvc-170
    /// Double-Quotes: we don't support for now
    ///     * The dollar-sign shall retain its special meaning introducing parameter expansion
    ///     * The backquote shall retain its special meaning introducing the other form of command substitution
    ///     * A POSIX convention lets you omit the delimiter when you are specifying a single-character option alias, i.e. myapp -vquiet.
    /// </summary>
    public static CommandLineParseResult Parse(string[] args, IEnvironment environment)
        => Parse(args.ToList(), environment);

    private static CommandLineParseResult Parse(List<string> args, IEnvironment environment)
    {
        List<CommandLineParseOption> options = [];
        List<string> errors = [];

        string? currentOption = null;
        string? toolName = null;
        List<string> currentOptionArguments = [];
        bool isFirstRealArgument = true;
        for (int i = 0; i < args.Count; i++)
        {
            string? currentArg = args[i];

            if (currentArg.StartsWith("@", StringComparison.Ordinal) && ResponseFileHelper.TryReadResponseFile(currentArg.Substring(1), errors, out string[]? newArguments))
            {
                args.InsertRange(i + 1, newArguments);
                continue;
            }

            // If it's the first argument and it doesn't start with - then it's the tool name
            // TODO: This won't work correctly if the first argument provided is a response file that contains the tool name.
            if (isFirstRealArgument && currentArg.Length > 0 && currentArg[0] != '-')
            {
                toolName = currentArg;
                isFirstRealArgument = false;
                continue;
            }

            isFirstRealArgument = false;

            // we accept as start for options -- and - all the rest are arguments to the previous option
            if ((currentArg.Length > 1 && currentArg[0].Equals('-') && !currentArg[1].Equals('-')) ||
                (currentArg.Length > 2 && currentArg[0].Equals('-') && currentArg[1].Equals('-') && !currentArg[2].Equals('-')))
            {
                if (currentOption is not null)
                {
                    options.Add(new(currentOption, [.. currentOptionArguments]));
                    currentOptionArguments.Clear();
                }

                ParseOptionAndSeparators(currentArg, out currentOption, out currentArg);
            }

            if (currentArg is not null)
            {
                // When a quoted value is split across multiple args (e.g. a path with spaces),
                // reassemble the fragments into a single argument.
                currentArg = TryMergeQuotedArguments(args, ref i, currentArg);

                if (currentOption is null)
                {
                    errors.Add(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineParserUnexpectedArgument, args[i]));
                }
                else
                {
                    if (TryUnescape(currentArg.Trim(), currentOption, environment, out string? unescapedArg, out string? error))
                    {
                        currentOptionArguments.Add(unescapedArg);
                    }
                    else
                    {
                        errors.Add(error);
                    }
                }
            }
        }

        if (currentOption is not null)
        {
            options.Add(new(currentOption, [.. currentOptionArguments]));
        }

        return new CommandLineParseResult(toolName, options, errors);

        static void ParseOptionAndSeparators(string arg, out string? currentOption, out string? currentArg)
        {
            (currentOption, currentArg) = arg.IndexOfAny([':', '=']) switch
            {
                -1 => (arg, null),
                var delimiterIndex => (arg[..delimiterIndex], arg[(delimiterIndex + 1)..]),
            };

            currentOption = currentOption.TrimStart('-');
        }

        // When a quoted argument value is split across multiple args[] entries (e.g. a path
        // containing spaces like "/path/my dir/log"), reassemble the fragments into a single
        // string. Handles double-quote, single-quote, and escaped-quote (\") wrapping.
        static string TryMergeQuotedArguments(List<string> args, ref int i, string currentArg)
        {
            string trimmed = currentArg.Trim();
            if (trimmed.Length == 0)
            {
                return currentArg;
            }

            // Determine the opening quote style and whether it's already closed.
            string? closingSuffix;
            if (trimmed.StartsWith("\\\"", StringComparison.Ordinal))
            {
                // Escaped double-quote: \"...\"
                if (trimmed.Length > 4 && trimmed.EndsWith("\\\"", StringComparison.Ordinal))
                {
                    return currentArg;
                }

                closingSuffix = "\\\"";
            }
            else if (trimmed[0] == '"')
            {
                if (trimmed.Length > 1 && trimmed[trimmed.Length - 1] == '"')
                {
                    return currentArg;
                }

                closingSuffix = "\"";
            }
            else if (trimmed[0] == '\'')
            {
                if (trimmed.Length > 1 && trimmed[trimmed.Length - 1] == '\'')
                {
                    return currentArg;
                }

                closingSuffix = "'";
            }
            else
            {
                return currentArg;
            }

            // Merge subsequent args until the closing quote is found.
            StringBuilder merged = new(currentArg);
            while (i + 1 < args.Count)
            {
                string nextArg = args[i + 1];
                i++;
                merged.Append(' ').Append(nextArg);

                if (nextArg.TrimEnd().EndsWith(closingSuffix, StringComparison.Ordinal))
                {
                    return merged.ToString();
                }
            }

            // Closing quote was never found — return what we assembled.
            // TryUnescape / arity validation will report appropriate errors downstream.
            return merged.ToString();
        }

        static bool TryUnescape(string input, string? option, IEnvironment environment, [NotNullWhen(true)] out string? unescapedArg, [NotNullWhen(false)] out string? error)
        {
            unescapedArg = input;
            error = null;

            // Enclosing characters in single-quotes ( '' ) shall preserve the literal value of each character within the single-quotes.
            // A single-quote cannot occur within single-quotes.
            if (input.StartsWith("\'", StringComparison.Ordinal) && input.EndsWith("\'", StringComparison.Ordinal))
            {
                if (input.IndexOf('\'', 1, input.Length - 2) != -1)
                {
                    error = option is null
                        ? string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineParserUnexpectedSingleQuoteInArgument, input)
                        : string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineParserUnexpectedSingleQuoteInArgumentForOption, input, option);
                    return false;
                }

                unescapedArg = input[1..^1];
                return true;
            }

            // Handle escaped double-quotes: \"...\"
            // Some process launchers (e.g. VS Code on Linux) emit escaped quotes that arrive
            // as literal backslash-quote characters in the args array.
            if (input.StartsWith("\\\"", StringComparison.Ordinal) && input.EndsWith("\\\"", StringComparison.Ordinal) && input.Length > 4)
            {
                unescapedArg = input[2..^2];
                return true;
            }

            // Enclosing characters in double-quotes("") shall preserve the literal value of all characters within the double-quotes,
            // with the exception of the characters backquote, <dollar-sign>, and <backslash>, as follows:
            //  * The backquote shall retain its special meaning introducing the other form of command substitution. [NOT SUPPORTED]
            //  * The <dollar-sign> shall retain its special meaning introducing parameter expansion. [NOT SUPPORTED]
            //  * The backslash shall retain its special meaning as an escape character only when followed by one of the following characters when considered special:
            //    $   `   "   \   <newline>
            if (input.StartsWith("\"", StringComparison.Ordinal) && input.EndsWith("\"", StringComparison.Ordinal))
            {
                unescapedArg = input[1..^1].Replace(@"\\", "\\")
                    .Replace(@"\""", "\"")
                    .Replace(@"\$", "$")
                    .Replace(@"\`", "`")
                    .Replace($@"\{environment.NewLine}", environment.NewLine);
                return true;
            }

            return true;
        }
    }
}
