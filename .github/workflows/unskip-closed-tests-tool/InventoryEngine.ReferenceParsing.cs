using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnskipClosedTests.Tool;

internal static partial class InventoryEngine
{
    internal static bool AttributeMatches(AttributeSyntax attribute, IEnumerable<string> configuredNames)
    {
        string actual = attribute.Name.WithoutTrivia().ToFullString().Replace("global::", "", StringComparison.Ordinal);
        string actualShort = actual.Split('.').Last();
        return configuredNames.Any(configured =>
        {
            string normalized = configured.EndsWith("Attribute", StringComparison.Ordinal)
                ? configured[..^"Attribute".Length]
                : configured;
            string actualNormalized = actual.EndsWith("Attribute", StringComparison.Ordinal)
                ? actual[..^"Attribute".Length]
                : actual;
            string shortNormalized = actualShort.EndsWith("Attribute", StringComparison.Ordinal)
                ? actualShort[..^"Attribute".Length]
                : actualShort;
            return normalized.Contains('.', StringComparison.Ordinal)
                ? string.Equals(normalized, actualNormalized, StringComparison.Ordinal)
                : string.Equals(normalized, shortNormalized, StringComparison.Ordinal);
        });
    }

    private static List<IssueReference> ExtractReferences(AttributeSyntax attribute, string currentRepository)
    {
        List<IssueReference> references = [];
        if (attribute.ArgumentList is null)
        {
            return references;
        }

        foreach (AttributeArgumentSyntax argument in attribute.ArgumentList.Arguments)
        {
            bool supportedName = argument.NameEquals is null ||
                string.Equals(argument.NameEquals.Name.Identifier.ValueText, "IgnoreMessage", StringComparison.Ordinal);
            if (!supportedName || argument.NameColon is not null ||
                argument.Expression is not LiteralExpressionSyntax literal ||
                !literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                continue;
            }

            string value = literal.Token.ValueText;
            references.AddRange(ParseReferences(value, currentRepository));
        }

        return references
            .GroupBy(static reference => reference.Canonical, StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static reference => reference.Canonical, StringComparer.Ordinal)
            .ToList();
    }

    internal static IEnumerable<IssueReference> ParseReferences(string value, string currentRepository)
    {
        List<(int Start, int Length, string Owner, string Repo, int Number, string Kind)> matches = [];
        foreach (Match match in FullReferenceRegex().Matches(value))
        {
            matches.Add((
                match.Index,
                match.Length,
                match.Groups["owner"].Value,
                match.Groups["repo"].Value,
                int.Parse(match.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture),
                match.Groups["kind"].Value.Equals("pull", StringComparison.OrdinalIgnoreCase) ? "pull_request" : "issue"));
        }

        foreach (Match match in QualifiedReferenceRegex().Matches(value))
        {
            if (matches.Any(existing => RangesOverlap(existing.Start, existing.Length, match.Index, match.Length)))
            {
                continue;
            }

            matches.Add((
                match.Index,
                match.Length,
                match.Groups["owner"].Value,
                match.Groups["repo"].Value,
                int.Parse(match.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture),
                "unknown"));
        }

        string[] current = currentRepository.Split('/');
        foreach (Match match in BareReferenceRegex().Matches(value))
        {
            if (matches.Any(existing => RangesOverlap(existing.Start, existing.Length, match.Index, match.Length)))
            {
                continue;
            }

            matches.Add((
                match.Index,
                match.Length,
                current[0],
                current[1],
                int.Parse(match.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture),
                "unknown"));
        }

        foreach ((_, _, string ownerValue, string repoValue, int number, string kind) in matches.OrderBy(static item => item.Start))
        {
            if (number <= 0)
            {
                continue;
            }

            string owner = ownerValue.ToLowerInvariant();
            string repo = repoValue.ToLowerInvariant();
            string pathKind = kind == "pull_request" ? "pull" : "issues";
            yield return new IssueReference
            {
                Kind = kind,
                Owner = owner,
                Repo = repo,
                Number = number,
                Canonical = $"{owner}/{repo}#{number}",
                Url = $"https://github.com/{owner}/{repo}/{pathKind}/{number}",
                Eligibility = false,
                State = "unknown",
                StateReason = "unresolved",
            };
        }
    }

    private static bool RangesOverlap(int firstStart, int firstLength, int secondStart, int secondLength) =>
        firstStart < secondStart + secondLength && secondStart < firstStart + firstLength;

    [GeneratedRegex(
        @"https://github\.com/(?<owner>[A-Za-z0-9_.-]+)/(?<repo>[A-Za-z0-9_.-]+)/(?<kind>issues|pull)/(?<number>[1-9][0-9]*)(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex FullReferenceRegex();

    [GeneratedRegex(
        @"(?<![A-Za-z0-9_.-])(?<owner>[A-Za-z0-9_.-]+)/(?<repo>[A-Za-z0-9_.-]+)#(?<number>[1-9][0-9]*)(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant)]
    private static partial Regex QualifiedReferenceRegex();

    [GeneratedRegex(
        @"(?<![A-Za-z0-9_/#])#(?<number>[1-9][0-9]*)(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant)]
    private static partial Regex BareReferenceRegex();
}
