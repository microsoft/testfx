// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.InteropServices;

using Microsoft.Testing.Extensions.OpenTelemetry;

namespace Microsoft.Testing.Extensions.UnitTests;

/// <summary>
/// Direct tests for the internal <see cref="TestingPlatformResourceDetector"/> that backs the stable
/// <c>AddTestingPlatformResource()</c> helper. These tests mutate process-global environment variables (the CI
/// markers and <c>OTEL_SERVICE_NAME</c>), so the class is <see cref="DoNotParallelizeAttribute"/> and every method
/// runs against a snapshot that is neutralised first and restored afterwards, otherwise the ambient CI environment
/// this suite itself runs in would leak into the assertions.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class TestingPlatformResourceDetectorTests
{
    // Every environment variable the detector reads. They are all cleared before a test body runs so that the CI
    // environment hosting this test run cannot make a "no CI provider" assertion fail, and restored afterwards.
    private static readonly string[] ObservedEnvironmentVariables =
    [
        "OTEL_SERVICE_NAME",
        "GITHUB_ACTIONS", "GITHUB_WORKFLOW", "GITHUB_RUN_ID", "GITHUB_JOB", "GITHUB_REF_NAME", "GITHUB_SHA", "GITHUB_REPOSITORY",
        "TF_BUILD", "BUILD_DEFINITIONNAME", "BUILD_BUILDID", "SYSTEM_JOBID", "BUILD_SOURCEBRANCHNAME", "BUILD_SOURCEVERSION", "BUILD_REPOSITORY_URI",
        "GITLAB_CI", "CI_PIPELINE_NAME", "CI_PIPELINE_ID", "CI_JOB_ID", "CI_COMMIT_REF_NAME", "CI_COMMIT_SHA", "CI_REPOSITORY_URL",
        "JENKINS_URL", "JOB_NAME", "BUILD_NUMBER", "GIT_BRANCH", "GIT_COMMIT", "GIT_URL",
    ];

    [TestMethod]
    public void GetServiceName_WithOtelServiceNameSet_ReturnsThatValue()
        => WithEnvironment(
            new() { ["OTEL_SERVICE_NAME"] = "my-custom-service" },
            () => Assert.AreEqual("my-custom-service", TestingPlatformResourceDetector.GetServiceName()));

    [TestMethod]
    public void GetServiceName_WithBlankOtelServiceName_FallsBackToEntryAssemblyName()
        => WithEnvironment(
            new() { ["OTEL_SERVICE_NAME"] = "   " },
            () =>
            {
                string? expected = Assembly.GetEntryAssembly()?.GetName().Name;
                Assert.IsNotNull(expected);
                Assert.AreEqual(expected, TestingPlatformResourceDetector.GetServiceName());
            });

    [TestMethod]
    public void GetResourceAttributes_AlwaysIncludeProcessAndHostAttributes()
        => WithEnvironment(
            [],
            () =>
            {
                Dictionary<string, object> attributes = GetResourceAttributeMap();

                Assert.AreEqual(Environment.MachineName, attributes["host.name"]);
                Assert.AreEqual(".NET", attributes["process.runtime.name"]);
                Assert.AreEqual(RuntimeInformation.FrameworkDescription, attributes["process.runtime.description"]);
                Assert.AreEqual(RuntimeInformation.OSDescription, attributes["os.description"]);
                Assert.IsTrue(attributes.ContainsKey("host.arch"));
                Assert.IsTrue(attributes.ContainsKey("process.pid"));
            });

    [TestMethod]
    public void GetResourceAttributes_MapsHostArchitectureToOpenTelemetrySpelling()
        => WithEnvironment(
            [],
            () =>
            {
                Dictionary<string, object> attributes = GetResourceAttributeMap();

                string expectedArch = RuntimeInformation.OSArchitecture switch
                {
                    Architecture.X64 => "amd64",
                    Architecture.X86 => "x86",
                    Architecture.Arm => "arm32",
                    Architecture.Arm64 => "arm64",
                    _ => RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
                };

                Assert.AreEqual(expectedArch, attributes["host.arch"]);
            });

    [TestMethod]
    public void GetResourceAttributes_WithNoCiProvider_DoesNotEmitCiProviderName()
        => WithEnvironment(
            [],
            () => Assert.IsFalse(GetResourceAttributeMap().ContainsKey("cicd.provider.name")));

    [TestMethod]
    public void GetResourceAttributes_ForGitHubActions_EmitsGitHubCiAttributes()
        => WithEnvironment(
            new()
            {
                ["GITHUB_ACTIONS"] = "true",
                ["GITHUB_WORKFLOW"] = "CI",
                ["GITHUB_RUN_ID"] = "42",
                ["GITHUB_JOB"] = "build",
                ["GITHUB_REF_NAME"] = "main",
                ["GITHUB_SHA"] = "abc123",
                ["GITHUB_REPOSITORY"] = "microsoft/testfx",
            },
            () =>
            {
                Dictionary<string, object> attributes = GetResourceAttributeMap();

                Assert.AreEqual("github_actions", attributes["cicd.provider.name"]);
                Assert.AreEqual("CI", attributes["cicd.pipeline.name"]);
                Assert.AreEqual("42", attributes["cicd.pipeline.run.id"]);
                Assert.AreEqual("build", attributes["cicd.pipeline.task.name"]);
                Assert.AreEqual("main", attributes["vcs.ref.head.name"]);
                Assert.AreEqual("abc123", attributes["vcs.ref.head.revision"]);
                Assert.AreEqual("microsoft/testfx", attributes["vcs.repository.name"]);
            });

    [TestMethod]
    public void GetResourceAttributes_ForAzurePipelines_EmitsAzureCiAttributes()
        => WithEnvironment(
            new()
            {
                ["TF_BUILD"] = "true",
                ["BUILD_DEFINITIONNAME"] = "testfx-ci",
                ["BUILD_BUILDID"] = "7",
                ["SYSTEM_JOBID"] = "job-guid",
                ["BUILD_SOURCEBRANCHNAME"] = "main",
                ["BUILD_SOURCEVERSION"] = "deadbeef",
                ["BUILD_REPOSITORY_URI"] = "https://dev.azure.com/org/_git/repo",
            },
            () =>
            {
                Dictionary<string, object> attributes = GetResourceAttributeMap();

                Assert.AreEqual("azure_pipelines", attributes["cicd.provider.name"]);
                Assert.AreEqual("testfx-ci", attributes["cicd.pipeline.name"]);
                Assert.AreEqual("7", attributes["cicd.pipeline.run.id"]);
                Assert.AreEqual("job-guid", attributes["cicd.pipeline.task.run.id"]);
                Assert.AreEqual("main", attributes["vcs.ref.head.name"]);
                Assert.AreEqual("deadbeef", attributes["vcs.ref.head.revision"]);
                Assert.AreEqual("https://dev.azure.com/org/_git/repo", attributes["vcs.repository.url.full"]);
            });

    [TestMethod]
    public void GetResourceAttributes_ForGitLab_EmitsGitLabCiAttributes()
        => WithEnvironment(
            new()
            {
                ["GITLAB_CI"] = "true",
                ["CI_PIPELINE_NAME"] = "pipeline",
                ["CI_PIPELINE_ID"] = "9",
                ["CI_JOB_ID"] = "13",
                ["CI_COMMIT_REF_NAME"] = "feature",
                ["CI_COMMIT_SHA"] = "cafe",
                ["CI_REPOSITORY_URL"] = "https://gitlab.example.com/group/project.git",
            },
            () =>
            {
                Dictionary<string, object> attributes = GetResourceAttributeMap();

                Assert.AreEqual("gitlab", attributes["cicd.provider.name"]);
                Assert.AreEqual("pipeline", attributes["cicd.pipeline.name"]);
                Assert.AreEqual("9", attributes["cicd.pipeline.run.id"]);
                Assert.AreEqual("13", attributes["cicd.pipeline.task.run.id"]);
                Assert.AreEqual("feature", attributes["vcs.ref.head.name"]);
                Assert.AreEqual("cafe", attributes["vcs.ref.head.revision"]);
                Assert.AreEqual("https://gitlab.example.com/group/project.git", attributes["vcs.repository.url.full"]);
            });

    [TestMethod]
    public void GetResourceAttributes_ForJenkins_EmitsJenkinsCiAttributes()
        => WithEnvironment(
            new()
            {
                ["JENKINS_URL"] = "https://jenkins.example.com/",
                ["JOB_NAME"] = "nightly",
                ["BUILD_NUMBER"] = "128",
                ["GIT_BRANCH"] = "origin/main",
                ["GIT_COMMIT"] = "1234abcd",
                ["GIT_URL"] = "https://github.com/microsoft/testfx.git",
            },
            () =>
            {
                Dictionary<string, object> attributes = GetResourceAttributeMap();

                Assert.AreEqual("jenkins", attributes["cicd.provider.name"]);
                Assert.AreEqual("nightly", attributes["cicd.pipeline.name"]);
                Assert.AreEqual("128", attributes["cicd.pipeline.run.id"]);
                Assert.AreEqual("origin/main", attributes["vcs.ref.head.name"]);
                Assert.AreEqual("1234abcd", attributes["vcs.ref.head.revision"]);
                Assert.AreEqual("https://github.com/microsoft/testfx.git", attributes["vcs.repository.url.full"]);
            });

    [TestMethod]
    public void GetResourceAttributes_StripsCredentialsFromRepositoryUrl()
        => WithEnvironment(
            new()
            {
                ["TF_BUILD"] = "true",
                ["BUILD_REPOSITORY_URI"] = "https://user:s3cr3t-token@dev.azure.com/org/_git/repo",
            },
            () => Assert.AreEqual(
                "https://dev.azure.com/org/_git/repo",
                GetResourceAttributeMap()["vcs.repository.url.full"]));

    [TestMethod]
    public void GetResourceAttributes_WhenGitHubAndAzureBothSet_PrefersGitHubActions()
        => WithEnvironment(
            new()
            {
                ["GITHUB_ACTIONS"] = "true",
                ["TF_BUILD"] = "true",
                // Azure-only marker: vcs.repository.url.full is emitted by the Azure branch but never by the
                // GitHub branch, so its absence proves the GitHub branch won and yielded before Azure could run.
                ["BUILD_REPOSITORY_URI"] = "https://dev.azure.com/org/_git/repo",
            },
            () =>
            {
                Dictionary<string, object> attributes = GetResourceAttributeMap();

                Assert.AreEqual("github_actions", attributes["cicd.provider.name"]);
                Assert.IsFalse(attributes.ContainsKey("vcs.repository.url.full"));
            });

    private static Dictionary<string, object> GetResourceAttributeMap()
    {
        Dictionary<string, object> map = [];
        foreach (KeyValuePair<string, object> attribute in TestingPlatformResourceDetector.GetResourceAttributes())
        {
            map[attribute.Key] = attribute.Value;
        }

        return map;
    }

    private static void WithEnvironment(Dictionary<string, string?> values, Action body)
    {
        Dictionary<string, string?> snapshot = [];
        foreach (string name in ObservedEnvironmentVariables)
        {
            snapshot[name] = Environment.GetEnvironmentVariable(name);
            // Neutralise every observed variable first so the ambient environment cannot leak into the assertions.
            Environment.SetEnvironmentVariable(name, null);
        }

        try
        {
            foreach (KeyValuePair<string, string?> value in values)
            {
                Environment.SetEnvironmentVariable(value.Key, value.Value);
            }

            body();
        }
        finally
        {
            foreach (KeyValuePair<string, string?> entry in snapshot)
            {
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }
        }
    }
}
