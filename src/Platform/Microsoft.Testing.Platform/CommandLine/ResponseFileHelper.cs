// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Resources;

// Most of the core logic is from:
// - https://github.com/dotnet/command-line-api/blob/fa1991f84bc8c384aa636a251398a40e56ee1702/src/System.CommandLine/Parsing/CommandLineParser.cs#L40
// - https://github.com/dotnet/command-line-api/blob/fa1991f84bc8c384aa636a251398a40e56ee1702/src/System.CommandLine/Parsing/StringExtensions.cs#L316
internal static class ResponseFileHelper
{
    internal static bool TryReadResponseFile(
        string rspFilePath,
        ICollection<string> errors,
        [NotNullWhen(true)] out string[]? newArguments)
        => TryReadResponseFile(rspFilePath, rspFilePath, errors, out newArguments);

    internal static bool TryReadResponseFile(
        string rspFilePath,
        string diagnosticPath,
        ICollection<string> errors,
        [NotNullWhen(true)] out string[]? newArguments)
    {
        try
        {
            newArguments = [.. ExpandResponseFile(rspFilePath)];
            return true;
        }
        catch (FileNotFoundException)
        {
            errors.Add(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineParserResponseFileNotFound, diagnosticPath));
        }
        catch (IOException e)
        {
            errors.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.CommandLineParserFailedToReadResponseFile,
                    diagnosticPath,
                    GetExceptionDetail(e, rspFilePath, diagnosticPath)));
        }
        catch (FormatException e)
        {
            // Use the full exception detail (not just Message) for consistency with the IOException
            // branch above; the response file content that triggered a parsing failure (e.g. an
            // unclosed quote) is easier to diagnose with the complete exception information.
            errors.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.CommandLineParserFailedToReadResponseFile,
                    diagnosticPath,
                    GetExceptionDetail(e, rspFilePath, diagnosticPath)));
        }

        newArguments = null;
        return false;

        // Local functions
        static string GetExceptionDetail(Exception exception, string actualPath, string diagnosticPath)
            => actualPath == diagnosticPath ? exception.ToString() : exception.GetType().Name;

        static IEnumerable<string> ExpandResponseFile(string filePath)
        {
            string[] lines = File.ReadAllLines(filePath);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                foreach (string p in SplitLine(line, i + 1))
                {
                    yield return p;
                }
            }
        }

        static IEnumerable<string> SplitLine(string line, int lineNumber)
        {
            string arg = line.Trim();

            if (arg.Length == 0 || arg[0] == '#')
            {
                yield break;
            }

            foreach (string word in SplitCommandLine(arg, lineNumber))
            {
                yield return word;
            }
        }
    }

    private enum Boundary
    {
        TokenStart,
        WordEnd,
        QuoteStart,
        QuoteEnd,
    }

    public static IEnumerable<string> SplitCommandLine(string commandLine)
        => SplitCommandLine(commandLine, lineNumber: null);

    private static IEnumerable<string> SplitCommandLine(string commandLine, int? lineNumber)
    {
        int startTokenIndex = 0;

        int pos = 0;

        Boundary seeking = Boundary.TokenStart;
        Boundary seekingQuote = Boundary.QuoteStart;

        while (pos < commandLine.Length)
        {
            char c = commandLine[pos];

            if (char.IsWhiteSpace(c))
            {
                if (seekingQuote == Boundary.QuoteStart)
                {
                    switch (seeking)
                    {
                        case Boundary.WordEnd:
                            yield return CurrentToken();
                            startTokenIndex = pos;
                            seeking = Boundary.TokenStart;
                            break;

                        case Boundary.TokenStart:
                            startTokenIndex = pos;
                            break;
                    }
                }
            }
            else if (c == '\"')
            {
                if (seeking == Boundary.TokenStart)
                {
                    switch (seekingQuote)
                    {
                        case Boundary.QuoteEnd:
                            yield return CurrentToken();
                            startTokenIndex = pos;
                            seekingQuote = Boundary.QuoteStart;
                            break;

                        case Boundary.QuoteStart:
                            startTokenIndex = pos + 1;
                            seekingQuote = Boundary.QuoteEnd;
                            break;
                    }
                }
                else
                {
                    switch (seekingQuote)
                    {
                        case Boundary.QuoteEnd:
                            seekingQuote = Boundary.QuoteStart;
                            break;

                        case Boundary.QuoteStart:
                            seekingQuote = Boundary.QuoteEnd;
                            break;
                    }
                }
            }
            else if (seeking == Boundary.TokenStart && seekingQuote == Boundary.QuoteStart)
            {
                seeking = Boundary.WordEnd;
                startTokenIndex = pos;
            }

            Advance();

            if (IsAtEndOfInput())
            {
                if (seekingQuote == Boundary.QuoteEnd)
                {
                    throw new FormatException(lineNumber is null
                        ? PlatformResources.CommandLineParserUnclosedQuoteInCommandLine
                        : string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineParserUnclosedQuoteInResponseFile, lineNumber));
                }

                switch (seeking)
                {
                    case Boundary.TokenStart:
                        break;
                    default:
                        yield return CurrentToken();
                        break;
                }
            }
        }

        void Advance() => pos++;

        string CurrentToken() => commandLine.Substring(startTokenIndex, IndexOfEndOfToken()).Replace("\"", string.Empty);

        int IndexOfEndOfToken() => pos - startTokenIndex;

        bool IsAtEndOfInput() => pos == commandLine.Length;
    }
}
