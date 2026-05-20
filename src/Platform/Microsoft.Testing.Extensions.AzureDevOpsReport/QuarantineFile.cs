// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Helpers;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed class QuarantineFile
{
    private const int MaxPatternCount = 10_000;
    private const int MaxPatternLength = 4 * 1024;

    // TODO: Consider an opt-in modifier for case-insensitive quarantine matching if customer scenarios require it.
    private static readonly RegexOptions RegexOptions = RegexOptions.CultureInvariant;

    private readonly Regex[] _patterns;

    public QuarantineFile(string path, IFileSystem fileSystem, ILogger logger)
        : this(ParsePatterns(fileSystem.ReadAllText(path), logger))
    {
    }

    internal QuarantineFile(IEnumerable<string> patterns)
        => _patterns = [.. patterns.Select(CreatePatternRegex)];

    public bool Matches(string testFqn)
    {
        if (RoslynString.IsNullOrWhiteSpace(testFqn))
        {
            return false;
        }

        string normalizedTestFqn = Normalize(testFqn);
        foreach (Regex pattern in _patterns)
        {
            if (pattern.IsMatch(normalizedTestFqn))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> ParsePatterns(string fileContent, ILogger logger)
    {
        int patternCount = 0;
        foreach (string line in fileContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmedLine = line.Trim();
            if (trimmedLine.Length == 0 || trimmedLine.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            string normalizedPattern = Normalize(trimmedLine);
            if (normalizedPattern.Length > MaxPatternLength)
            {
                logger.LogWarning(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.QuarantinePatternTooLongWarning, MaxPatternLength));
                continue;
            }

            if (patternCount >= MaxPatternCount)
            {
                logger.LogWarning(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.QuarantinePatternsCappedWarning, MaxPatternCount));
                yield break;
            }

            patternCount++;
            yield return normalizedPattern;
        }
    }

    private static Regex CreatePatternRegex(string pattern)
    {
        string escapedPattern = Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".");

        return new Regex($"^{escapedPattern}$", RegexOptions);
    }

    private static string Normalize(string value)
        => value.Trim();
}
