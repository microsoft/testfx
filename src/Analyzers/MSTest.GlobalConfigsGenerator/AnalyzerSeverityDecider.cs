// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

// NOTE for our current matrix.
// We decide whether to include a rule in a specific mode or not based on two factors.
// 1. IsEnabledByDefault (true/false)
// 2. DefaultSeverity (Info, Warn) - note: we currently don't use severity Hidden or Error.
// So, we have 4 possible combinations.
// 1. IsEnabledByDefault = true, DefaultSeverity = Warn (we include that in All, Recommended, and Default)
// 2. IsEnabledByDefault = true, DefaultSeverity = Info (we include that in All and Recommended)
// 3. IsEnabledByDefault = false, DefaultSeverity = Warn (we don't combine Warn with not enabled by default)
// 4. IsEnabledByDefault = false, DefaultSeverity = Info (we include that in All only)
internal static class AnalyzerSeverityDecider
{
    public static DiagnosticSeverity? GetSeverity(DiagnosticDescriptor rule, MSTestAnalysisMode mode)
    {
        DiagnosticSeverity? severity = mode switch
        {
            MSTestAnalysisMode.All => DecideForModeAll(rule),
            MSTestAnalysisMode.Recommended => DecideForModeRecommended(rule),
            MSTestAnalysisMode.Default => DecideForModeDefault(rule),
            MSTestAnalysisMode.None => DecideForModeNone(rule),
            _ => throw new ArgumentException($"Unexpected MSTestAnalysisMode '{mode}'.", nameof(mode)),
        };

        return severity;
    }

    private static DiagnosticSeverity? DecideForModeAll(DiagnosticDescriptor rule) =>
        // TODO: We should consider at least not enabling "conflicting" rules.
        // Or alternatively, have a configuration with reasonable default for such rules.
        // Example of conflicting rules is an analyzer that suggests Dispose instead of
        // cleanup, and another analyzer that suggests the opposite.

        // NOTE: If, for any odd case, we decided to introduce an analyzer with default severity as Error,
        // then analysis mode all shouldn't change that.
        (DiagnosticSeverity)Math.Max((int)DiagnosticSeverity.Warning, (int)rule.DefaultSeverity);

    private static DiagnosticSeverity? DecideForModeRecommended(DiagnosticDescriptor rule)
    {
        if (rule.IsEnabledByDefault && rule.DefaultSeverity >= DiagnosticSeverity.Info)
        {
            // Recommended mode will elevate info to warning only if the rule is enabled by default.
            return DiagnosticSeverity.Warning;
        }

        if (rule.DefaultSeverity >= DiagnosticSeverity.Warning)
        {
            throw new InvalidOperationException("Rules with severity >= Warning are expected to be enabled by default.");
        }

        // If a rule is enabled by default, Recommended keeps it as Info.
        // If a rule is disabled by default, Recommended keeps it disabled.
        return rule.IsEnabledByDefault ? DiagnosticSeverity.Info : null;
    }

    private static DiagnosticSeverity? DecideForModeDefault(DiagnosticDescriptor rule)
    {
        if (rule.IsEnabledByDefault && rule.DefaultSeverity >= DiagnosticSeverity.Warning)
        {
            // Default mode will enable warnings only if the rule is enabled by default.
            return rule.DefaultSeverity;
        }

        if (rule.DefaultSeverity >= DiagnosticSeverity.Warning)
        {
            throw new InvalidOperationException("Rules with severity >= Warning are expected to be enabled by default.");
        }

        // If a rule is enabled by default, Default keeps it at its original severity.
        // If a rule is disabled by default, Default keeps it disabled.
        return rule.IsEnabledByDefault ? rule.DefaultSeverity : null;
    }

    private static DiagnosticSeverity? DecideForModeNone(DiagnosticDescriptor _)
        => null;
}
