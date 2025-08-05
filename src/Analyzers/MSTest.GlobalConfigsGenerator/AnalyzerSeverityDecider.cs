// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

using MSTest.Analyzers.Helpers;

// NOTE for our current matrix.
// We decide whether to include a rule in a specific mode or not based on two factors.
// 1. IsEnabledByDefault (true/false)
// 2. DefaultSeverity (Info, Warn, Error) - note: we currently don't use severity Hidden or Error (but we intend to use Error in future)
// So, we have 6 possible combinations.
// 1. IsEnabledByDefault = true, DefaultSeverity = Error (we include that in All, Recommended, and Default)
// 2. IsEnabledByDefault = true, DefaultSeverity = Warn (we include that in All, Recommended, and Default)
// 3. IsEnabledByDefault = true, DefaultSeverity = Info (we include that in All and Recommended)
// 4. IsEnabledByDefault = false, DefaultSeverity = Error (we don't combine Error with not enabled by default)
// 5. IsEnabledByDefault = false, DefaultSeverity = Warn (we don't combine Warn with not enabled by default)
// 6. IsEnabledByDefault = false, DefaultSeverity = Info (we include that in All only)
//
// In addition to that, there are two custom tags that influence the logic.
// 1. EscalateToErrorInRecommended (makes a specific rule as "error" when using Recommended or All mode)
// 2. DisableInAllMode (Always disable the rule - it's completely opt-in)
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

        if (rule.CustomTags.Contains(WellKnownCustomTags.DisableInAllMode))
        {
            if (rule.IsEnabledByDefault || rule.DefaultSeverity > DiagnosticSeverity.Info)
            {
                throw new InvalidOperationException("Rules with DisableInAllMode custom tag are expected to be disabled by default and have severity Info.");
            }
            else if (severity != null)
            {
                throw new InvalidOperationException("Rules with DisableInAllMode custom tag are expected to be disabled.");
            }
        }

        return severity;
    }

    private static DiagnosticSeverity? DecideForModeAll(DiagnosticDescriptor rule)
    {
        if (rule.CustomTags.Contains(WellKnownCustomTags.EscalateToErrorInRecommended))
        {
            if (rule.DefaultSeverity != DiagnosticSeverity.Warning)
            {
                // It feels odd to escalate a rule to Error in Recommended mode if it's not a warning by default.
                throw new InvalidOperationException("Is it intended that default severity is not warning when escalating to error in recommended mode?");
            }

            return DiagnosticSeverity.Error;
        }
        else if (rule.CustomTags.Contains(WellKnownCustomTags.DisableInAllMode))
        {
            return null;
        }

        // NOTE: If we decided to introduce an analyzer with default severity as Error,
        // then analysis mode all shouldn't change that.
        return (DiagnosticSeverity)Math.Max((int)DiagnosticSeverity.Warning, (int)rule.DefaultSeverity);
    }

    private static DiagnosticSeverity? DecideForModeRecommended(DiagnosticDescriptor rule)
    {
        if (rule.CustomTags.Contains(WellKnownCustomTags.EscalateToErrorInRecommended))
        {
            if (rule.DefaultSeverity != DiagnosticSeverity.Warning)
            {
                // It feels odd to escalate a rule to Error in Recommended mode if it's not a warning by default.
                throw new InvalidOperationException("Is it intended that default severity is not warning when escalating to error in recommended mode?");
            }

            return DiagnosticSeverity.Error;
        }

        if (rule.IsEnabledByDefault && rule.DefaultSeverity >= DiagnosticSeverity.Info)
        {
            // Recommended mode will elevate info to warning only if the rule is enabled by default.
            // In addition, if the rule is already error by default, we keep it as error. So, choose the max between warning and default severity.
            return (DiagnosticSeverity)Math.Max((int)rule.DefaultSeverity, (int)DiagnosticSeverity.Warning);
        }

        if (rule.DefaultSeverity >= DiagnosticSeverity.Warning)
        {
            throw new InvalidOperationException($"Rule '{rule.Id}' with severity >= Warning is expected to be enabled by default.");
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

    private static DiagnosticSeverity? DecideForModeNone(DiagnosticDescriptor rule)
        // Even with 'None' mode, we still keep the rules that are errors by default.
        // Such rules are likely to be critical and shouldn't be suppressed by MSTestAnalysisMode None.
        => rule.DefaultSeverity == DiagnosticSeverity.Error ? DiagnosticSeverity.Error : null;
}
