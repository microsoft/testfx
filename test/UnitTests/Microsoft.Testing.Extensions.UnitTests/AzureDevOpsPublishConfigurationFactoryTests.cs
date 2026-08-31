// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

/// <summary>
/// Covers <see cref="AzureDevOpsPublishConfigurationFactory"/>, which had no direct unit test coverage:
/// existing tests only exercised it indirectly through <c>AzureDevOpsTestResultsPublisher</c>, which never
/// hit the missing-variable aggregation, the non-numeric build id branch, the run-name override, or
/// <see cref="AzureDevOpsPublishConfigurationFactory.BuildTestRunUrl"/>.
/// </summary>
[TestClass]
public sealed class AzureDevOpsPublishConfigurationFactoryTests
{
    private const string TestApplicationPath = "/repo/artifacts/MyTests.dll";

    [TestMethod]
    public void TryCreate_NotRunningInAzureDevOps_ReportsAllMissingVariables()
    {
        Mock<IEnvironment> environment = CreateEnvironmentMock();
        environment.Setup(x => x.GetEnvironmentVariable("TF_BUILD")).Returns((string?)null);
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_COLLECTIONURI")).Returns((string?)null);
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_TEAMPROJECT")).Returns((string?)null);

        bool result = AzureDevOpsPublishConfigurationFactory.TryCreate(
            CreateCommandLineOptionsMock().Object,
            CreateConfigurationMock().Object,
            environment.Object,
            CreateTestApplicationModuleInfoMock().Object,
            out AzureDevOpsPublishConfiguration? configuration,
            out string? warning);

        Assert.IsFalse(result);
        Assert.IsNull(configuration);
        Assert.IsNotNull(warning);
        string expected = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            AzureDevOpsResources.AzureDevOpsLivePublishingMissingConfiguration,
            "TF_BUILD=true, SYSTEM_COLLECTIONURI, SYSTEM_TEAMPROJECT");
        Assert.AreEqual(expected, warning);
    }

    [TestMethod]
    public void TryCreate_NonNumericBuildId_ReturnsFalseWithBuildIdWarning()
    {
        Mock<IEnvironment> environment = CreateEnvironmentMock();
        environment.Setup(x => x.GetEnvironmentVariable("BUILD_BUILDID")).Returns("not-a-number");

        bool result = AzureDevOpsPublishConfigurationFactory.TryCreate(
            CreateCommandLineOptionsMock().Object,
            CreateConfigurationMock().Object,
            environment.Object,
            CreateTestApplicationModuleInfoMock().Object,
            out AzureDevOpsPublishConfiguration? configuration,
            out string? warning);

        Assert.IsFalse(result);
        Assert.IsNull(configuration);
        string expected = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            AzureDevOpsResources.AzureDevOpsLivePublishingMissingConfiguration,
            "BUILD_BUILDID");
        Assert.AreEqual(expected, warning);
    }

    [TestMethod]
    public void TryCreate_AllVariablesPresent_ReturnsTrueWithPopulatedConfiguration()
    {
        Mock<IEnvironment> environment = CreateEnvironmentMock();

        bool result = AzureDevOpsPublishConfigurationFactory.TryCreate(
            CreateCommandLineOptionsMock().Object,
            CreateConfigurationMock().Object,
            environment.Object,
            CreateTestApplicationModuleInfoMock().Object,
            out AzureDevOpsPublishConfiguration? configuration,
            out string? warning);

        Assert.IsTrue(result);
        Assert.IsNull(warning);
        Assert.IsNotNull(configuration);
        Assert.AreEqual("https://dev.azure.com/org/", configuration!.CollectionUri);
        Assert.AreEqual("project", configuration.Project);
        Assert.AreEqual("token", configuration.AccessToken);
        Assert.AreEqual(123, configuration.BuildId);
        Assert.AreEqual("mytests.dll", configuration.AutomatedTestStorage);
        Assert.AreEqual("results-dir", configuration.ResultsDirectory);
        Assert.Contains("MyTests", configuration.RunName);
        Assert.Contains("agent-name", configuration.RunName);
    }

    [TestMethod]
    public void TryCreate_AssemblyNameUnavailable_FallsBackToFileName()
    {
        Mock<ITestApplicationModuleInfo> testApplicationModuleInfo = new();
        testApplicationModuleInfo.Setup(x => x.TryGetAssemblyName()).Returns((string?)null);
        testApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns(TestApplicationPath);

        bool result = AzureDevOpsPublishConfigurationFactory.TryCreate(
            CreateCommandLineOptionsMock().Object,
            CreateConfigurationMock().Object,
            CreateEnvironmentMock().Object,
            testApplicationModuleInfo.Object,
            out AzureDevOpsPublishConfiguration? configuration,
            out _);

        Assert.IsTrue(result);
        Assert.Contains("MyTests", configuration!.RunName);
    }

    [TestMethod]
    public void TryCreate_RunNameOptionProvided_OverridesDerivedRunName()
    {
        Mock<ICommandLineOptions> commandLineOptions = new();
        string[]? runNameArguments = ["custom-run-name"];
        commandLineOptions.Setup(x => x.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.PublishAzureDevOpsRunNameOptionName, out runNameArguments)).Returns(true);

        bool result = AzureDevOpsPublishConfigurationFactory.TryCreate(
            commandLineOptions.Object,
            CreateConfigurationMock().Object,
            CreateEnvironmentMock().Object,
            CreateTestApplicationModuleInfoMock().Object,
            out AzureDevOpsPublishConfiguration? configuration,
            out _);

        Assert.IsTrue(result);
        Assert.AreEqual("custom-run-name", configuration!.RunName);
    }

    [TestMethod]
    public void TryCreate_RunNameOptionWithMultipleArguments_IsIgnored()
    {
        Mock<ICommandLineOptions> commandLineOptions = new();
        string[]? runNameArguments = ["one", "two"];
        commandLineOptions.Setup(x => x.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.PublishAzureDevOpsRunNameOptionName, out runNameArguments)).Returns(true);

        bool result = AzureDevOpsPublishConfigurationFactory.TryCreate(
            commandLineOptions.Object,
            CreateConfigurationMock().Object,
            CreateEnvironmentMock().Object,
            CreateTestApplicationModuleInfoMock().Object,
            out AzureDevOpsPublishConfiguration? configuration,
            out _);

        Assert.IsTrue(result);
        Assert.AreNotEqual("one", configuration!.RunName);
        Assert.Contains("MyTests", configuration.RunName);
    }

    [TestMethod]
    public void TryCreate_LongStageAndJobNames_TruncatesRunNameToMaxLength()
    {
        Mock<IEnvironment> environment = CreateEnvironmentMock(
            stageName: new string('s', 300),
            jobName: new string('j', 300));

        bool result = AzureDevOpsPublishConfigurationFactory.TryCreate(
            CreateCommandLineOptionsMock().Object,
            CreateConfigurationMock().Object,
            environment.Object,
            CreateTestApplicationModuleInfoMock().Object,
            out AzureDevOpsPublishConfiguration? configuration,
            out _);

        Assert.IsTrue(result);
        Assert.AreEqual(256, configuration!.RunName.Length);
    }

    [TestMethod]
    public void TryCreate_StageAndJobNamesContainUnsafeCharacters_AreSanitized()
    {
        Mock<IEnvironment> environment = CreateEnvironmentMock(
            stageName: "stage/with\\slashes",
            jobName: "job\r\nwith\u0001control");

        bool result = AzureDevOpsPublishConfigurationFactory.TryCreate(
            CreateCommandLineOptionsMock().Object,
            CreateConfigurationMock().Object,
            environment.Object,
            CreateTestApplicationModuleInfoMock().Object,
            out AzureDevOpsPublishConfiguration? configuration,
            out _);

        Assert.IsTrue(result);

        // The factory joins sanitized stage/job components with a literal "/" separator, so a single "/" is
        // expected in the output. What must NOT survive is the "\", "\r", "\n", and control characters that were
        // embedded inside the original stage/job names, nor the original unsanitized "/" from the stage name.
        Assert.Contains("stage_with_slashes/job__with_control", configuration!.RunName);
        Assert.DoesNotContain("\\", configuration.RunName);
        Assert.DoesNotContain("\r", configuration.RunName);
        Assert.DoesNotContain("\n", configuration.RunName);
        Assert.DoesNotContain("\u0001", configuration.RunName);
    }

    [TestMethod]
    public void TryCreate_NoStagePhaseOrJob_LeavesPipelineReferenceNull()
    {
        Mock<IEnvironment> environment = CreateEnvironmentMock(stageName: null, jobName: null);

        bool result = AzureDevOpsPublishConfigurationFactory.TryCreate(
            CreateCommandLineOptionsMock().Object,
            CreateConfigurationMock().Object,
            environment.Object,
            CreateTestApplicationModuleInfoMock().Object,
            out AzureDevOpsPublishConfiguration? configuration,
            out _);

        Assert.IsTrue(result);
        Assert.IsNull(configuration!.PipelineReference);
    }

    [TestMethod]
    public void TryCreate_OnlyPhasePresent_PopulatesPipelineReferenceWithoutStageOrJob()
    {
        Mock<IEnvironment> environment = CreateEnvironmentMock(stageName: null, jobName: null, phaseName: "RunTests");

        bool result = AzureDevOpsPublishConfigurationFactory.TryCreate(
            CreateCommandLineOptionsMock().Object,
            CreateConfigurationMock().Object,
            environment.Object,
            CreateTestApplicationModuleInfoMock().Object,
            out AzureDevOpsPublishConfiguration? configuration,
            out _);

        Assert.IsTrue(result);
        Assert.IsNotNull(configuration!.PipelineReference);
        Assert.AreEqual("RunTests", configuration.PipelineReference!.PhaseName);
        Assert.IsNull(configuration.PipelineReference.StageName);
        Assert.IsNull(configuration.PipelineReference.JobName);
    }

    [TestMethod]
    public void BuildTestRunUrl_TrimsTrailingSlashAndEscapesProject()
    {
        AzureDevOpsPublishConfiguration configuration = new(
            "https://dev.azure.com/org/",
            "my project/with spaces",
            "token",
            123,
            "run-name",
            "storage.dll",
            "results-dir");

        string url = AzureDevOpsPublishConfigurationFactory.BuildTestRunUrl(configuration, 42);

        Assert.AreEqual("https://dev.azure.com/org/my%20project%2Fwith%20spaces/_TestManagement/Runs?runId=42&_a=resultQuery", url);
    }

    private static Mock<IEnvironment> CreateEnvironmentMock(
        string? stageName = "stage",
        string? jobName = "job",
        string? phaseName = null,
        string? stageAttempt = null,
        string? phaseAttempt = null,
        string? jobAttempt = null)
    {
        Mock<IEnvironment> environment = new();
        environment.SetupGet(x => x.MachineName).Returns("agent-name");
        environment.Setup(x => x.GetEnvironmentVariable("TF_BUILD")).Returns("true");
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_COLLECTIONURI")).Returns("https://dev.azure.com/org/");
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_TEAMPROJECT")).Returns("project");
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN")).Returns("token");
        environment.Setup(x => x.GetEnvironmentVariable("BUILD_BUILDID")).Returns("123");
        environment.Setup(x => x.GetEnvironmentVariable("AGENT_NAME")).Returns("agent-name");
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_STAGENAME")).Returns(stageName);
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_JOBNAME")).Returns(jobName);
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_PHASENAME")).Returns(phaseName);
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_STAGEATTEMPT")).Returns(stageAttempt);
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_PHASEATTEMPT")).Returns(phaseAttempt);
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_JOBATTEMPT")).Returns(jobAttempt);
        return environment;
    }

    private static Mock<ICommandLineOptions> CreateCommandLineOptionsMock()
    {
        Mock<ICommandLineOptions> commandLineOptions = new();
        string[]? runNameArguments = null;
        commandLineOptions.Setup(x => x.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.PublishAzureDevOpsRunNameOptionName, out runNameArguments)).Returns(false);
        return commandLineOptions;
    }

    private static Mock<IConfiguration> CreateConfigurationMock()
    {
        Mock<IConfiguration> configuration = new();
        configuration.Setup(x => x[PlatformConfigurationConstants.PlatformResultDirectory]).Returns("results-dir");
        return configuration;
    }

    private static Mock<ITestApplicationModuleInfo> CreateTestApplicationModuleInfoMock()
    {
        Mock<ITestApplicationModuleInfo> testApplicationModuleInfo = new();
        testApplicationModuleInfo.Setup(x => x.TryGetAssemblyName()).Returns("MyTests");
        testApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns(TestApplicationPath);
        return testApplicationModuleInfo;
    }
}
