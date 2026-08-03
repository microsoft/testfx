// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal static class AzureDevOpsConstants
{
    /// <summary>
    /// Name of the Azure Pipelines environment variable that indicates whether the
    /// current process is running inside an Azure DevOps build/release agent.
    /// </summary>
    public const string TfBuildEnvironmentVariableName = "TF_BUILD";

    /// <summary>
    /// Expected value of <see cref="TfBuildEnvironmentVariableName"/> when running
    /// inside an Azure DevOps build/release agent. Azure DevOps actually sets the
    /// variable to <c>True</c> (capitalized), but the comparison performed by
    /// <see cref="IsRunningInAzureDevOps(IEnvironment)"/> is case-insensitive so any
    /// casing of <c>true</c> is accepted.
    /// </summary>
    public const string TfBuildEnabledValue = "true";

    /// <summary>
    /// Name of the environment variable that carries the id of an Azure DevOps test run created by an
    /// ancestor process, so that every process taking part in the same logical run publishes into it
    /// instead of creating one of its own.
    /// </summary>
    /// <remarks>
    /// Set by <see cref="AzureDevOpsTestRunOrchestratorLifetime"/> before the orchestrated test hosts are
    /// launched, and inherited by them. This is what makes <c>--publish-azdo-test-results</c> produce a
    /// single run when combined with <c>--retry-failed-tests</c>: the run outlives every attempt, so the
    /// attempts must neither create nor complete it.
    /// <para>
    /// The value is <c>&lt;buildId&gt;:&lt;runId&gt;</c>. The build id is what makes a stale value safe:
    /// an agent that reuses its environment, or a long-lived process that inherited the variable from an
    /// earlier build, would otherwise redirect this build's results into an unrelated (and probably
    /// already completed) run, which Azure DevOps then rejects.
    /// </para>
    /// <para>
    /// Deliberately carries nothing else. The rest of the publish configuration is derived independently
    /// by each process from the same Azure Pipelines variables, and the access token must never be
    /// propagated through a channel that is easier to observe than the agent's own environment.
    /// </para>
    /// </remarks>
    public const string TestRunIdEnvironmentVariableName = "TESTINGPLATFORM_AZUREDEVOPS_TESTRUNID";

    /// <summary>
    /// Formats the handoff value that tells descendant processes which run to publish into.
    /// </summary>
    public static string FormatInheritedTestRunId(int buildId, int runId)
        => $"{buildId.ToString(CultureInfo.InvariantCulture)}:{runId.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Returns the Azure DevOps test run id established by an ancestor process for <paramref name="buildId"/>,
    /// or <see langword="null"/> when the current process is the one that owns the run's lifetime.
    /// </summary>
    /// <remarks>
    /// A value that is malformed, non-positive, or belongs to a different build is treated as absent: the
    /// variable is an internal handoff, so anything we cannot make sense of means the handoff did not
    /// happen for this build, and falling back to the normal (self-owned) path keeps publishing working
    /// rather than sending results into somebody else's run.
    /// </remarks>
    public static int? TryGetInheritedTestRunId(IEnvironment environment, int buildId)
    {
        string? value = environment.GetEnvironmentVariable(TestRunIdEnvironmentVariableName);
        if (value is null || value.Length == 0)
        {
            return null;
        }

        int separatorIndex = value.IndexOf(':');
        return separatorIndex <= 0
            ? null
            : int.TryParse(value[..separatorIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out int inheritedBuildId)
                && inheritedBuildId == buildId
                && int.TryParse(value[(separatorIndex + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int runId)
                && runId > 0
                    ? runId
                    : null;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the current process is running inside an
    /// Azure DevOps build/release agent (i.e. when the <c>TF_BUILD</c> environment
    /// variable is set to <c>true</c>). The comparison is case-insensitive so the
    /// value set by Azure DevOps (<c>True</c>) and any other casing variant
    /// (<c>true</c>, <c>TRUE</c>, ...) are all treated as enabled.
    /// </summary>
    public static bool IsRunningInAzureDevOps(IEnvironment environment)
        => string.Equals(
            environment.GetEnvironmentVariable(TfBuildEnvironmentVariableName),
            TfBuildEnabledValue,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns whether an individual <c>on|off</c> feature knob is enabled. A knob is enabled unless it
    /// is explicitly set to <c>off</c> (case-insensitive), so every feature defaults to <c>on</c> once
    /// the extension itself is active.
    /// </summary>
    public static bool IsFeatureKnobEnabled(ICommandLineOptions commandLineOptions, string knobOptionName)
        => !(commandLineOptions.TryGetOptionArgumentList(knobOptionName, out string[]? arguments)
            && arguments is [string value]
            && string.Equals(value, AzureDevOpsCommandLineOptions.OptionOff, StringComparison.OrdinalIgnoreCase));
}
