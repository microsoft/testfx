// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Provides CI environment detection capabilities.
/// </summary>
/// <remarks>
/// Based on https://learn.microsoft.com/dotnet/core/tools/telemetry#continuous-integration-detection
/// and https://github.com/dotnet/sdk/blob/main/src/Cli/dotnet/Telemetry/CIEnvironmentDetectorForTelemetry.cs.
/// </remarks>
internal sealed class CIEnvironmentDetector
{
    /// <summary>
    /// Gets the default instance that uses the real environment.
    /// </summary>
    public static CIEnvironmentDetector Instance { get; } = new(EnvironmentWrapper.Instance);

    private readonly IEnvironment _environment;

    // Systems that provide boolean values only, so we can simply parse and check for true
    private static readonly string[] BooleanVariables =
    [
        // Azure Pipelines - https://docs.microsoft.com/azure/devops/pipelines/build/variables#system-variables-devops-services
        "TF_BUILD",

        // GitHub Actions - https://docs.github.com/en/actions/learn-github-actions/environment-variables#default-environment-variables
        "GITHUB_ACTIONS",

        // AppVeyor - https://www.appveyor.com/docs/environment-variables/
        "APPVEYOR",

        // A general-use flag - Many of the major players support this: AzDo, GitHub, GitLab, AppVeyor, Travis CI, CircleCI.
        "CI",

        // Travis CI - https://docs.travis-ci.com/user/environment-variables/#default-environment-variables
        "TRAVIS",

        // CircleCI - https://circleci.com/docs/2.0/env-vars/#built-in-environment-variables
        "CIRCLECI",
    ];

    // Systems where every variable must be present and not-null before returning true
    private static readonly string[][] AllNotNullVariables =
    [
        // AWS CodeBuild - https://docs.aws.amazon.com/codebuild/latest/userguide/build-env-ref-env-vars.html
        ["CODEBUILD_BUILD_ID", "AWS_REGION"],

        // Jenkins - https://github.com/jenkinsci/jenkins/blob/master/core/src/main/resources/jenkins/model/CoreEnvironmentContributor/buildEnv.groovy
        ["BUILD_ID", "BUILD_URL"],

        // Google Cloud Build - https://cloud.google.com/build/docs/configuring-builds/substitute-variable-values#using_default_substitutions
        ["BUILD_ID", "PROJECT_ID"],
    ];

    // Systems where the variable must be present and not-null
    private static readonly string[] IfNonNullVariables =
    [
        // TeamCity - https://www.jetbrains.com/help/teamcity/predefined-build-parameters.html#Predefined+Server+Build+Parameters
        "TEAMCITY_VERSION",

        // JetBrains Space - https://www.jetbrains.com/help/space/automation-environment-variables.html#general
        "JB_SPACE_API_URL",
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="CIEnvironmentDetector"/> class.
    /// </summary>
    /// <param name="environment">The environment abstraction to use for reading environment variables.</param>
    internal /* for testing purposes */ CIEnvironmentDetector(IEnvironment environment) => _environment = environment;

    /// <summary>
    /// Detects if the current environment is a CI environment.
    /// </summary>
    /// <returns><c>true</c> if running in a CI environment; otherwise, <c>false</c>.</returns>
    public bool IsCIEnvironment()
    {
        foreach (string booleanVariable in BooleanVariables)
        {
            if (bool.TryParse(_environment.GetEnvironmentVariable(booleanVariable), out bool envVar) && envVar)
            {
                return true;
            }
        }

        foreach (string[] variables in AllNotNullVariables)
        {
            bool allVariablesPresent = true;
            foreach (string variable in variables)
            {
                if (string.IsNullOrEmpty(_environment.GetEnvironmentVariable(variable)))
                {
                    allVariablesPresent = false;
                    break;
                }
            }

            if (allVariablesPresent)
            {
                return true;
            }
        }

        foreach (string variable in IfNonNullVariables)
        {
            if (!string.IsNullOrEmpty(_environment.GetEnvironmentVariable(variable)))
            {
                return true;
            }
        }

        return false;
    }
}
