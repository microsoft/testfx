// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

internal sealed class GlobalConfigBuilder
{
    private readonly StringBuilder _builder = new();
    private readonly Dictionary<string, DiagnosticSeverity?> _severityDictionary = [];

    public GlobalConfigBuilder(MSTestAnalysisMode mode)
    {
        Mode = mode;
        _builder.AppendLine(CultureInfo.InvariantCulture, $"# A GlobalConfig file for the '{mode}' MSTest analysis mode.");
        _builder.AppendLine();
        _builder.AppendLine("is_global = true");
        _builder.AppendLine("global_level = -100");
        _builder.AppendLine();
    }

    public MSTestAnalysisMode Mode { get; }

    public DiagnosticSeverity? AppendRule(DiagnosticDescriptor rule)
    {
        DiagnosticSeverity? severity = AnalyzerSeverityDecider.GetSeverity(rule, Mode);
        if (_severityDictionary.TryGetValue(rule.Id, out DiagnosticSeverity? existingSeverity))
        {
            return existingSeverity != severity
                ? throw new InvalidOperationException($"Rule '{rule.Id}' has conflicting severities '{existingSeverity}' and '{severity}'.")
                : severity;
        }

        _severityDictionary.Add(rule.Id, severity);

        string severityString = severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            DiagnosticSeverity.Info => "suggestion",
            DiagnosticSeverity.Hidden => "silent",
            null => "none",
            _ => throw new InvalidOperationException($"Unexpected severity '{severity}'."),
        };

        _builder.AppendLine(CultureInfo.InvariantCulture, $"# {rule.Id}: {rule.Title}");
        _builder.AppendLine(CultureInfo.InvariantCulture, $"dotnet_diagnostic.{rule.Id}.severity = {severityString}");
        _builder.AppendLine();

        return severity;
    }

    internal void WriteToFile(string outputPath)
    {
        string globalconfigsDirectory = Path.Combine(outputPath, "globalconfigs");
        Directory.CreateDirectory(globalconfigsDirectory);
        string globalconfigPath = Path.Combine(globalconfigsDirectory, $"mstest-{Mode.ToString().ToLowerInvariant()}.globalconfig");
        File.WriteAllText(globalconfigPath, _builder.ToString());
    }
}
