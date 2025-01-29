// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

// Ensure that the title when generating the globalconfig will be in English.
// We have LocalizableResource from DiagnosticDescriptor.Rule, so ToString() will return the localized string.
CultureInfo.CurrentCulture = new CultureInfo("en-US");
CultureInfo.CurrentUICulture = new CultureInfo("en-US");

string? outputPath = Environment.GetEnvironmentVariable("OUTPUT_PATH")
    ?? throw new InvalidOperationException("OUTPUT_PATH environment variable is not set.");

var analyzerFileReference = new AnalyzerFileReference(Path.Combine(outputPath, "MSTest.Analyzers.dll"), AnalyzerAssemblyLoader.Instance);
analyzerFileReference.AnalyzerLoadFailed += (_, e) => throw e.Exception ?? new NotSupportedException(e.Message);
ImmutableArray<DiagnosticAnalyzer> analyzers = analyzerFileReference.GetAnalyzersForAllLanguages();

var builders = new GlobalConfigBuilder[]
{
    new(MSTestAnalysisMode.None),
    new(MSTestAnalysisMode.Default),
    new(MSTestAnalysisMode.Recommended),
    new(MSTestAnalysisMode.All),
};

foreach (DiagnosticDescriptor rule in analyzers.SelectMany(analyzer => analyzer.SupportedDiagnostics).OrderBy(descriptor => descriptor.Id))
{
    DiagnosticSeverity? previousSeverity = null;
    foreach (GlobalConfigBuilder builder in builders)
    {
        DiagnosticSeverity? severity = builder.AppendRule(rule);
        if (!IsGreaterThanOrEqual(severity, previousSeverity))
        {
            // Builders are sorted from the weaker to the stronger.
            // If the current builder produces smaller severity, then something is wrong.
            throw new InvalidOperationException($"Rule '{rule.Id}' produces severity '{severity}' for mode '{builder.Mode}', and severity '{previousSeverity}' for mode '{builder.Mode - 1}'");
        }

        previousSeverity = severity;
    }
}

foreach (GlobalConfigBuilder builder in builders)
{
    builder.WriteToFile(outputPath);
}

static bool IsGreaterThanOrEqual(DiagnosticSeverity? left, DiagnosticSeverity? right)
{
    if (!right.HasValue)
    {
        // right is "none" severity. So, left is always greater than or equal.
        return true;
    }

    if (!left.HasValue)
    {
        // left is "none", and right is a value greater than none.
        return false;
    }

    // Both left and right has value and we can compare normally.
    return left.Value >= right.Value;
}
