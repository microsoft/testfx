// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Analyzers.UnitTests;

[TestClass]
public sealed class AnalyzerReleaseTrackingTests
{
    [TestMethod]
    public void EveryDiagnosticAndSuppressionIdIsTracked()
    {
        string analyzerDirectory = Path.Combine(RootFinder.Find(), "src", "Analyzers", "MSTest.Analyzers");
        string releases = File.ReadAllText(Path.Combine(analyzerDirectory, "AnalyzerReleases.Shipped.md"))
            + File.ReadAllText(Path.Combine(analyzerDirectory, "AnalyzerReleases.Unshipped.md"));
        var trackedIds = Regex.Matches(releases, @"^MSTEST\d{4}\s*\|", RegexOptions.Multiline)
            .Select(match => match.Value.Split('|')[0].Trim())
            .ToHashSet(StringComparer.Ordinal);

        Assembly analyzerAssembly = typeof(NonNullableReferenceNotInitializedSuppressor).Assembly;
        Type diagnosticIds = analyzerAssembly.GetType("MSTest.Analyzers.Helpers.DiagnosticIds")!;
        IEnumerable<string> definedIds = diagnosticIds.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (string)field.GetRawConstantValue()!)
            .Concat(analyzerAssembly.GetTypes()
                .Where(type => !type.IsAbstract && typeof(DiagnosticSuppressor).IsAssignableFrom(type))
                .SelectMany(type => ((DiagnosticSuppressor)Activator.CreateInstance(type)!).SupportedSuppressions)
                .Select(rule => rule.Id));
        string[] missingIds = definedIds
            .Where(id => !trackedIds.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.AreEqual(string.Empty, string.Join(", ", missingIds));
    }
}
