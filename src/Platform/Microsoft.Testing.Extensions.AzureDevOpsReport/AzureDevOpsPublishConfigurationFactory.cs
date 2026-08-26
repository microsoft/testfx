// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

/// <summary>
/// Builds the Azure DevOps live-publishing configuration from the Azure Pipelines environment.
/// </summary>
/// <remarks>
/// Shared by the test-host publisher and by <see cref="AzureDevOpsTestRunOrchestratorLifetime"/>, which
/// runs in the orchestrator process and therefore has no test session to derive it from. Both must agree
/// on the collection, project and build being targeted, so the derivation lives in one place.
/// </remarks>
internal static class AzureDevOpsPublishConfigurationFactory
{
    public static bool TryCreate(
        ICommandLineOptions commandLineOptions,
        IConfiguration configuration,
        IEnvironment environment,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        [NotNullWhen(true)] out AzureDevOpsPublishConfiguration? publishConfiguration,
        [NotNullWhen(false)] out string? warning)
    {
        publishConfiguration = null;
        warning = null;

        List<string> missingVariables = [];

        bool isTfBuild = AzureDevOpsConstants.IsRunningInAzureDevOps(environment);
        if (!isTfBuild)
        {
            missingVariables.Add($"{AzureDevOpsConstants.TfBuildEnvironmentVariableName}={AzureDevOpsConstants.TfBuildEnabledValue}");
        }

        string? collectionUri = GetRequiredEnvironmentVariable(environment, "SYSTEM_COLLECTIONURI", missingVariables);
        string? project = GetRequiredEnvironmentVariable(environment, "SYSTEM_TEAMPROJECT", missingVariables);
        string? accessToken = GetRequiredEnvironmentVariable(environment, "SYSTEM_ACCESSTOKEN", missingVariables);
        string? buildIdText = GetRequiredEnvironmentVariable(environment, "BUILD_BUILDID", missingVariables);

        if (missingVariables.Count > 0)
        {
            warning = string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingMissingConfiguration, string.Join(", ", missingVariables));
            return false;
        }

        if (!int.TryParse(buildIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int buildId))
        {
            warning = string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingMissingConfiguration, "BUILD_BUILDID");
            return false;
        }

        string currentTestApplicationPath = testApplicationModuleInfo.GetCurrentTestApplicationFullPath();
        string assemblyName = testApplicationModuleInfo.TryGetAssemblyName() ?? Path.GetFileNameWithoutExtension(currentTestApplicationPath);
        string automatedTestStorage = Path.GetFileName(currentTestApplicationPath).ToLowerInvariant();
        string targetFrameworkMoniker = TargetFrameworkMonikerHelper.GetTargetFrameworkMonikerIncludingPlatform();
        string agentName = environment.GetEnvironmentVariable("AGENT_NAME") ?? environment.MachineName;
        string? stageName = environment.GetEnvironmentVariable("SYSTEM_STAGENAME");
        string? jobName = environment.GetEnvironmentVariable("SYSTEM_JOBNAME");
        string runName = GetRunName(assemblyName, targetFrameworkMoniker, agentName, stageName, jobName);
        string resultsDirectory = configuration.GetTestResultDirectory();

        if (commandLineOptions.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.PublishAzureDevOpsRunNameOptionName, out string[]? arguments) && arguments is [string configuredRunName])
        {
            runName = configuredRunName;
        }

        publishConfiguration = new AzureDevOpsPublishConfiguration(collectionUri!, project!, accessToken!, buildId, runName, automatedTestStorage, resultsDirectory)
        {
            PipelineReference = CreatePipelineReference(environment, stageName, jobName),
        };
        return true;
    }

    public static string BuildTestRunUrl(AzureDevOpsPublishConfiguration configuration, int runId)
        => $"{configuration.CollectionUri.TrimEnd('/')}/{Uri.EscapeDataString(configuration.Project)}/_TestManagement/Runs?runId={runId}&_a=resultQuery";

    private static AzureDevOpsPipelineReference? CreatePipelineReference(IEnvironment environment, string? stageName, string? jobName)
    {
        string? phaseName = environment.GetEnvironmentVariable("SYSTEM_PHASENAME");
        int? stageAttempt = GetOptionalEnvironmentVariableAsInt32(environment, "SYSTEM_STAGEATTEMPT");
        int? phaseAttempt = GetOptionalEnvironmentVariableAsInt32(environment, "SYSTEM_PHASEATTEMPT");
        int? jobAttempt = GetOptionalEnvironmentVariableAsInt32(environment, "SYSTEM_JOBATTEMPT");

        // Azure DevOps rejects a reference that names nothing, and a run without any stage/job context is
        // better linked to the build alone than to an empty reference.
        return RoslynString.IsNullOrWhiteSpace(stageName) && RoslynString.IsNullOrWhiteSpace(phaseName) && RoslynString.IsNullOrWhiteSpace(jobName)
            ? null
            : new AzureDevOpsPipelineReference(
                NullIfWhiteSpace(stageName),
                stageAttempt,
                NullIfWhiteSpace(phaseName),
                phaseAttempt,
                NullIfWhiteSpace(jobName),
                jobAttempt);
    }

    private static int? GetOptionalEnvironmentVariableAsInt32(IEnvironment environment, string variableName)
        => int.TryParse(environment.GetEnvironmentVariable(variableName), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : null;

    private static string? NullIfWhiteSpace(string? value)
        => RoslynString.IsNullOrWhiteSpace(value) ? null : value;

    private static string? GetRequiredEnvironmentVariable(IEnvironment environment, string variableName, List<string> missingVariables)
    {
        string? value = environment.GetEnvironmentVariable(variableName);
        if (RoslynString.IsNullOrWhiteSpace(value))
        {
            missingVariables.Add(variableName);
        }

        return value;
    }

    private static string GetRunName(string assemblyName, string targetFrameworkMoniker, string agentName, string? stageName, string? jobName)
    {
        string runName = $"{assemblyName} ({targetFrameworkMoniker}) on {agentName}";
        string? stageJob = (SanitizeRunNameComponent(stageName), SanitizeRunNameComponent(jobName)) switch
        {
            ({ Length: > 0 } stage, { Length: > 0 } job) => $"{stage}/{job}",
            ({ Length: > 0 } stage, _) => stage,
            (_, { Length: > 0 } job) => job,
            _ => null,
        };

        string candidateRunName = stageJob is null ? runName : $"{runName} [{stageJob}]";
        return candidateRunName.Length <= AzureDevOpsLivePublishingConstants.MaxRunNameLength
            ? candidateRunName
            : candidateRunName[..AzureDevOpsLivePublishingConstants.MaxRunNameLength];
    }

    private static string? SanitizeRunNameComponent(string? value)
    {
        if (RoslynString.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        char[] buffer = value.ToCharArray();
        for (int index = 0; index < buffer.Length; index++)
        {
            char current = buffer[index];
            if (current is '/' or '\\' or '\r' or '\n' || char.IsControl(current))
            {
                buffer[index] = '_';
            }
        }

        return new string(buffer);
    }
}
