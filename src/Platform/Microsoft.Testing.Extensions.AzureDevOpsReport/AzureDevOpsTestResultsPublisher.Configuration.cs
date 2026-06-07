// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Configurations;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsTestResultsPublisher
{
    private bool TryCreatePublishConfiguration(out AzureDevOpsPublishConfiguration? publishConfiguration, out string? warning)
    {
        publishConfiguration = null;
        warning = null;

        List<string> missingVariables = [];

        bool isTfBuild = string.Equals(_environment.GetEnvironmentVariable("TF_BUILD"), "true", StringComparison.OrdinalIgnoreCase);
        if (!isTfBuild)
        {
            missingVariables.Add("TF_BUILD=true");
        }

        string? collectionUri = GetRequiredEnvironmentVariable("SYSTEM_COLLECTIONURI", missingVariables);
        string? project = GetRequiredEnvironmentVariable("SYSTEM_TEAMPROJECT", missingVariables);
        string? accessToken = GetRequiredEnvironmentVariable("SYSTEM_ACCESSTOKEN", missingVariables);
        string? buildIdText = GetRequiredEnvironmentVariable("BUILD_BUILDID", missingVariables);

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

        string currentTestApplicationPath = _testApplicationModuleInfo.GetCurrentTestApplicationFullPath();
        string assemblyName = _testApplicationModuleInfo.TryGetAssemblyName() ?? Path.GetFileNameWithoutExtension(currentTestApplicationPath);
        string automatedTestStorage = Path.GetFileNameWithoutExtension(currentTestApplicationPath);
        string targetFrameworkMoniker = global::Microsoft.Testing.Extensions.TargetFrameworkMonikerHelper.GetTargetFrameworkMoniker();
        string agentName = _environment.GetEnvironmentVariable("AGENT_NAME") ?? _environment.MachineName;
        string? stageName = _environment.GetEnvironmentVariable("SYSTEM_STAGENAME");
        string? jobName = _environment.GetEnvironmentVariable("SYSTEM_JOBNAME");
        string runName = GetRunName(assemblyName, targetFrameworkMoniker, agentName, stageName, jobName);
        string resultsDirectory = _configuration.GetTestResultDirectory();

        if (_commandLineOptions.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.PublishAzureDevOpsRunNameOptionName, out string[]? arguments) && arguments is [string configuredRunName])
        {
            runName = configuredRunName;
        }

        publishConfiguration = new AzureDevOpsPublishConfiguration(collectionUri!, project!, accessToken!, buildId, runName, automatedTestStorage, resultsDirectory);
        return true;
    }

    private string? GetRequiredEnvironmentVariable(string variableName, List<string> missingVariables)
    {
        string? value = _environment.GetEnvironmentVariable(variableName);
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
