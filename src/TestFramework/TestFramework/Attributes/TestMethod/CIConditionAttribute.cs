// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This attribute is used to conditionally control whether a test class or a test method will run or be ignored based on whether the test is running in a CI environment.
/// </summary>
/// <remarks>
/// This attribute isn't inherited. Applying it to a base class will not affect derived classes.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class CIConditionAttribute : ConditionBaseAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CIConditionAttribute"/> class.
    /// </summary>
    /// <param name="mode">Decides whether the test should be included or excluded in CI environments.</param>
    public CIConditionAttribute(ConditionMode mode)
        : base(mode)
    {
        IgnoreMessage = mode == ConditionMode.Include
            ? "Test is only supported in CI environments"
            : "Test is not supported in CI environments";
    }

    /// <summary>
    /// Gets a value indicating whether the test method or test class should run.
    /// </summary>
    public override bool ShouldRun
    {
        get
        {
            bool isCI = IsCIEnvironment();
            return Mode == ConditionMode.Include ? isCI : !isCI;
        }
    }

    /// <summary>
    /// Gets the ignore message (in case <see cref="ShouldRun"/> returns <see langword="false"/>).
    /// </summary>
    public override string? IgnoreMessage { get; }

    /// <summary>
    /// Gets the group name for this attribute.
    /// </summary>
    public override string GroupName => nameof(CIConditionAttribute);

    // CI Detection logic based on https://learn.microsoft.com/dotnet/core/tools/telemetry#continuous-integration-detection
    // From: https://github.com/dotnet/sdk/blob/main/src/Cli/dotnet/Telemetry/CIEnvironmentDetectorForTelemetry.cs
    private static bool IsCIEnvironment()
    {
        // Systems that provide boolean values only, so we can simply parse and check for true
        string[] booleanVariables =
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
            "CIRCLECI"
        ];

        // Systems where every variable must be present and not-null before returning true
        string[][] allNotNullVariables =
        [
            // AWS CodeBuild - https://docs.aws.amazon.com/codebuild/latest/userguide/build-env-ref-env-vars.html
            ["CODEBUILD_BUILD_ID", "AWS_REGION"],

            // Jenkins - https://github.com/jenkinsci/jenkins/blob/master/core/src/main/resources/jenkins/model/CoreEnvironmentContributor/buildEnv.groovy
            ["BUILD_ID", "BUILD_URL"],

            // Google Cloud Build - https://cloud.google.com/build/docs/configuring-builds/substitute-variable-values#using_default_substitutions
            ["BUILD_ID", "PROJECT_ID"],
        ];

        // Systems where the variable must be present and not-null
        string[] ifNonNullVariables =
        [
            // TeamCity - https://www.jetbrains.com/help/teamcity/predefined-build-parameters.html#Predefined+Server+Build+Parameters
            "TEAMCITY_VERSION",

            // JetBrains Space - https://www.jetbrains.com/help/space/automation-environment-variables.html#general
            "JB_SPACE_API_URL"
        ];

        foreach (string booleanVariable in booleanVariables)
        {
            if (bool.TryParse(Environment.GetEnvironmentVariable(booleanVariable), out bool envVar) && envVar)
            {
                return true;
            }
        }

        foreach (string[] variables in allNotNullVariables)
        {
            if (variables.All(variable => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable))))
            {
                return true;
            }
        }

        foreach (string variable in ifNonNullVariables)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable)))
            {
                return true;
            }
        }

        return false;
    }
}