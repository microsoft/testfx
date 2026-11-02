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

[TestClass]
public sealed class AzureDevOpsPublishConfigurationFactoryTests
{
    [TestMethod]
    public void TryCreate_MissingRequiredEnvironmentVariables_ReturnsAggregatedWarning()
    {
        Mock<IEnvironment> environment = CreateEnvironmentMock();
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_COLLECTIONURI")).Returns((string?)null);
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_TEAMPROJECT")).Returns(" ");
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN")).Returns(string.Empty);
        environment.Setup(x => x.GetEnvironmentVariable("BUILD_BUILDID")).Returns((string?)null);

        bool result = TryCreate(environment, out AzureDevOpsPublishConfiguration? publishConfiguration, out string? warning);

        Assert.IsFalse(result);
        Assert.IsNull(publishConfiguration);
        Assert.AreEqual(
            string.Format(
                CultureInfo.InvariantCulture,
                AzureDevOpsResources.AzureDevOpsLivePublishingMissingConfiguration,
                "SYSTEM_COLLECTIONURI, SYSTEM_TEAMPROJECT, SYSTEM_ACCESSTOKEN, BUILD_BUILDID"),
            warning);
    }

    [TestMethod]
    public void TryCreate_NonNumericBuildId_ReturnsWarning()
    {
        Mock<IEnvironment> environment = CreateEnvironmentMock();
        environment.Setup(x => x.GetEnvironmentVariable("BUILD_BUILDID")).Returns("not-a-number");

        bool result = TryCreate(environment, out AzureDevOpsPublishConfiguration? publishConfiguration, out string? warning);

        Assert.IsFalse(result);
        Assert.IsNull(publishConfiguration);
        Assert.AreEqual(
            string.Format(
                CultureInfo.InvariantCulture,
                AzureDevOpsResources.AzureDevOpsLivePublishingMissingConfiguration,
                "BUILD_BUILDID"),
            warning);
    }

    [TestMethod]
    public void TryCreate_ValidEnvironment_UsesAssemblyNameFallbackAndCreatesConfiguration()
    {
        Mock<IEnvironment> environment = CreateEnvironmentMock();

        bool result = TryCreate(
            environment,
            out AzureDevOpsPublishConfiguration? publishConfiguration,
            out string? warning,
            assemblyName: null,
            applicationPath: Path.Combine("artifacts", "Fallback.Tests.dll"));

        Assert.IsTrue(result);
        Assert.IsNull(warning);
        Assert.IsNotNull(publishConfiguration);
        Assert.AreEqual("https://dev.azure.com/example/", publishConfiguration.CollectionUri);
        Assert.AreEqual("My Project", publishConfiguration.Project);
        Assert.AreEqual("access-token", publishConfiguration.AccessToken);
        Assert.AreEqual(123, publishConfiguration.BuildId);
        Assert.StartsWith("Fallback.Tests (", publishConfiguration.RunName);
        Assert.EndsWith(") on agent-name", publishConfiguration.RunName);
        Assert.AreEqual("fallback.tests.dll", publishConfiguration.AutomatedTestStorage);
        Assert.AreEqual("test-results", publishConfiguration.ResultsDirectory);
    }

    [TestMethod]
    public void TryCreate_SingleRunNameArgument_UsesOverride()
    {
        bool result = TryCreate(
            CreateEnvironmentMock(),
            out AzureDevOpsPublishConfiguration? publishConfiguration,
            out string? warning,
            runNameArguments: ["custom run / name"],
            hasRunNameOption: true);

        Assert.IsTrue(result);
        Assert.IsNull(warning);
        Assert.IsNotNull(publishConfiguration);
        Assert.AreEqual("custom run / name", publishConfiguration.RunName);
    }

    [TestMethod]
    public void TryCreate_MultipleRunNameArguments_IgnoresOverride()
    {
        Mock<IEnvironment> environment = CreateEnvironmentMock();
        Assert.IsTrue(TryCreate(environment, out AzureDevOpsPublishConfiguration? defaultConfiguration, out _));

        bool result = TryCreate(
            environment,
            out AzureDevOpsPublishConfiguration? publishConfiguration,
            out string? warning,
            runNameArguments: ["first", "second"],
            hasRunNameOption: true);

        Assert.IsTrue(result);
        Assert.IsNull(warning);
        Assert.IsNotNull(defaultConfiguration);
        Assert.IsNotNull(publishConfiguration);
        Assert.AreEqual(defaultConfiguration.RunName, publishConfiguration.RunName);
    }

    [TestMethod]
    public void TryCreate_LongRunName_TruncatesToMaximumLength()
    {
        string longAssemblyName = new('a', AzureDevOpsLivePublishingConstants.MaxRunNameLength + 50);

        bool result = TryCreate(
            CreateEnvironmentMock(),
            out AzureDevOpsPublishConfiguration? publishConfiguration,
            out string? warning,
            assemblyName: longAssemblyName);

        Assert.IsTrue(result);
        Assert.IsNull(warning);
        Assert.IsNotNull(publishConfiguration);
        Assert.AreEqual(
            new string('a', AzureDevOpsLivePublishingConstants.MaxRunNameLength),
            publishConfiguration.RunName);
    }

    [TestMethod]
    public void TryCreate_UnsafeStageAndJobCharacters_SanitizesRunName()
    {
        Mock<IEnvironment> environment = CreateEnvironmentMock();
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_STAGENAME")).Returns("stage/name\\part");
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_JOBNAME")).Returns("job\r\nline\u0001");

        bool result = TryCreate(environment, out AzureDevOpsPublishConfiguration? publishConfiguration, out string? warning);

        Assert.IsTrue(result);
        Assert.IsNull(warning);
        Assert.IsNotNull(publishConfiguration);
        Assert.EndsWith(" [stage_name_part/job__line_]", publishConfiguration.RunName);
    }

    [TestMethod]
    public void TryCreate_NoStagePhaseOrJob_LeavesPipelineReferenceNull()
    {
        bool result = TryCreate(
            CreateEnvironmentMock(),
            out AzureDevOpsPublishConfiguration? publishConfiguration,
            out string? warning);

        Assert.IsTrue(result);
        Assert.IsNull(warning);
        Assert.IsNotNull(publishConfiguration);
        Assert.IsNull(publishConfiguration.PipelineReference);
    }

    [TestMethod]
    public void TryCreate_PipelineVariables_PopulatesPipelineReference()
    {
        Mock<IEnvironment> environment = CreateEnvironmentMock();
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_STAGENAME")).Returns("Build");
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_STAGEATTEMPT")).Returns("2");
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_PHASENAME")).Returns("RunTests");
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_PHASEATTEMPT")).Returns("3");
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_JOBNAME")).Returns("Windows");
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_JOBATTEMPT")).Returns("4");

        bool result = TryCreate(environment, out AzureDevOpsPublishConfiguration? publishConfiguration, out string? warning);

        Assert.IsTrue(result);
        Assert.IsNull(warning);
        Assert.IsNotNull(publishConfiguration);
        AzureDevOpsPipelineReference? pipelineReference = publishConfiguration.PipelineReference;
        Assert.IsNotNull(pipelineReference);
        Assert.AreEqual("Build", pipelineReference.StageName);
        Assert.AreEqual(2, pipelineReference.StageAttempt);
        Assert.AreEqual("RunTests", pipelineReference.PhaseName);
        Assert.AreEqual(3, pipelineReference.PhaseAttempt);
        Assert.AreEqual("Windows", pipelineReference.JobName);
        Assert.AreEqual(4, pipelineReference.JobAttempt);
    }

    [TestMethod]
    public void TryCreate_PhaseOnly_PopulatesPipelineReference()
    {
        Mock<IEnvironment> environment = CreateEnvironmentMock();
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_PHASENAME")).Returns("RunTests");
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_PHASEATTEMPT")).Returns("3");

        bool result = TryCreate(environment, out AzureDevOpsPublishConfiguration? publishConfiguration, out string? warning);

        Assert.IsTrue(result);
        Assert.IsNull(warning);
        Assert.IsNotNull(publishConfiguration);
        AzureDevOpsPipelineReference? pipelineReference = publishConfiguration.PipelineReference;
        Assert.IsNotNull(pipelineReference);
        Assert.IsNull(pipelineReference.StageName);
        Assert.IsNull(pipelineReference.StageAttempt);
        Assert.AreEqual("RunTests", pipelineReference.PhaseName);
        Assert.AreEqual(3, pipelineReference.PhaseAttempt);
        Assert.IsNull(pipelineReference.JobName);
        Assert.IsNull(pipelineReference.JobAttempt);
    }

    [TestMethod]
    public void BuildTestRunUrl_TrailingSlashesAndUnsafeProjectCharacters_TrimsAndEscapesUrl()
    {
        AzureDevOpsPublishConfiguration configuration = new(
            "https://dev.azure.com/example///",
            "project name/with?characters",
            "token",
            123,
            "run",
            "tests.dll",
            "results");

        string url = AzureDevOpsPublishConfigurationFactory.BuildTestRunUrl(configuration, 456);

        Assert.AreEqual(
            "https://dev.azure.com/example/project%20name%2Fwith%3Fcharacters/_TestManagement/Runs?runId=456&_a=resultQuery",
            url);
    }

    private static bool TryCreate(
        Mock<IEnvironment> environment,
        [NotNullWhen(true)] out AzureDevOpsPublishConfiguration? publishConfiguration,
        [NotNullWhen(false)] out string? warning,
        string? assemblyName = "MyTests",
        string applicationPath = "artifacts/MyTests.dll",
        string[]? runNameArguments = null,
        bool hasRunNameOption = false)
    {
        Mock<ICommandLineOptions> commandLineOptions = new();
        commandLineOptions
            .Setup(x => x.TryGetOptionArgumentList(
                AzureDevOpsCommandLineOptions.PublishAzureDevOpsRunNameOptionName,
                out runNameArguments))
            .Returns(hasRunNameOption);

        Mock<IConfiguration> configuration = new();
        configuration
            .Setup(x => x[PlatformConfigurationConstants.PlatformResultDirectory])
            .Returns("test-results");

        Mock<ITestApplicationModuleInfo> testApplicationModuleInfo = new();
        testApplicationModuleInfo.Setup(x => x.TryGetAssemblyName()).Returns(assemblyName);
        testApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns(applicationPath);

        return AzureDevOpsPublishConfigurationFactory.TryCreate(
            commandLineOptions.Object,
            configuration.Object,
            environment.Object,
            testApplicationModuleInfo.Object,
            out publishConfiguration,
            out warning);
    }

    private static Mock<IEnvironment> CreateEnvironmentMock()
    {
        Mock<IEnvironment> environment = new();
        environment.SetupGet(x => x.MachineName).Returns("machine-name");
        environment.Setup(x => x.GetEnvironmentVariable("TF_BUILD")).Returns("true");
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_COLLECTIONURI")).Returns("https://dev.azure.com/example/");
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_TEAMPROJECT")).Returns("My Project");
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN")).Returns("access-token");
        environment.Setup(x => x.GetEnvironmentVariable("BUILD_BUILDID")).Returns("123");
        environment.Setup(x => x.GetEnvironmentVariable("AGENT_NAME")).Returns("agent-name");
        return environment;
    }
}
