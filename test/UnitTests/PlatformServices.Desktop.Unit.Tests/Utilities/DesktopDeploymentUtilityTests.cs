// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests.Utilities;

extern alias FrameworkV1;
extern alias FrameworkV2;

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
using Moq;
using MSTestAdapter.PlatformServices.Tests.Utilities;
using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

[TestClass]
#pragma warning disable SA1649 // File name must match first type name
public class DeploymentUtilityTests
#pragma warning restore SA1649 // File name must match first type name
{
    private const string TestRunDirectory = "C:\\temp\\testRunDirectory";
    private const string RootDeploymentDirectory = "C:\\temp\\testRunDirectory\\Deploy";
    private const string DefaultDeploymentItemPath = @"c:\temp";
    private const string DefaultDeploymentItemOutputDirectory = "out";

    private Mock<ReflectionUtility> mockReflectionUtility;
    private Mock<FileUtility> mockFileUtility;

    private Mock<AssemblyUtility> mockAssemblyUtility;

    private DeploymentUtility deploymentUtility;

    private Mock<IRunContext> mockRunContext;
    private Mock<ITestExecutionRecorder> mocktestExecutionRecorder;

    private IList<string> warnings;

    [TestInitialize]
    public void TestInit()
    {
        mockReflectionUtility = new Mock<ReflectionUtility>();
        mockFileUtility = new Mock<FileUtility>();
        mockAssemblyUtility = new Mock<AssemblyUtility>();
        warnings = new List<string>();

        deploymentUtility = new DeploymentUtility(
            new DeploymentItemUtility(mockReflectionUtility.Object),
            mockAssemblyUtility.Object,
            mockFileUtility.Object);

        mockRunContext = new Mock<IRunContext>();
        mocktestExecutionRecorder = new Mock<ITestExecutionRecorder>();
    }

    #region Deploy tests

    [TestMethod]
    public void DeployShouldReturnFalseWhenNoDeploymentItemsOnTestCase()
    {
        var testCase = new TestCase("A.C.M", new System.Uri("executor://testExecutor"), "A");
        testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, null);
        var testRunDirectories = new TestRunDirectories(RootDeploymentDirectory);

        mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
        mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
        mockAssemblyUtility.Setup(
            au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out warnings))
            .Returns(new string[] { });
        mockAssemblyUtility.Setup(
            au => au.GetSatelliteAssemblies(It.IsAny<string>()))
            .Returns(new List<string> { });

        Assert.IsFalse(
            deploymentUtility.Deploy(
                new List<TestCase> { testCase },
                testCase.Source,
                mockRunContext.Object,
                mocktestExecutionRecorder.Object,
                testRunDirectories));
    }

    [TestMethod]
    public void DeployShouldDeploySourceAndItsConfigFile()
    {
        var testCase = GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out var testRunDirectories);

        // Setup mocks.
        mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
        mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
        mockAssemblyUtility.Setup(
            au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out warnings))
            .Returns(new string[] { });
        mockAssemblyUtility.Setup(
            au => au.GetSatelliteAssemblies(It.IsAny<string>()))
            .Returns(new List<string> { });

        // Act.
        Assert.IsTrue(
            deploymentUtility.Deploy(
                new List<TestCase> { testCase },
                testCase.Source,
                mockRunContext.Object,
                mocktestExecutionRecorder.Object,
                testRunDirectories));

        // Assert.
        string warning;
        var sourceFile = Assembly.GetExecutingAssembly().GetName().Name + ".dll";
        var configFile = Assembly.GetExecutingAssembly().GetName().Name + ".dll.config";
        mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(sourceFile)),
                Path.Combine(testRunDirectories.OutDirectory, sourceFile),
                out warning),
            Times.Once);
        mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(configFile)),
                Path.Combine(testRunDirectories.OutDirectory, configFile),
                out warning),
            Times.Once);
    }

    [TestMethod]
    public void DeployShouldDeployDependentFiles()
    {
        var dependencyFile = "C:\\temp\\dependency.dll";

        var testCase = GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out var testRunDirectories);

        // Setup mocks.
        mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
        mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
        mockAssemblyUtility.Setup(
            au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out warnings))
            .Returns(new string[] { dependencyFile });
        mockAssemblyUtility.Setup(
            au => au.GetSatelliteAssemblies(It.IsAny<string>()))
            .Returns(new List<string> { });

        // Act.
        Assert.IsTrue(
            deploymentUtility.Deploy(
                new List<TestCase> { testCase },
                testCase.Source,
                mockRunContext.Object,
                mocktestExecutionRecorder.Object,
                testRunDirectories));

        // Assert.
        string warning;

        mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(dependencyFile)),
                Path.Combine(testRunDirectories.OutDirectory, Path.GetFileName(dependencyFile)),
                out warning),
            Times.Once);
    }

    [TestMethod]
    public void DeployShouldDeploySatelliteAssemblies()
    {
        var testCase = GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out var testRunDirectories);
        var assemblyFullPath = Assembly.GetExecutingAssembly().Location;
        var satelliteFullPath = Path.Combine(Path.GetDirectoryName(assemblyFullPath), "de", "satellite.dll");

        // Setup mocks.
        mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
        mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
        mockAssemblyUtility.Setup(
            au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out warnings))
            .Returns(new string[] { });
        mockAssemblyUtility.Setup(
            au => au.GetSatelliteAssemblies(assemblyFullPath))
            .Returns(new List<string> { satelliteFullPath });

        // Act.
        Assert.IsTrue(
            deploymentUtility.Deploy(
                new List<TestCase> { testCase },
                testCase.Source,
                mockRunContext.Object,
                mocktestExecutionRecorder.Object,
                testRunDirectories));

        // Assert.
        string warning;

        mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(satelliteFullPath)),
                Path.Combine(testRunDirectories.OutDirectory, "de", Path.GetFileName(satelliteFullPath)),
                out warning),
            Times.Once);
    }

    [TestMethod]
    public void DeployShouldNotDeployIfOutputDirectoryIsInvalid()
    {
        var assemblyFullPath = Assembly.GetExecutingAssembly().Location;
        var deploymentItemPath = "C:\\temp\\sample.dll";
        var deploymentItemOutputDirectory = "..\\..\\out";

        var testCase = GetTestCaseAndTestRunDirectories(deploymentItemPath, deploymentItemOutputDirectory, out var testRunDirectories);

        // Setup mocks.
        mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
        mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
        mockAssemblyUtility.Setup(
            au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out warnings))
            .Returns(new string[] { });
        mockAssemblyUtility.Setup(
            au => au.GetSatelliteAssemblies(It.IsAny<string>()))
            .Returns(new List<string> { });

        // Act.
        Assert.IsTrue(
            deploymentUtility.Deploy(
                new List<TestCase> { testCase },
                testCase.Source,
                mockRunContext.Object,
                mocktestExecutionRecorder.Object,
                testRunDirectories));

        // Assert.
        string warning;

        mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(deploymentItemPath)),
                It.IsAny<string>(),
                out warning),
            Times.Never);

        // Verify the warning.
        mocktestExecutionRecorder.Verify(
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
        var assemblyFullPath = Assembly.GetExecutingAssembly().Location;

        var testCase = GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out var testRunDirectories);
        var content1 = Path.Combine(DefaultDeploymentItemPath, "directoryContents.dll");
        var directoryContentFiles = new List<string> { content1 };

        // Setup mocks.
        mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
        mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
        mockAssemblyUtility.Setup(
            au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out warnings))
            .Returns(new string[] { });
        mockFileUtility.Setup(
            fu => fu.AddFilesFromDirectory(DefaultDeploymentItemPath, It.IsAny<bool>())).Returns(directoryContentFiles);
        mockFileUtility.Setup(
            fu => fu.AddFilesFromDirectory(DefaultDeploymentItemPath, It.IsAny<Func<string, bool>>(), It.IsAny<bool>())).Returns(directoryContentFiles);
        mockAssemblyUtility.Setup(
            au => au.GetSatelliteAssemblies(It.IsAny<string>()))
            .Returns(new List<string> { });

        // Act.
        Assert.IsTrue(
            deploymentUtility.Deploy(
                new List<TestCase> { testCase },
                testCase.Source,
                mockRunContext.Object,
                mocktestExecutionRecorder.Object,
                testRunDirectories));

        // Assert.
        string warning;

        mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(content1)),
                It.IsAny<string>(),
                out warning),
            Times.Once);
    }

    [TestMethod]
    public void DeployShouldDeployPdbWithSourceIfPdbFileIsPresentInSourceDirectory()
    {
        var testCase = GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out var testRunDirectories);

        // Setup mocks.
        mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !s.EndsWith(".dll")))).Returns(true);
        mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
        mockAssemblyUtility.Setup(
            au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out warnings))
            .Returns(new string[] { });
        mockAssemblyUtility.Setup(
            au => au.GetSatelliteAssemblies(It.IsAny<string>()))
            .Returns(new List<string> { });
        string warning;
        mockFileUtility.Setup(fu => fu.CopyFileOverwrite(It.IsAny<string>(), It.IsAny<string>(), out warning))
            .Returns(
                (string x, string y, string z) =>
                    {
                        z = string.Empty;
                        return y;
                    });

        // Act.
        Assert.IsTrue(
            deploymentUtility.Deploy(
                new List<TestCase> { testCase },
                testCase.Source,
                mockRunContext.Object,
                mocktestExecutionRecorder.Object,
                testRunDirectories));

        // Assert.
        var sourceFile = Assembly.GetExecutingAssembly().GetName().Name + ".dll";
        var pdbFile = Assembly.GetExecutingAssembly().GetName().Name + ".pdb";
        mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(sourceFile)),
                Path.Combine(testRunDirectories.OutDirectory, sourceFile),
                out warning),
            Times.Once);
        mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(pdbFile)),
                Path.Combine(testRunDirectories.OutDirectory, pdbFile),
                out warning),
            Times.Once);
    }

    [TestMethod]
    public void DeployShouldNotDeployPdbFileOfAssemblyIfPdbFileIsNotPresentInAssemblyDirectory()
    {
        var dependencyFile = "C:\\temp\\dependency.dll";

        // Path for pdb file of dependent assembly if pdb file is present.
        var pdbFile = Path.ChangeExtension(dependencyFile, "pdb");

        var testCase = GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out var testRunDirectories);

        // Setup mocks.
        mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !s.EndsWith(".dll")))).Returns(true);
        mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
        mockAssemblyUtility.Setup(
            au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out warnings))
            .Returns(new string[] { dependencyFile });
        mockAssemblyUtility.Setup(
            au => au.GetSatelliteAssemblies(It.IsAny<string>()))
            .Returns(new List<string> { });
        string warning;
        mockFileUtility.Setup(fu => fu.CopyFileOverwrite(It.IsAny<string>(), It.IsAny<string>(), out warning))
            .Returns(
                (string x, string y, string z) =>
                {
                    z = string.Empty;
                    return y;
                });

        // Act.
        Assert.IsTrue(
            deploymentUtility.Deploy(
                new List<TestCase> { testCase },
                testCase.Source,
                mockRunContext.Object,
                mocktestExecutionRecorder.Object,
                testRunDirectories));

        // Assert.
        mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(dependencyFile)),
                Path.Combine(testRunDirectories.OutDirectory, Path.GetFileName(dependencyFile)),
                out warning),
            Times.Once);

        mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(pdbFile)),
                Path.Combine(testRunDirectories.OutDirectory, Path.GetFileName(pdbFile)),
                out warning),
            Times.Never);
    }

    #endregion

    #region private methods

    private TestCase GetTestCaseAndTestRunDirectories(string deploymentItemPath, string defaultDeploymentItemOutputDirectoryout, out TestRunDirectories testRunDirectories)
    {
        var testCase = new TestCase("A.C.M", new System.Uri("executor://testExecutor"), Assembly.GetExecutingAssembly().Location);
        var kvpArray = new[]
                {
                    new KeyValuePair<string, string>(
                        deploymentItemPath,
                        defaultDeploymentItemOutputDirectoryout)
                };
        testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, kvpArray);
        var currentExecutingFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        testRunDirectories = new TestRunDirectories(currentExecutingFolder);

        return testCase;
    }

    #endregion
}
