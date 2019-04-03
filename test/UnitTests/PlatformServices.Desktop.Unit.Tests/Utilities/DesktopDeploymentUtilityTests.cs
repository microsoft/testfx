// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests.Utilities
{
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
            this.mockAssemblyUtility.Setup(
                au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out this.warnings))
                .Returns(new string[] { });
            this.mockAssemblyUtility.Setup(
                au => au.GetSatelliteAssemblies(It.IsAny<string>()))
                .Returns(new List<string> { });

            Assert.IsFalse(
                this.deploymentUtility.Deploy(
                    new List<TestCase> { testCase },
                    testCase.Source,
                    this.mockRunContext.Object,
                    this.mocktestExecutionRecorder.Object,
                    testRunDirectories));
        }

        [TestMethod]
        public void DeployShouldDeploySourceAndItsConfigFile()
        {
            TestRunDirectories testRunDirectories;
            var testCase = this.GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out testRunDirectories);

            // Setup mocks.
            this.mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
            this.mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
            this.mockAssemblyUtility.Setup(
                au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out this.warnings))
                .Returns(new string[] { });
            this.mockAssemblyUtility.Setup(
                au => au.GetSatelliteAssemblies(It.IsAny<string>()))
                .Returns(new List<string> { });

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
            var sourceFile = Assembly.GetExecutingAssembly().GetName().Name + ".dll";
            var configFile = Assembly.GetExecutingAssembly().GetName().Name + ".dll.config";
            this.mockFileUtility.Verify(
                fu =>
                fu.CopyFileOverwrite(
                    It.Is<string>(s => s.Contains(sourceFile)),
                    Path.Combine(testRunDirectories.OutDirectory, sourceFile),
                    out warning),
                Times.Once);
            this.mockFileUtility.Verify(
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

            TestRunDirectories testRunDirectories;
            var testCase = this.GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out testRunDirectories);

            // Setup mocks.
            this.mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
            this.mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
            this.mockAssemblyUtility.Setup(
                au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out this.warnings))
                .Returns(new string[] { dependencyFile });
            this.mockAssemblyUtility.Setup(
                au => au.GetSatelliteAssemblies(It.IsAny<string>()))
                .Returns(new List<string> { });

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
                    It.Is<string>(s => s.Contains(dependencyFile)),
                    Path.Combine(testRunDirectories.OutDirectory, Path.GetFileName(dependencyFile)),
                    out warning),
                Times.Once);
        }

        [TestMethod]
        public void DeployShouldDeploySatelliteAssemblies()
        {
            TestRunDirectories testRunDirectories;
            var testCase = this.GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out testRunDirectories);
            var assemblyFullPath = Assembly.GetExecutingAssembly().Location;
            var satelliteFullPath = Path.Combine(Path.GetDirectoryName(assemblyFullPath), "de", "satellite.dll");

            // Setup mocks.
            this.mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
            this.mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
            this.mockAssemblyUtility.Setup(
                au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out this.warnings))
                .Returns(new string[] { });
            this.mockAssemblyUtility.Setup(
                au => au.GetSatelliteAssemblies(assemblyFullPath))
                .Returns(new List<string> { satelliteFullPath });

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
                    It.Is<string>(s => s.Contains(satelliteFullPath)),
                    Path.Combine(testRunDirectories.OutDirectory, "de", Path.GetFileName(satelliteFullPath)),
                    out warning),
                Times.Once);
        }

        [TestMethod]
        public void DeployShouldNotDeployIfOutputDirectoryIsInvalid()
        {
            TestRunDirectories testRunDirectories;
            var assemblyFullPath = Assembly.GetExecutingAssembly().Location;
            var deploymentItemPath = "C:\\temp\\sample.dll";
            var deploymentItemOutputDirectory = "..\\..\\out";

            var testCase = this.GetTestCaseAndTestRunDirectories(deploymentItemPath, deploymentItemOutputDirectory, out testRunDirectories);

            // Setup mocks.
            this.mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
            this.mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
            this.mockAssemblyUtility.Setup(
                au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out this.warnings))
                .Returns(new string[] { });
            this.mockAssemblyUtility.Setup(
                au => au.GetSatelliteAssemblies(It.IsAny<string>()))
                .Returns(new List<string> { });

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
            var assemblyFullPath = Assembly.GetExecutingAssembly().Location;

            var testCase = this.GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out testRunDirectories);
            var content1 = Path.Combine(DefaultDeploymentItemPath, "directoryContents.dll");
            var directoryContentFiles = new List<string> { content1 };

            // Setup mocks.
            this.mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
            this.mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
            this.mockAssemblyUtility.Setup(
                au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out this.warnings))
                .Returns(new string[] { });
            this.mockFileUtility.Setup(
                fu => fu.AddFilesFromDirectory(DefaultDeploymentItemPath, It.IsAny<bool>())).Returns(directoryContentFiles);
            this.mockFileUtility.Setup(
                fu => fu.AddFilesFromDirectory(DefaultDeploymentItemPath, It.IsAny<Func<string, bool>>(), It.IsAny<bool>())).Returns(directoryContentFiles);
            this.mockAssemblyUtility.Setup(
                au => au.GetSatelliteAssemblies(It.IsAny<string>()))
                .Returns(new List<string> { });

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

        [TestMethod]
        public void DeployShouldDeployPdbWithSourceIfPdbFileIsPresentInSourceDirectory()
        {
            TestRunDirectories testRunDirectories;
            var testCase = this.GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out testRunDirectories);

            // Setup mocks.
            this.mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !s.EndsWith(".dll")))).Returns(true);
            this.mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
            this.mockAssemblyUtility.Setup(
                au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out this.warnings))
                .Returns(new string[] { });
            this.mockAssemblyUtility.Setup(
                au => au.GetSatelliteAssemblies(It.IsAny<string>()))
                .Returns(new List<string> { });
            string warning;
            this.mockFileUtility.Setup(fu => fu.CopyFileOverwrite(It.IsAny<string>(), It.IsAny<string>(), out warning))
                .Returns(
                    (string x, string y, string z) =>
                        {
                            z = string.Empty;
                            return y;
                        });

            // Act.
            Assert.IsTrue(
                this.deploymentUtility.Deploy(
                    new List<TestCase> { testCase },
                    testCase.Source,
                    this.mockRunContext.Object,
                    this.mocktestExecutionRecorder.Object,
                    testRunDirectories));

            // Assert.
            var sourceFile = Assembly.GetExecutingAssembly().GetName().Name + ".dll";
            var pdbFile = Assembly.GetExecutingAssembly().GetName().Name + ".pdb";
            this.mockFileUtility.Verify(
                fu =>
                fu.CopyFileOverwrite(
                    It.Is<string>(s => s.Contains(sourceFile)),
                    Path.Combine(testRunDirectories.OutDirectory, sourceFile),
                    out warning),
                Times.Once);
            this.mockFileUtility.Verify(
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

            TestRunDirectories testRunDirectories;
            var testCase = this.GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out testRunDirectories);

            // Setup mocks.
            this.mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !s.EndsWith(".dll")))).Returns(true);
            this.mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
            this.mockAssemblyUtility.Setup(
                au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out this.warnings))
                .Returns(new string[] { dependencyFile });
            this.mockAssemblyUtility.Setup(
                au => au.GetSatelliteAssemblies(It.IsAny<string>()))
                .Returns(new List<string> { });
            string warning;
            this.mockFileUtility.Setup(fu => fu.CopyFileOverwrite(It.IsAny<string>(), It.IsAny<string>(), out warning))
                .Returns(
                    (string x, string y, string z) =>
                    {
                        z = string.Empty;
                        return y;
                    });

            // Act.
            Assert.IsTrue(
                this.deploymentUtility.Deploy(
                    new List<TestCase> { testCase },
                    testCase.Source,
                    this.mockRunContext.Object,
                    this.mocktestExecutionRecorder.Object,
                    testRunDirectories));

            // Assert.
            this.mockFileUtility.Verify(
                fu =>
                fu.CopyFileOverwrite(
                    It.Is<string>(s => s.Contains(dependencyFile)),
                    Path.Combine(testRunDirectories.OutDirectory, Path.GetFileName(dependencyFile)),
                    out warning),
                Times.Once);

            this.mockFileUtility.Verify(
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
}
