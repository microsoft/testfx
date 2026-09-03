// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
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
        var readContext = new ResponseFileReadContext(rspFilePath, diagnosticPath);
        try
        {
            newArguments = [.. ExpandResponseFile(
                rspFilePath,
                diagnosticPath,
                [with(
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? StringComparer.OrdinalIgnoreCase
                        : StringComparer.Ordinal)],
                readContext)];
            return true;
        }
        catch (FileNotFoundException)
        {
            errors.Add(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineParserResponseFileNotFound, readContext.DiagnosticPath));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            errors.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.CommandLineParserFailedToReadResponseFile,
                    readContext.DiagnosticPath,
                    GetExceptionDetail(e, readContext.ActualPath, readContext.DiagnosticPath)));
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
                    readContext.DiagnosticPath,
                    GetExceptionDetail(e, readContext.ActualPath, readContext.DiagnosticPath)));
        }

        newArguments = null;
        return false;

        // Local functions
        static string GetExceptionDetail(Exception exception, string actualPath, string diagnosticPath)
            => actualPath == diagnosticPath ? exception.ToString() : exception.GetType().Name;

        static IEnumerable<string> ExpandResponseFile(
            string filePath,
            string diagnosticPath,
            HashSet<string> activeResponseFiles,
            ResponseFileReadContext readContext)
        {
            readContext.SetCurrentFile(filePath, diagnosticPath);
            string fullPath = Path.GetFullPath(filePath);
            if (!activeResponseFiles.Add(fullPath))
            {
                throw new FormatException(PlatformResources.CommandLineParserRecursiveResponseFile);
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath);
                List<string> arguments = [];
                for (int i = 0; i < lines.Length; i++)
                {
                    arguments.AddRange(SplitLine(lines[i], i + 1));
                }

                for (int argumentIndex = 0; argumentIndex < arguments.Count; argumentIndex++)
                {
                    string argument = arguments[argumentIndex];
                    if (argument.StartsWith("@", StringComparison.Ordinal))
                    {
                        string nestedDiagnosticPath;
                        if (filePath != diagnosticPath)
                        {
                            nestedDiagnosticPath = diagnosticPath;
                        }
                        else
                        {
                            string redactedNestedArgument = CommandLineArgumentsRedactor.RedactArgument([.. arguments], argumentIndex);
                            nestedDiagnosticPath = redactedNestedArgument.StartsWith("@", StringComparison.Ordinal)
                                ? redactedNestedArgument[1..]
                                : redactedNestedArgument;
                        }

                        // Nested response files intentionally use the process working directory, just like
                        // top-level response files, rather than the containing response file's directory.
                        foreach (string nestedArgument in ExpandResponseFile(
                            argument[1..],
                            nestedDiagnosticPath,
                            activeResponseFiles,
                            readContext))
                        {
                            yield return nestedArgument;
                        }

                        readContext.SetCurrentFile(filePath, diagnosticPath);
                    }
                    else
                    {
                        yield return argument;
                    }
                }
            }
            finally
            {
                activeResponseFiles.Remove(fullPath);
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

    private sealed class ResponseFileReadContext(string actualPath, string diagnosticPath)
    {
        public string ActualPath { get; private set; } = actualPath;

        public string DiagnosticPath { get; private set; } = diagnosticPath;

        public void SetCurrentFile(string currentActualPath, string currentDiagnosticPath)
        {
            ActualPath = currentActualPath;
            DiagnosticPath = currentDiagnosticPath;
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
