// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using MSTestAdapter.PlatformServices.Tests.Utilities;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Utilities;

public class DeploymentUtilityTests : TestContainer
{
    private const string TestRunDirectory = "C:\\temp\\testRunDirectory";
    private const string RootDeploymentDirectory = "C:\\temp\\testRunDirectory\\Deploy";
    private const string DefaultDeploymentItemPath = @"c:\temp";
    private const string DefaultDeploymentItemOutputDirectory = "out";

    private readonly Mock<ReflectionUtility> _mockReflectionUtility;
    private readonly Mock<FileUtility> _mockFileUtility;
    private readonly Mock<AssemblyUtility> _mockAssemblyUtility;
    private readonly Mock<IRunContext> _mockRunContext;
    private readonly Mock<ITestExecutionRecorder> _mockTestExecutionRecorder;

    private readonly DeploymentUtility _deploymentUtility;

#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
    private IList<string> _warnings;
#pragma warning restore IDE0052 // Remove unread private members
#pragma warning restore IDE0044 // Add readonly modifier

    public DeploymentUtilityTests()
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
        _mockTestExecutionRecorder = new Mock<ITestExecutionRecorder>();
    }

    #region Deploy tests

    public void DeployShouldReturnFalseWhenNoDeploymentItemsOnTestCase()
    {
        var testCase = new TestCase("A.C.M", new Uri("executor://testExecutor"), "A");
        testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, null);
        var testRunDirectories = new TestRunDirectories(RootDeploymentDirectory);

        _mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
        _mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
#if NET462
        _mockAssemblyUtility.Setup(
                au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out _warnings))
            .Returns(Array.Empty<string>());
        _mockAssemblyUtility.Setup(
                au => au.GetSatelliteAssemblies(It.IsAny<string>()))
            .Returns([]);
#endif

        Verify(
            !_deploymentUtility.Deploy(
                new List<TestCase> { testCase },
                testCase.Source,
                _mockRunContext.Object,
                _mockTestExecutionRecorder.Object,
                testRunDirectories));
    }

#if NET462
    public void DeployShouldDeploySourceAndItsConfigFile()
    {
        TestCase testCase = GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out TestRunDirectories testRunDirectories);

        // Setup mocks.
        _mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
        _mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
        _mockAssemblyUtility.Setup(
            au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out _warnings))
            .Returns(Array.Empty<string>());
        _mockAssemblyUtility.Setup(
            au => au.GetSatelliteAssemblies(It.IsAny<string>()))
            .Returns([]);

        // Act.
        Verify(
            _deploymentUtility.Deploy(
                new List<TestCase> { testCase },
                testCase.Source,
                _mockRunContext.Object,
                _mockTestExecutionRecorder.Object,
                testRunDirectories));

        // Assert.
        string warning;
        string sourceFile = Assembly.GetExecutingAssembly().GetName().Name + ".dll";
        string configFile = Assembly.GetExecutingAssembly().GetName().Name + ".dll.config";
        _mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(sourceFile)),
                Path.Combine(testRunDirectories.OutDirectory, sourceFile),
                out warning),
            Times.Once);
        _mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(configFile)),
                Path.Combine(testRunDirectories.OutDirectory, configFile),
                out warning),
            Times.Once);
    }

    public void DeployShouldDeployDependentFiles()
    {
        string dependencyFile = "C:\\temp\\dependency.dll";

        TestCase testCase = GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out TestRunDirectories testRunDirectories);

        // Setup mocks.
        _mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
        _mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
        _mockAssemblyUtility.Setup(
            au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out _warnings))
            .Returns([dependencyFile]);
        _mockAssemblyUtility.Setup(
            au => au.GetSatelliteAssemblies(It.IsAny<string>()))
            .Returns([]);

        // Act.
        Verify(
            _deploymentUtility.Deploy(
                new List<TestCase> { testCase },
                testCase.Source,
                _mockRunContext.Object,
                _mockTestExecutionRecorder.Object,
                testRunDirectories));

        // Assert.
        string warning;

        _mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(dependencyFile)),
                Path.Combine(testRunDirectories.OutDirectory, Path.GetFileName(dependencyFile)),
                out warning),
            Times.Once);
    }

    public void DeployShouldDeploySatelliteAssemblies()
    {
        TestCase testCase = GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out TestRunDirectories testRunDirectories);
        string assemblyFullPath = Assembly.GetExecutingAssembly().Location;
        string satelliteFullPath = Path.Combine(Path.GetDirectoryName(assemblyFullPath), "de", "satellite.dll");

        // Setup mocks.
        _mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
        _mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
        _mockAssemblyUtility.Setup(
            au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out _warnings))
            .Returns(Array.Empty<string>());
        _mockAssemblyUtility.Setup(
            au => au.GetSatelliteAssemblies(assemblyFullPath))
            .Returns([satelliteFullPath]);

        // Act.
        Verify(
            _deploymentUtility.Deploy(
                new List<TestCase> { testCase },
                testCase.Source,
                _mockRunContext.Object,
                _mockTestExecutionRecorder.Object,
                testRunDirectories));

        // Assert.
        string warning;

        _mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(satelliteFullPath)),
                Path.Combine(testRunDirectories.OutDirectory, "de", Path.GetFileName(satelliteFullPath)),
                out warning),
            Times.Once);
    }
#endif

    public void DeployShouldNotDeployIfOutputDirectoryIsInvalid()
    {
        string deploymentItemPath = "C:\\temp\\sample.dll";
        string deploymentItemOutputDirectory = "..\\..\\out";

        TestCase testCase = GetTestCaseAndTestRunDirectories(deploymentItemPath, deploymentItemOutputDirectory, out TestRunDirectories testRunDirectories);

        // Setup mocks.
        _mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
        _mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
#if NET462
        _mockAssemblyUtility.Setup(
                au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out _warnings))
            .Returns(Array.Empty<string>());
        _mockAssemblyUtility.Setup(
                au => au.GetSatelliteAssemblies(It.IsAny<string>()))
            .Returns([]);
#endif

        // Act.
        Verify(
            _deploymentUtility.Deploy(
                new List<TestCase> { testCase },
                testCase.Source,
                _mockRunContext.Object,
                _mockTestExecutionRecorder.Object,
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
        _mockTestExecutionRecorder.Verify(
            ter =>
            ter.SendMessage(
                TestMessageLevel.Warning,
                string.Format(
                    CultureInfo.InvariantCulture,
                    Resource.DeploymentErrorBadDeploymentItem,
                    deploymentItemPath,
                    deploymentItemOutputDirectory)),
            Times.Once);
    }

    public void DeployShouldDeployContentsOfADirectoryIfSpecified()
    {
        TestCase testCase = GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out TestRunDirectories testRunDirectories);
        string content1 = Path.Combine(DefaultDeploymentItemPath, "directoryContents.dll");
        var directoryContentFiles = new List<string> { content1 };

        // Setup mocks.
        _mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !(s.EndsWith(".dll") || s.EndsWith(".config"))))).Returns(true);
        _mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
#if NET462
        _mockAssemblyUtility.Setup(
            au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out _warnings))
            .Returns(Array.Empty<string>());
        _mockAssemblyUtility.Setup(
            au => au.GetSatelliteAssemblies(It.IsAny<string>()))
            .Returns([]);
#endif
        _mockFileUtility.Setup(
            fu => fu.AddFilesFromDirectory(DefaultDeploymentItemPath, It.IsAny<bool>())).Returns(directoryContentFiles);
        _mockFileUtility.Setup(
            fu => fu.AddFilesFromDirectory(DefaultDeploymentItemPath, It.IsAny<Func<string, bool>>(), It.IsAny<bool>())).Returns(directoryContentFiles);

        // Act.
        Verify(
            _deploymentUtility.Deploy(
                new List<TestCase> { testCase },
                testCase.Source,
                _mockRunContext.Object,
                _mockTestExecutionRecorder.Object,
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

#if NET462
    public void DeployShouldDeployPdbWithSourceIfPdbFileIsPresentInSourceDirectory()
    {
        TestCase testCase = GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out TestRunDirectories testRunDirectories);

        // Setup mocks.
        _mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !s.EndsWith(".dll")))).Returns(true);
        _mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
        _mockAssemblyUtility.Setup(
            au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out _warnings))
            .Returns(Array.Empty<string>());
        _mockAssemblyUtility.Setup(
            au => au.GetSatelliteAssemblies(It.IsAny<string>()))
            .Returns([]);
        string warning;
        _mockFileUtility.Setup(fu => fu.CopyFileOverwrite(It.IsAny<string>(), It.IsAny<string>(), out warning))
            .Returns(
                (string x, string y, string z) =>
                    {
                        z = string.Empty;
                        return y;
                    });

        // Act.
        Verify(
            _deploymentUtility.Deploy(
                new List<TestCase> { testCase },
                testCase.Source,
                _mockRunContext.Object,
                _mockTestExecutionRecorder.Object,
                testRunDirectories));

        // Assert.
        string sourceFile = Assembly.GetExecutingAssembly().GetName().Name + ".dll";
        string pdbFile = Assembly.GetExecutingAssembly().GetName().Name + ".pdb";
        _mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(sourceFile)),
                Path.Combine(testRunDirectories.OutDirectory, sourceFile),
                out warning),
            Times.Once);
        _mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(pdbFile)),
                Path.Combine(testRunDirectories.OutDirectory, pdbFile),
                out warning),
            Times.Once);
    }

    public void DeployShouldNotDeployPdbFileOfAssemblyIfPdbFileIsNotPresentInAssemblyDirectory()
    {
        string dependencyFile = "C:\\temp\\dependency.dll";

        // Path for pdb file of dependent assembly if pdb file is present.
        string pdbFile = Path.ChangeExtension(dependencyFile, "pdb");

        TestCase testCase = GetTestCaseAndTestRunDirectories(DefaultDeploymentItemPath, DefaultDeploymentItemOutputDirectory, out TestRunDirectories testRunDirectories);

        // Setup mocks.
        _mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !s.EndsWith(".dll")))).Returns(true);
        _mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
        _mockAssemblyUtility.Setup(
            au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out _warnings))
            .Returns([dependencyFile]);
        _mockAssemblyUtility.Setup(
            au => au.GetSatelliteAssemblies(It.IsAny<string>()))
            .Returns([]);
        string warning;
        _mockFileUtility.Setup(fu => fu.CopyFileOverwrite(It.IsAny<string>(), It.IsAny<string>(), out warning))
            .Returns(
                (string x, string y, string z) =>
                {
                    z = string.Empty;
                    return y;
                });

        // Act.
        Verify(
            _deploymentUtility.Deploy(
                new List<TestCase> { testCase },
                testCase.Source,
                _mockRunContext.Object,
                _mockTestExecutionRecorder.Object,
                testRunDirectories));

        // Assert.
        _mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(dependencyFile)),
                Path.Combine(testRunDirectories.OutDirectory, Path.GetFileName(dependencyFile)),
                out warning),
            Times.Once);

        _mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(pdbFile)),
                Path.Combine(testRunDirectories.OutDirectory, Path.GetFileName(pdbFile)),
                out warning),
            Times.Never);
    }
#endif

    #endregion

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

    #region private methods

    private static TestCase GetTestCaseAndTestRunDirectories(string deploymentItemPath, string defaultDeploymentItemOutputDirectoryOut, out TestRunDirectories testRunDirectories)
    {
        var testCase = new TestCase("A.C.M", new Uri("executor://testExecutor"), Assembly.GetExecutingAssembly().Location);
        KeyValuePair<string, string>[] kvpArray = new[]
        {
            new KeyValuePair<string, string>(
                deploymentItemPath,
                defaultDeploymentItemOutputDirectoryOut),
        };
        testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, kvpArray);
        string currentExecutingFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        testRunDirectories = new TestRunDirectories(currentExecutingFolder);

        return testCase;
    }

    #endregion
}
