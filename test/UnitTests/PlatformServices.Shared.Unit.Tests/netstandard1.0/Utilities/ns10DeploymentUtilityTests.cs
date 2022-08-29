// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Utilities;

#if NETCOREAPP
using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
extern alias FrameworkV1;
extern alias FrameworkV2;

using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Moq;

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

    #region CreateDeploymentDirectories tests

    [TestMethod]
    public void CreateDeploymentDirectoriesShouldCreateDeploymentDirectoryFromRunContext()
    {
        // Setup mocks
        mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(TestRunDirectory);
        mockFileUtility.Setup(fu => fu.GetNextIterationDirectoryName(TestRunDirectory, It.IsAny<string>()))
            .Returns(RootDeploymentDirectory);

        // Act.
        deploymentUtility.CreateDeploymentDirectories(mockRunContext.Object);

        // Assert.
        mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(RootDeploymentDirectory), Times.Once);
        mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(Path.Combine(RootDeploymentDirectory, TestRunDirectories.DeploymentInDirectorySuffix)), Times.Once);
        mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(RootDeploymentDirectory), Times.Once);
        mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(Path.Combine(Path.Combine(RootDeploymentDirectory, TestRunDirectories.DeploymentInDirectorySuffix), Environment.MachineName)), Times.Once);
    }

    [TestMethod]
    public void CreateDeploymentDirectoriesShouldCreateDefaultDeploymentDirectoryIfTestRunDirectoryIsNull()
    {
        // Setup mocks
        mockRunContext.Setup(rc => rc.TestRunDirectory).Returns((string)null);
        mockFileUtility.Setup(fu => fu.GetNextIterationDirectoryName(It.Is<string>(s => s.Contains(TestRunDirectories.DefaultDeploymentRootDirectory)), It.IsAny<string>()))
            .Returns(RootDeploymentDirectory);

        // Act.
        deploymentUtility.CreateDeploymentDirectories(mockRunContext.Object);

        // Assert.
        mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(RootDeploymentDirectory), Times.Once);
        mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(Path.Combine(RootDeploymentDirectory, TestRunDirectories.DeploymentInDirectorySuffix)), Times.Once);
        mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(RootDeploymentDirectory), Times.Once);
        mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(Path.Combine(Path.Combine(RootDeploymentDirectory, TestRunDirectories.DeploymentInDirectorySuffix), Environment.MachineName)), Times.Once);
    }

    [TestMethod]
    public void CreateDeploymentDirectoriesShouldCreateDefaultDeploymentDirectoryIfRunContextIsNull()
    {
        // Setup mocks
        mockFileUtility.Setup(fu => fu.GetNextIterationDirectoryName(It.Is<string>(s => s.Contains(TestRunDirectories.DefaultDeploymentRootDirectory)), It.IsAny<string>()))
            .Returns(RootDeploymentDirectory);

        // Act.
        deploymentUtility.CreateDeploymentDirectories(null);

        // Assert.
        mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(RootDeploymentDirectory), Times.Once);
        mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(Path.Combine(RootDeploymentDirectory, TestRunDirectories.DeploymentInDirectorySuffix)), Times.Once);
        mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(RootDeploymentDirectory), Times.Once);
        mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(Path.Combine(Path.Combine(RootDeploymentDirectory, TestRunDirectories.DeploymentInDirectorySuffix), Environment.MachineName)), Times.Once);
    }

    #endregion
}
