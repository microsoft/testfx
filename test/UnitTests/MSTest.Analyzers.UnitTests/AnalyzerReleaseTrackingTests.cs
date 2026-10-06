// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Analyzers.UnitTests;

[TestClass]
public sealed class AnalyzerReleaseTrackingTests
{
    private static string AnalyzerDirectory => Path.Combine(RootFinder.Find(), "src", "Analyzers", "MSTest.Analyzers");

    [TestMethod]
    public void EveryDiagnosticAndSuppressionIdIsTracked()
    {
        string releases = ReadDiagnosticReleases()
            + File.ReadAllText(Path.Combine(AnalyzerDirectory, "SuppressionReleases.md"));
        HashSet<string> trackedIds = GetTrackedIds(releases);

        Assembly analyzerAssembly = typeof(NonNullableReferenceNotInitializedSuppressor).Assembly;
        Type diagnosticIds = analyzerAssembly.GetType("MSTest.Analyzers.Helpers.DiagnosticIds")!;
        IEnumerable<string> definedIds = diagnosticIds.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (string)field.GetRawConstantValue()!)
            .Concat(GetSupportedSuppressions().Select(rule => rule.Id));
        string[] missingIds = definedIds
            .Where(id => !trackedIds.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.IsEmpty(missingIds);
    }

    [TestMethod]
    public void SuppressionCatalogMatchesSupportedSuppressions()
    {
        string releases = File.ReadAllText(Path.Combine(AnalyzerDirectory, "SuppressionReleases.md"));
        (string Id, string SuppressedDiagnosticId)[] trackedSuppressions = Regex.Matches(releases, @"^(MSTEST\d{4})\s*\|\s*(\w+)\s*\|", RegexOptions.Multiline)
            .Cast<Match>()
            .Select(match => (Id: match.Groups[1].Value, SuppressedDiagnosticId: match.Groups[2].Value))
            .ToArray();
        (string Id, string SuppressedDiagnosticId)[] supportedSuppressions = GetSupportedSuppressions()
            .Select(rule => (rule.Id, rule.SuppressedDiagnosticId))
            .ToArray();

        Assert.AreSequenceEqual(supportedSuppressions, trackedSuppressions, SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public void SuppressionIdsAreNotTrackedAsDiagnostics()
    {
        HashSet<string> trackedIds = GetTrackedIds(ReadDiagnosticReleases());
        string[] incorrectlyTrackedIds = GetSupportedSuppressions()
            .Select(rule => rule.Id)
            .Where(trackedIds.Contains)
            .ToArray();

        Assert.IsEmpty(incorrectlyTrackedIds);
    }

    private static string ReadDiagnosticReleases()
        => File.ReadAllText(Path.Combine(AnalyzerDirectory, "AnalyzerReleases.Shipped.md"))
            + File.ReadAllText(Path.Combine(AnalyzerDirectory, "AnalyzerReleases.Unshipped.md"));

    private static HashSet<string> GetTrackedIds(string releases)
        => Regex.Matches(releases, @"^MSTEST\d{4}\s*\|", RegexOptions.Multiline)
            .Cast<Match>()
            .Select(match => match.Value.Split('|')[0].Trim())
            .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<SuppressionDescriptor> GetSupportedSuppressions()
        => typeof(NonNullableReferenceNotInitializedSuppressor).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(DiagnosticSuppressor).IsAssignableFrom(type))
            .SelectMany(type => ((DiagnosticSuppressor)Activator.CreateInstance(type)!).SupportedSuppressions);
}
