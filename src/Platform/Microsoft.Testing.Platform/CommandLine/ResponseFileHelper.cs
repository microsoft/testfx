// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Resources;

// Most of the core logic is from:
// - https://github.com/dotnet/command-line-api/blob/feb61c7f328a2401d74f4317b39d02126cfdfe24/src/System.CommandLine/Parsing/CliParser.cs#L40
// - https://github.com/dotnet/command-line-api/blob/feb61c7f328a2401d74f4317b39d02126cfdfe24/src/System.CommandLine/Parsing/StringExtensions.cs#L349
internal static class ResponseFileHelper
{
    internal static bool TryReadResponseFile(string rspFilePath, ICollection<string> errors, [NotNullWhen(true)] out string[]? newArguments)
    {
        try
        {
            newArguments = [.. ExpandResponseFile(rspFilePath)];
            return true;
        }
        catch (FileNotFoundException)
        {
            errors.Add(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineParserResponseFileNotFound, rspFilePath));
        }
        catch (IOException e)
        {
            errors.Add(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineParserFailedToReadResponseFile, rspFilePath, e.ToString()));
        }

        newArguments = null;
        return false;

        // Local functions
        static IEnumerable<string> ExpandResponseFile(string filePath)
        {
            string[] lines = File.ReadAllLines(filePath);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                foreach (string p in SplitLine(line))
                {
                    yield return p;
                }
            }
        }

        static IEnumerable<string> SplitLine(string line)
        {
            string arg = line.Trim();

            if (arg.Length == 0 || arg[0] == '#')
            {
                yield break;
            }

            foreach (string word in SplitCommandLine(arg))
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
