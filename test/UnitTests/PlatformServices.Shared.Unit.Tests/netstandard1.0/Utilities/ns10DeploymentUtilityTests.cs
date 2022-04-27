// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Utilities
{
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
            this.mockReflectionUtility = new Mock<ReflectionUtility>();
            this.mockFileUtility = new Mock<FileUtility>();
            this.mockAssemblyUtility = new Mock<AssemblyUtility>();
            this.warnings = new List<string>();

            this.deploymentUtility = new DeploymentUtility(
                new DeploymentItemUtility(this.mockReflectionUtility.Object),
                this.mockAssemblyUtility.Object,
                this.mockFileUtility.Object);

            this.mockRunContext = new Mock<IRunContext>();
            this.mocktestExecutionRecorder = new Mock<ITestExecutionRecorder>();
        }

        #region CreateDeploymentDirectories tests

        [TestMethod]
        public void CreateDeploymentDirectoriesShouldCreateDeploymentDirectoryFromRunContext()
        {
            // Setup mocks
            this.mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(TestRunDirectory);
            this.mockFileUtility.Setup(fu => fu.GetNextIterationDirectoryName(TestRunDirectory, It.IsAny<string>()))
                .Returns(RootDeploymentDirectory);

            // Act.
            this.deploymentUtility.CreateDeploymentDirectories(this.mockRunContext.Object);

            // Assert.
            this.mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(RootDeploymentDirectory), Times.Once);
            this.mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(Path.Combine(RootDeploymentDirectory, TestRunDirectories.DeploymentInDirectorySuffix)), Times.Once);
            this.mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(RootDeploymentDirectory), Times.Once);
            this.mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(Path.Combine(Path.Combine(RootDeploymentDirectory, TestRunDirectories.DeploymentInDirectorySuffix), Environment.MachineName)), Times.Once);
        }

        [TestMethod]
        public void CreateDeploymentDirectoriesShouldCreateDefaultDeploymentDirectoryIfTestRunDirectoryIsNull()
        {
            // Setup mocks
            this.mockRunContext.Setup(rc => rc.TestRunDirectory).Returns((string)null);
            this.mockFileUtility.Setup(fu => fu.GetNextIterationDirectoryName(It.Is<string>(s => s.Contains(TestRunDirectories.DefaultDeploymentRootDirectory)), It.IsAny<string>()))
                .Returns(RootDeploymentDirectory);

            // Act.
            this.deploymentUtility.CreateDeploymentDirectories(this.mockRunContext.Object);

            // Assert.
            this.mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(RootDeploymentDirectory), Times.Once);
            this.mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(Path.Combine(RootDeploymentDirectory, TestRunDirectories.DeploymentInDirectorySuffix)), Times.Once);
            this.mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(RootDeploymentDirectory), Times.Once);
            this.mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(Path.Combine(Path.Combine(RootDeploymentDirectory, TestRunDirectories.DeploymentInDirectorySuffix), Environment.MachineName)), Times.Once);
        }

        [TestMethod]
        public void CreateDeploymentDirectoriesShouldCreateDefaultDeploymentDirectoryIfRunContextIsNull()
        {
            // Setup mocks
            this.mockFileUtility.Setup(fu => fu.GetNextIterationDirectoryName(It.Is<string>(s => s.Contains(TestRunDirectories.DefaultDeploymentRootDirectory)), It.IsAny<string>()))
                .Returns(RootDeploymentDirectory);

            // Act.
            this.deploymentUtility.CreateDeploymentDirectories(null);

            // Assert.
            this.mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(RootDeploymentDirectory), Times.Once);
            this.mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(Path.Combine(RootDeploymentDirectory, TestRunDirectories.DeploymentInDirectorySuffix)), Times.Once);
            this.mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(RootDeploymentDirectory), Times.Once);
            this.mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(Path.Combine(Path.Combine(RootDeploymentDirectory, TestRunDirectories.DeploymentInDirectorySuffix), Environment.MachineName)), Times.Once);
        }

        #endregion
    }
}
