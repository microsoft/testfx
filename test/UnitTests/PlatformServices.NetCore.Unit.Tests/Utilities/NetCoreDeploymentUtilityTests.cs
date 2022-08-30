// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Utilities;

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
public class NetCoreDeploymentUtilityTests
{
    private const string TestRunDirectory = "C:\\temp\\testRunDirectory";
    private const string RootDeploymentDirectory = "C:\\temp\\testRunDirectory\\Deploy";
    private const string DefaultDeploymentItemPath = @"c:\temp";
    private const string DefaultDeploymentItemOutputDirectory = "out";

    private Mock<ReflectionUtility> _mockReflectionUtility;
    private Mock<FileUtility> _mockFileUtility;
    private Mock<AssemblyUtility> _mockAssemblyUtility;

    private DeploymentUtility _deploymentUtility;

    private Mock<IRunContext> _mockRunContext;
    private Mock<ITestExecutionRecorder> _mocktestExecutionRecorder;

    private IList<string> _warnings;

    [TestInitialize]
    public void TestInit()
    {
        _mockReflectionUtility = new Mock<ReflectionUtility>();
        _mockFileUtility = new Mock<FileUtility>();
        _mockAssemblyUtility = new Mock<AssemblyUtility>();
        _warnings = new List<string>();

        _deploymentUtility = new DeploymentUtility(
            new DeploymentItemUtility(_mockReflectionUtility.Object),
            _mockAssemblyUtility.Object,
            _mockFileUtility.Object);

        _mockRunContext = new Mock<IRunContext>();
        _mocktestExecutionRecorder = new Mock<ITestExecutionRecorder>();
    }

    #region Deploy tests

    [TestMethod]
    public void DeployShouldReturnFalseWhenNoDeploymentItemsOnTestCase()
    {
        var testCase = new TestCase("A.C.M", new System.Uri("executor://testExecutor"), "A");
        testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, null);
        var testRunDirectories = new TestRunDirectories(RootDeploymentDirectory);

        _mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
        _mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);

        Assert.IsFalse(
            _deploymentUtility.Deploy(
                new List<TestCase> { testCase },
                testCase.Source,
                _mockRunContext.Object,
                _mocktestExecutionRecorder.Object,
                testRunDirectories));
    }

    [TestMethod]
    public void DeployShouldNotDeployIfOutputDirectoryIsInvalid()
    {
        var assemblyFullPath = Assembly.GetEntryAssembly().Location;
        var deploymentItemPath = "C:\\temp\\sample.dll";
        var deploymentItemOutputDirectory = "..\\..\\out";

        var testCase = GetTestCaseAndTestRunDirectories(deploymentItemPath, deploymentItemOutputDirectory, out var testRunDirectories);

        // Setup mocks.
        _mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
        _mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);

        // Act.
        Assert.IsTrue(
            _deploymentUtility.Deploy(
                new List<TestCase> { testCase },
                testCase.Source,
                _mockRunContext.Object,
                _mocktestExecutionRecorder.Object,
                testRunDirectories));

        // Assert.
        string warning;

        _mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(deploymentItemPath)),
                It.IsAny<string>(),
                out warning),
            Times.Never);

        // Verify the warning.
        _mocktestExecutionRecorder.Verify(
            ter =>
            ter.SendMessage(
                TestMessageLevel.Warning,
                string.Format(
                    Resource.DeploymentErrorBadDeploymentItem,
                    deploymentItemPath,
                    deploymentItemOutputDirectory)),
            Times.Once);
    }

    [TestMethod]
    public void DeployShouldDeployContentsOfADirectoryIfSpecified()
    {
        var assemblyFullPath = Assembly.GetEntryAssembly().Location;

        var testCase = GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out var testRunDirectories);
        var content1 = Path.Combine(DefaultDeploymentItemPath, "directoryContents.dll");
        var directoryContentFiles = new List<string> { content1 };

        // Setup mocks.
        _mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
        _mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
        _mockFileUtility.Setup(
            fu => fu.AddFilesFromDirectory(DefaultDeploymentItemPath, It.IsAny<bool>())).Returns(directoryContentFiles);
        _mockFileUtility.Setup(
            fu => fu.AddFilesFromDirectory(DefaultDeploymentItemPath, It.IsAny<Func<string, bool>>(), It.IsAny<bool>())).Returns(directoryContentFiles);

        // Act.
        Assert.IsTrue(
            _deploymentUtility.Deploy(
                new List<TestCase> { testCase },
                testCase.Source,
                _mockRunContext.Object,
                _mocktestExecutionRecorder.Object,
                testRunDirectories));

        // Assert.
        string warning;

        _mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(content1)),
                It.IsAny<string>(),
                out warning),
            Times.Once);
    }

    #endregion

    #region private methods

    private TestCase GetTestCaseAndTestRunDirectories(string deploymentItemPath, string defaultDeploymentItemOutputDirectoryout, out TestRunDirectories testRunDirectories)
    {
        GetType();
        var testCase = new TestCase("A.C.M", new System.Uri("executor://testExecutor"), typeof(DeploymentUtilityTests).GetTypeInfo().Assembly.Location);
        var kvpArray = new[]
                {
                    new KeyValuePair<string, string>(
                        deploymentItemPath,
                        defaultDeploymentItemOutputDirectoryout)
                };
        testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, kvpArray);
        var currentExecutingFolder = Path.GetDirectoryName(typeof(DeploymentUtilityTests).GetTypeInfo().Assembly.Location);

        testRunDirectories = new TestRunDirectories(currentExecutingFolder);

        return testCase;
    }

    #endregion
}
