// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Utilities
{
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
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class NetCoreDeploymentUtilityTests
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

        #region Deploy tests

        [TestMethod]
        public void DeployShouldReturnFalseWhenNoDeploymentItemsOnTestCase()
        {
            var testCase = new TestCase("A.C.M", new System.Uri("executor://testExecutor"), "A");
            testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, null);
            var testRunDirectories = new TestRunDirectories(RootDeploymentDirectory);

            this.mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
            this.mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);

            Assert.IsFalse(
                this.deploymentUtility.Deploy(
                    new List<TestCase> { testCase },
                    testCase.Source,
                    this.mockRunContext.Object,
                    this.mocktestExecutionRecorder.Object,
                    testRunDirectories));
        }

        [TestMethod]
        public void DeployShouldNotDeployIfOutputDirectoryIsInvalid()
        {
            TestRunDirectories testRunDirectories;
            var assemblyFullPath = Assembly.GetEntryAssembly().Location;
            var deploymentItemPath = "C:\\temp\\sample.dll";
            var deploymentItemOutputDirectory = "..\\..\\out";

            var testCase = this.GetTestCaseAndTestRunDirectories(deploymentItemPath, deploymentItemOutputDirectory, out testRunDirectories);

            // Setup mocks.
            this.mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
            this.mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);

            // Act.
            Assert.IsTrue(
                this.deploymentUtility.Deploy(
                    new List<TestCase> { testCase },
                    testCase.Source,
                    this.mockRunContext.Object,
                    this.mocktestExecutionRecorder.Object,
                    testRunDirectories));

            // Assert.
            string warning;

            this.mockFileUtility.Verify(
                fu =>
                fu.CopyFileOverwrite(
                    It.Is<string>(s => s.Contains(deploymentItemPath)),
                    It.IsAny<string>(),
                    out warning),
                Times.Never);

            // Verify the warning.
            this.mocktestExecutionRecorder.Verify(
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
            TestRunDirectories testRunDirectories;
            var assemblyFullPath = Assembly.GetEntryAssembly().Location;

            var testCase = this.GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out testRunDirectories);
            var content1 = Path.Combine(DefaultDeploymentItemPath, "directoryContents.dll");
            var directoryContentFiles = new List<string> { content1 };

            // Setup mocks.
            this.mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
            this.mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
            this.mockFileUtility.Setup(
                fu => fu.AddFilesFromDirectory(DefaultDeploymentItemPath, It.IsAny<bool>())).Returns(directoryContentFiles);
            this.mockFileUtility.Setup(
                fu => fu.AddFilesFromDirectory(DefaultDeploymentItemPath, It.IsAny<Func<string, bool>>(), It.IsAny<bool>())).Returns(directoryContentFiles);

            // Act.
            Assert.IsTrue(
                this.deploymentUtility.Deploy(
                    new List<TestCase> { testCase },
                    testCase.Source,
                    this.mockRunContext.Object,
                    this.mocktestExecutionRecorder.Object,
                    testRunDirectories));

            // Assert.
            string warning;

            this.mockFileUtility.Verify(
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
            this.GetType();
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
}
