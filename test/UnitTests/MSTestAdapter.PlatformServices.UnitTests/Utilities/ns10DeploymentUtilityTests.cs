// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.Tests.Utilities;
#pragma warning disable SA1649 // File name must match first type name
public class DeploymentUtilityTests : TestContainer
#pragma warning restore SA1649 // File name must match first type name
{
    private const string TestRunDirectory = "C:\\temp\\testRunDirectory";
    private const string RootDeploymentDirectory = "C:\\temp\\testRunDirectory\\Deploy";

    private readonly Mock<ReflectionUtility> _mockReflectionUtility;
    private readonly Mock<FileUtility> _mockFileUtility;
    private readonly Mock<AssemblyUtility> _mockAssemblyUtility;
    private readonly DeploymentUtility _deploymentUtility;
    private readonly Mock<IRunContext> _mockRunContext;

    public DeploymentUtilityTests()
    {
        _mockReflectionUtility = new Mock<ReflectionUtility>();
        _mockFileUtility = new Mock<FileUtility>();
        _mockAssemblyUtility = new Mock<AssemblyUtility>();

        _deploymentUtility = new DeploymentUtility(
            new DeploymentItemUtility(_mockReflectionUtility.Object),
            _mockAssemblyUtility.Object,
            _mockFileUtility.Object);

        _mockRunContext = new Mock<IRunContext>();
    }

    #region CreateDeploymentDirectories tests

    public void CreateDeploymentDirectoriesShouldCreateDeploymentDirectoryFromRunContext()
    {
        // Setup mocks
        _mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(TestRunDirectory);
        _mockFileUtility.Setup(fu => fu.GetNextIterationDirectoryName(TestRunDirectory, It.IsAny<string>()))
            .Returns(RootDeploymentDirectory);

        // Act.
        _deploymentUtility.CreateDeploymentDirectories(_mockRunContext.Object);

        // Assert.
        _mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(RootDeploymentDirectory), Times.Once);
        _mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(Path.Combine(RootDeploymentDirectory, TestRunDirectories.DeploymentInDirectorySuffix)), Times.Once);
        _mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(RootDeploymentDirectory), Times.Once);
        _mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(Path.Combine(Path.Combine(RootDeploymentDirectory, TestRunDirectories.DeploymentInDirectorySuffix), Environment.MachineName)), Times.Once);
    }

    public void CreateDeploymentDirectoriesShouldCreateDefaultDeploymentDirectoryIfTestRunDirectoryIsNull()
    {
        // Setup mocks
        _mockRunContext.Setup(rc => rc.TestRunDirectory).Returns((string)null);
        _mockFileUtility.Setup(fu => fu.GetNextIterationDirectoryName(It.Is<string>(s => s.Contains(TestRunDirectories.DefaultDeploymentRootDirectory)), It.IsAny<string>()))
            .Returns(RootDeploymentDirectory);

        // Act.
        _deploymentUtility.CreateDeploymentDirectories(_mockRunContext.Object);

        // Assert.
        _mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(RootDeploymentDirectory), Times.Once);
        _mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(Path.Combine(RootDeploymentDirectory, TestRunDirectories.DeploymentInDirectorySuffix)), Times.Once);
        _mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(RootDeploymentDirectory), Times.Once);
        _mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(Path.Combine(Path.Combine(RootDeploymentDirectory, TestRunDirectories.DeploymentInDirectorySuffix), Environment.MachineName)), Times.Once);
    }

    public void CreateDeploymentDirectoriesShouldCreateDefaultDeploymentDirectoryIfRunContextIsNull()
    {
        // Setup mocks
        _mockFileUtility.Setup(fu => fu.GetNextIterationDirectoryName(It.Is<string>(s => s.Contains(TestRunDirectories.DefaultDeploymentRootDirectory)), It.IsAny<string>()))
            .Returns(RootDeploymentDirectory);

        // Act.
        _deploymentUtility.CreateDeploymentDirectories(null);

        // Assert.
        _mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(RootDeploymentDirectory), Times.Once);
        _mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(Path.Combine(RootDeploymentDirectory, TestRunDirectories.DeploymentInDirectorySuffix)), Times.Once);
        _mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(RootDeploymentDirectory), Times.Once);
        _mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(Path.Combine(Path.Combine(RootDeploymentDirectory, TestRunDirectories.DeploymentInDirectorySuffix), Environment.MachineName)), Times.Once);
    }

    #endregion
}
