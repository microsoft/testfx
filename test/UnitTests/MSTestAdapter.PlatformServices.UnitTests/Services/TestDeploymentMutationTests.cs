// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

[DoNotParallelize]
public sealed class TestDeploymentMutationTests : TestContainer
{
    private const string DeploymentItemPath = @"c:\temp";
    private const string DeploymentItemOutputDirectory = "out";

    private static readonly string TestSource = typeof(TestDeploymentMutationTests).Assembly.Location;

    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();
    private readonly Mock<IReflectionOperations> _mockReflectionOperations = new();
    private readonly Mock<FileUtility> _mockFileUtility = new();

#if NETFRAMEWORK
#pragma warning disable IDE0044 // Add readonly modifier
    private IList<string> _warnings = [];
#pragma warning restore IDE0044 // Add readonly modifier
#endif

    public TestDeploymentMutationTests()
    {
        PlatformServiceProvider.Instance = _provider;
        TestDeployment.Reset();
        MSTestSettingsProvider.Reset();
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            TestDeployment.Reset();
            MSTestSettingsProvider.Reset();
            PlatformServiceProvider.Instance = _previousProvider;
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    public void CleanupShouldLogTheDeploymentDirectoryDeletionWhenInfoLoggingIsEnabled()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        TestDeployment testDeployment = CreateTestDeployment(out TestRunDirectories testRunDirectories);
        DeployATestWithDeploymentItems(testDeployment, testRunDirectories);

        testDeployment.Cleanup();

        _mockFileUtility.Verify(utility => utility.DeleteDirectories(testRunDirectories.RootDeploymentDirectory), Times.Once);
        _provider.MockTraceLogger.Verify(logger => logger.Info("Deleting deployment directory {0}", testRunDirectories.RootDeploymentDirectory), Times.Once);
        _provider.MockTraceLogger.Verify(logger => logger.Info("Deleted deployment directory {0}", testRunDirectories.RootDeploymentDirectory), Times.Once);
    }

    public void CleanupShouldNotLogTheDeploymentDirectoryDeletionWhenInfoLoggingIsDisabled()
    {
        TestDeployment testDeployment = CreateTestDeployment(out TestRunDirectories testRunDirectories);
        DeployATestWithDeploymentItems(testDeployment, testRunDirectories);

        testDeployment.Cleanup();

        _mockFileUtility.Verify(utility => utility.DeleteDirectories(testRunDirectories.RootDeploymentDirectory), Times.Once);
        _provider.MockTraceLogger.Verify(logger => logger.Info("Deleting deployment directory {0}", It.IsAny<object?[]>()), Times.Never);
        _provider.MockTraceLogger.Verify(logger => logger.Info("Deleted deployment directory {0}", It.IsAny<object?[]>()), Times.Never);
    }

    public void DeployShouldDeployWhenOnlySomeTestsHaveDeploymentItems()
    {
        TestDeployment testDeployment = CreateTestDeployment(out TestRunDirectories testRunDirectories);
        UnitTestElement[] tests = [CreateTestWithoutDeploymentItems(), CreateTestWithDeploymentItems()];

        bool deployed = testDeployment.Deploy(tests, new DeploymentContext(testRunDirectories.RootDeploymentDirectory, null), new Mock<IAdapterMessageLogger>().Object);

        deployed.Should().BeTrue();
        testDeployment.GetDeploymentDirectory().Should().Be(testRunDirectories.OutDirectory);
    }

    public void DeployShouldNotDeployWhenThereAreNoTests()
    {
        TestDeployment testDeployment = CreateTestDeployment(out TestRunDirectories testRunDirectories);

        bool deployed = testDeployment.Deploy([], new DeploymentContext(testRunDirectories.RootDeploymentDirectory, null), new Mock<IAdapterMessageLogger>().Object);

        deployed.Should().BeFalse();
    }

    public void DeployShouldNotFailWhenTheTestsAreNoLongerAvailableAfterCheckingTheDeploymentItems()
    {
        TestDeployment testDeployment = CreateTestDeployment(out TestRunDirectories testRunDirectories);
        var tests = new SingleEnumerationTests(CreateTestWithDeploymentItems());

        bool deployed = testDeployment.Deploy(tests, new DeploymentContext(testRunDirectories.RootDeploymentDirectory, null), new Mock<IAdapterMessageLogger>().Object);

        // Without a first test source, the out directory is below the deployment root.
        deployed.Should().BeTrue();
        testDeployment.GetDeploymentDirectory().Should().Be(Path.Combine(testRunDirectories.RootDeploymentDirectory, TestRunDirectories.DeploymentOutDirectorySuffix));
    }

    public void DeployShouldLogWhenDeploymentIsDisabledAndTheTestsHaveDeploymentItems()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        LoadSettings("<DeploymentEnabled>False</DeploymentEnabled>");
        TestDeployment testDeployment = CreateTestDeployment(out TestRunDirectories testRunDirectories);

        bool deployed = testDeployment.Deploy([CreateTestWithDeploymentItems()], new DeploymentContext(testRunDirectories.RootDeploymentDirectory, null), new Mock<IAdapterMessageLogger>().Object);

        deployed.Should().BeFalse();
        _provider.MockTraceLogger.Verify(logger => logger.Info("MSTestExecutor: CanDeploy is false."), Times.Once);
    }

    public void DeployShouldNotLogWhenDeploymentIsDisabledAndInfoLoggingIsDisabled()
    {
        LoadSettings("<DeploymentEnabled>False</DeploymentEnabled>");
        TestDeployment testDeployment = CreateTestDeployment(out TestRunDirectories testRunDirectories);

        bool deployed = testDeployment.Deploy([CreateTestWithDeploymentItems()], new DeploymentContext(testRunDirectories.RootDeploymentDirectory, null), new Mock<IAdapterMessageLogger>().Object);

        deployed.Should().BeFalse();
        _provider.MockTraceLogger.Verify(logger => logger.Info("MSTestExecutor: CanDeploy is false."), Times.Never);
    }

    public void GetDeploymentInformationShouldReturnEmptyDirectoriesWhenThereAreNoRunDirectoriesAndNoSource()
    {
        TestDeployment.GetDeploymentInformation(null).Should().BeEquivalentTo(CreateDeploymentInformation(string.Empty));
        TestDeployment.GetDeploymentInformation(string.Empty).Should().BeEquivalentTo(CreateDeploymentInformation(string.Empty));
    }

#if DEBUG && !NETFRAMEWORK
    public void DeployShouldReportMissingTests()
    {
        TestDeployment testDeployment = CreateTestDeployment(out _);
        var assertions = new List<string?>();

        using (new DiagnosticAssertionObserver(assertions.Add))
        {
            Action deploy = () => testDeployment.Deploy(null!, new DeploymentContext(null, null), new Mock<IAdapterMessageLogger>().Object);
            deploy.Should().Throw<ArgumentNullException>();
        }

        assertions.Should().Equal("tests");
    }

    public void CanDeployShouldReportMissingAdapterSettings()
    {
        TestDeployment testDeployment = CreateTestDeployment(out _);
        MethodInfo canDeploy = typeof(TestDeployment).GetMethod("CanDeploy", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var assertions = new List<string?>();

        using (new DiagnosticAssertionObserver(assertions.Add))
        {
            Action invoke = () => canDeploy.Invoke(testDeployment, null);
            invoke.Should().Throw<TargetInvocationException>().WithInnerException<NullReferenceException>();
        }

        assertions.Should().Equal("Adapter settings should not be null.");
    }
#endif

    private static void LoadSettings(string runSettingsXml)
    {
        using StringReader stringReader = new(runSettingsXml);
        using var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        new MSTestSettingsProvider().Load(reader);
    }

    private static UnitTestElement CreateTestWithDeploymentItems()
        => new(new TestMethod("M", "C", TestSource, displayName: null))
        {
            DeploymentItems = [new KeyValuePair<string, string>(DeploymentItemPath, DeploymentItemOutputDirectory)],
        };

    private static UnitTestElement CreateTestWithoutDeploymentItems()
        => new(new TestMethod("M", "C", TestSource, displayName: null))
        {
            DeploymentItems = null,
        };

    private static Dictionary<string, object> CreateDeploymentInformation(string directory)
        => new()
        {
            [TestContext.TestRunDirectoryLabel] = directory,
            [TestContext.DeploymentDirectoryLabel] = directory,
            [TestContext.ResultsDirectoryLabel] = directory,
            [TestContext.TestRunResultsDirectoryLabel] = directory,
            [TestContext.TestResultsDirectoryLabel] = directory,
        };

    private static void DeployATestWithDeploymentItems(TestDeployment testDeployment, TestRunDirectories testRunDirectories)
        => testDeployment.Deploy([CreateTestWithDeploymentItems()], new DeploymentContext(testRunDirectories.RootDeploymentDirectory, null), new Mock<IAdapterMessageLogger>().Object).Should().BeTrue();

    private TestDeployment CreateTestDeployment(out TestRunDirectories testRunDirectories)
    {
        string currentExecutingFolder = Path.GetDirectoryName(TestSource)!;

        const bool isAppDomainCreationDisabled =
#if NETFRAMEWORK
            false;
#else
            true;
#endif

        testRunDirectories = new TestRunDirectories(currentExecutingFolder, Path.Combine(currentExecutingFolder, "asm.dll"), isAppDomainCreationDisabled);

        _mockFileUtility.Setup(utility => utility.DoesDirectoryExist(It.Is<string>(directory => !directory.EndsWith(".dll")))).Returns(true);
        _mockFileUtility.Setup(utility => utility.DoesFileExist(It.IsAny<string>())).Returns(true);
        _mockFileUtility.Setup(utility => utility.GetNextIterationDirectoryName(It.IsAny<string>(), It.IsAny<string>())).Returns(testRunDirectories.RootDeploymentDirectory);

        var mockAssemblyUtility = new Mock<AssemblyUtility>();
#if NETFRAMEWORK
        mockAssemblyUtility.Setup(utility => utility.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out _warnings)).Returns(Array.Empty<string>());
        mockAssemblyUtility.Setup(utility => utility.GetSatelliteAssemblies(It.IsAny<string>())).Returns([]);
#endif

        var deploymentItemUtility = new DeploymentItemUtility(_mockReflectionOperations.Object);

        return new TestDeployment(
            deploymentItemUtility,
            new DeploymentUtility(deploymentItemUtility, mockAssemblyUtility.Object, _mockFileUtility.Object),
            _mockFileUtility.Object);
    }

    // The sequence can be walked only once, like a query over a changing data source.
    private sealed class SingleEnumerationTests(UnitTestElement test) : IEnumerable<UnitTestElement>
    {
        private bool _enumerated;

        public IEnumerator<UnitTestElement> GetEnumerator()
        {
            UnitTestElement[] tests = _enumerated ? [] : [test];
            _enumerated = true;
            return tests.AsEnumerable().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
#endif
