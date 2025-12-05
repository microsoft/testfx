// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

using Moq;

using MSTestAdapter.PlatformServices.UnitTests.Utilities;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public class TestDeploymentTests : TestContainer
{
    private const string DefaultDeploymentItemPath = @"c:\temp";
    private const string DefaultDeploymentItemOutputDirectory = "out";

    private readonly Mock<ReflectHelper> _mockReflectHelper;
    private readonly Mock<FileUtility> _mockFileUtility;

#pragma warning disable IDE0044 // Add readonly modifier
    private IList<string> _warnings;
#pragma warning restore IDE0044 // Add readonly modifier

    public TestDeploymentTests()
    {
        _mockReflectHelper = new Mock<ReflectHelper>();
        _mockFileUtility = new Mock<FileUtility>();
        _warnings = [];

        // Reset adapter settings.
        MSTestSettingsProvider.Reset();
    }

    #region GetDeploymentItems tests.

    public void GetDeploymentItemsReturnsNullWhenNoDeploymentItems()
    {
        MethodInfo methodInfo = typeof(TestDeploymentTests).GetMethod("GetDeploymentItemsReturnsNullWhenNoDeploymentItems")!;
        new TestDeployment().GetDeploymentItems(methodInfo, typeof(TestDeploymentTests), _warnings).Should().BeNull();
    }

    public void GetDeploymentItemsReturnsDeploymentItems()
    {
        // Arrange.
        var testDeployment = new TestDeployment(new DeploymentItemUtility(_mockReflectHelper.Object), null!, null!);

        // setup mocks
        KeyValuePair<string, string>[] methodLevelDeploymentItems =
        [
            new KeyValuePair<string, string>(
                DefaultDeploymentItemPath,
                DefaultDeploymentItemOutputDirectory)
        ];
        KeyValuePair<string, string>[] classLevelDeploymentItems =
        [
            new KeyValuePair<string, string>(
                DefaultDeploymentItemPath + "\\temp2",
                DefaultDeploymentItemOutputDirectory)
        ];
        MethodInfo memberInfo = typeof(TestDeploymentTests).GetMethod("GetDeploymentItemsReturnsDeploymentItems")!;
        SetupDeploymentItems(memberInfo, methodLevelDeploymentItems);
        SetupDeploymentItems(typeof(TestDeploymentTests), classLevelDeploymentItems);

        // Act.
        KeyValuePair<string, string>[]? deploymentItems = testDeployment.GetDeploymentItems(memberInfo, typeof(TestDeploymentTests), _warnings);

        // Assert.
        var expectedDeploymentItems = new KeyValuePair<string, string>[]
        {
            new(
                DefaultDeploymentItemPath,
                DefaultDeploymentItemOutputDirectory),
            new(
                DefaultDeploymentItemPath + "\\temp2",
                DefaultDeploymentItemOutputDirectory),
        };

        deploymentItems.Should().BeEquivalentTo(expectedDeploymentItems);
    }

    #endregion

    #region Cleanup tests

    public void CleanupShouldNotDeleteDirectoriesIfRunDirectoriesIsNull()
    {
        var testDeployment = new TestDeployment(null!, null!, _mockFileUtility.Object);

        testDeployment.Cleanup();

        _mockFileUtility.Verify(fu => fu.DeleteDirectories(It.IsAny<string>()), Times.Never);
    }

    public void CleanupShouldNotDeleteDirectoriesIfRunSettingsSpecifiesSo()
    {
        string runSettingsXml =
            "<DeleteDeploymentDirectoryAfterTestRunIsComplete>False</DeleteDeploymentDirectoryAfterTestRunIsComplete>";
        StringReader stringReader = new(runSettingsXml);
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        MSTestSettingsProvider mstestSettingsProvider = new();
        mstestSettingsProvider.Load(reader);

        TestCase testCase = GetTestCase(typeof(TestDeploymentTests).Assembly.Location);

        // Setup mocks.
        TestDeployment testDeployment = CreateAndSetupDeploymentRelatedUtilities(out TestRunDirectories testRunDirectories);

        var mockRunContext = new Mock<IRunContext>();
        mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

        testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object).Should().BeTrue();

        testDeployment.Cleanup();

        _mockFileUtility.Verify(fu => fu.DeleteDirectories(It.IsAny<string>()), Times.Never);
    }

    public void CleanupShouldDeleteRootDeploymentDirectory()
    {
        TestCase testCase = GetTestCase(typeof(DeploymentUtilityTests).Assembly.Location);

        // Setup mocks.
        TestDeployment testDeployment = CreateAndSetupDeploymentRelatedUtilities(out TestRunDirectories testRunDirectories);

        var mockRunContext = new Mock<IRunContext>();
        mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

        testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object).Should().BeTrue();

        // Act.
        testDeployment.Cleanup();

        _mockFileUtility.Verify(fu => fu.DeleteDirectories(testRunDirectories.RootDeploymentDirectory), Times.Once);
    }

    #endregion

    #region GetDeploymentDirectory tests

    public void GetDeploymentDirectoryShouldReturnNullIfDeploymentDirectoryIsNull() => new TestDeployment().GetDeploymentDirectory().Should().BeNull();

    public void GetDeploymentDirectoryShouldReturnDeploymentOutputDirectory()
    {
        TestCase testCase = GetTestCase(typeof(TestDeploymentTests).Assembly.Location);

        // Setup mocks.
        TestDeployment testDeployment = CreateAndSetupDeploymentRelatedUtilities(out TestRunDirectories testRunDirectories);

        var mockRunContext = new Mock<IRunContext>();
        mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

        testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object).Should().BeTrue();

        // Act.
        testDeployment.GetDeploymentDirectory().Should().Be(testRunDirectories.OutDirectory);
    }

    #endregion

    #region Deploy tests

    public void DeployShouldReturnFalseWhenDeploymentEnabledSetToFalseButHasDeploymentItems()
    {
        var testCase = new TestCase("A.C.M", new Uri("executor://testExecutor"), "path/to/asm.dll");
        KeyValuePair<string, string>[] kvpArray =
        [
            new KeyValuePair<string, string>(
                DefaultDeploymentItemPath,
                DefaultDeploymentItemOutputDirectory)
        ];
        testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, kvpArray);

        var testDeployment = new TestDeployment(
            new DeploymentItemUtility(_mockReflectHelper.Object),
            new DeploymentUtility(),
            _mockFileUtility.Object);

        string runSettingsXml =
             "<DeploymentEnabled>False</DeploymentEnabled>";
        StringReader stringReader = new(runSettingsXml);
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        MSTestSettingsProvider mstestSettingsProvider = new();
        mstestSettingsProvider.Load(reader);

        // Deployment should not happen
        testDeployment.Deploy(new List<TestCase> { testCase }, null, null!).Should().BeFalse();

        // Deployment directories should not be created
        testDeployment.GetDeploymentDirectory().Should().BeNull();
    }

    public void DeployShouldReturnFalseWhenDeploymentEnabledSetToFalseAndHasNoDeploymentItems()
    {
        var testCase = new TestCase("A.C.M", new Uri("executor://testExecutor"), "path/to/asm.dll");
        testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, null);
        var testDeployment = new TestDeployment(
            new DeploymentItemUtility(_mockReflectHelper.Object),
            new DeploymentUtility(),
            _mockFileUtility.Object);

        string runSettingsXml =
            "<DeploymentEnabled>False</DeploymentEnabled>";
        StringReader stringReader = new(runSettingsXml);
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        MSTestSettingsProvider mstestSettingsProvider = new();
        mstestSettingsProvider.Load(reader);

        // Deployment should not happen
        testDeployment.Deploy(new List<TestCase> { testCase }, null, null!).Should().BeFalse();

        // Deployment directories should get created
        testDeployment.GetDeploymentDirectory().Should().NotBeNull();
    }

    public void DeployShouldReturnFalseWhenDeploymentEnabledSetToTrueButHasNoDeploymentItems()
    {
        var testCase = new TestCase("A.C.M", new Uri("executor://testExecutor"), "path/to/asm.dll");
        testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, null);
        var testDeployment = new TestDeployment(
            new DeploymentItemUtility(_mockReflectHelper.Object),
            new DeploymentUtility(),
            _mockFileUtility.Object);

        string runSettingsXml =
            "<DeploymentEnabled>True</DeploymentEnabled>";
        StringReader stringReader = new(runSettingsXml);
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        MSTestSettingsProvider mstestSettingsProvider = new();
        mstestSettingsProvider.Load(reader);

        // Deployment should not happen
        testDeployment.Deploy(new List<TestCase> { testCase }, null, null!).Should().BeFalse();

        // Deployment directories should get created
        testDeployment.GetDeploymentDirectory().Should().NotBeNull();
    }

    // TODO: This test has to have mocks. It actually deploys stuff and we cannot assume that all the dependencies get copied over to bin\debug.
    internal void DeployShouldReturnTrueWhenDeploymentEnabledSetToTrueAndHasDeploymentItems()
    {
        var testCase = new TestCase("A.C.M", new Uri("executor://testExecutor"), typeof(TestDeploymentTests).Assembly.Location);
        KeyValuePair<string, string>[] kvpArray =
        [
            new KeyValuePair<string, string>(
                        DefaultDeploymentItemPath,
                        DefaultDeploymentItemOutputDirectory)
        ];
        testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, kvpArray);
        var testDeployment = new TestDeployment(
            new DeploymentItemUtility(_mockReflectHelper.Object),
            new DeploymentUtility(),
            _mockFileUtility.Object);

        string runSettingsXml =
            "<DeploymentEnabled>True</DeploymentEnabled>";
        StringReader stringReader = new(runSettingsXml);
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        MSTestSettingsProvider mstestSettingsProvider = new();
        mstestSettingsProvider.Load(reader);

        // Deployment should happen
        testDeployment.Deploy(new List<TestCase> { testCase }, null, new Mock<IFrameworkHandle>().Object).Should().BeTrue();

        // Deployment directories should get created
        testDeployment.GetDeploymentDirectory().Should().NotBeNull();
    }

    #endregion

    #region GetDeploymentInformation tests

    public void GetDeploymentInformationShouldReturnAppBaseDirectoryIfRunDirectoryIsNull()
    {
        TestDeployment.Reset();
        IDictionary<string, object> properties = TestDeployment.GetDeploymentInformation(typeof(TestDeploymentTests).Assembly.Location);

        string applicationBaseDirectory = Path.GetDirectoryName(typeof(TestDeploymentTests).Assembly.Location)!;
        var expectedProperties = new Dictionary<string, object>
        {
            [TestContext.TestRunDirectoryLabel] = applicationBaseDirectory,
            [TestContext.DeploymentDirectoryLabel] = applicationBaseDirectory,
            [TestContext.ResultsDirectoryLabel] = applicationBaseDirectory,
            [TestContext.TestRunResultsDirectoryLabel] = applicationBaseDirectory,
            [TestContext.TestResultsDirectoryLabel] = applicationBaseDirectory,
        };
        properties.Should().NotBeNull();
        properties.Should().BeEquivalentTo(expectedProperties);
    }

    public void GetDeploymentInformationShouldReturnRunDirectoryInformationIfSourceIsNull()
    {
        // Arrange.
        TestCase testCase = GetTestCase(typeof(TestDeploymentTests).Assembly.Location);

        // Setup mocks.
        TestDeployment testDeployment = CreateAndSetupDeploymentRelatedUtilities(out TestRunDirectories testRunDirectories);

        var mockRunContext = new Mock<IRunContext>();
        mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

        testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object).Should().BeTrue();

        // Act.
        IDictionary<string, object> properties = TestDeployment.GetDeploymentInformation(null);

        // Assert.
        var expectedProperties = new Dictionary<string, object>
        {
            [TestContext.TestRunDirectoryLabel] = testRunDirectories.RootDeploymentDirectory,
            [TestContext.DeploymentDirectoryLabel] = testRunDirectories.OutDirectory,
            [TestContext.ResultsDirectoryLabel] = testRunDirectories.InDirectory,
            [TestContext.TestRunResultsDirectoryLabel] = testRunDirectories.InMachineNameDirectory,
            [TestContext.TestResultsDirectoryLabel] = testRunDirectories.InDirectory,
        };

        properties.Should().NotBeNull();
        properties.Should().BeEquivalentTo(expectedProperties);
    }

    public void GetDeploymentInformationShouldReturnRunDirectoryInformationIfSourceIsNotNull()
    {
        // Arrange.
        TestCase testCase = GetTestCase(typeof(TestDeploymentTests).Assembly.Location);

        // Setup mocks.
        TestDeployment testDeployment = CreateAndSetupDeploymentRelatedUtilities(out TestRunDirectories testRunDirectories);

        var mockRunContext = new Mock<IRunContext>();
        mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

        testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object).Should().BeTrue();

        // Act.
        IDictionary<string, object> properties = TestDeployment.GetDeploymentInformation(typeof(TestDeploymentTests).Assembly.Location);

        // Assert.
        var expectedProperties = new Dictionary<string, object>
        {
            [TestContext.TestRunDirectoryLabel] = testRunDirectories.RootDeploymentDirectory,
            [TestContext.DeploymentDirectoryLabel] = testRunDirectories.OutDirectory,
            [TestContext.ResultsDirectoryLabel] = testRunDirectories.InDirectory,
            [TestContext.TestRunResultsDirectoryLabel] = testRunDirectories.InMachineNameDirectory,
            [TestContext.TestResultsDirectoryLabel] = testRunDirectories.InDirectory,
        };

        properties.Should().NotBeNull();
        properties.Should().BeEquivalentTo(expectedProperties);
    }

    #endregion

    #region private methods

    private void SetupDeploymentItems(MemberInfo memberInfo, KeyValuePair<string, string>[] deploymentItems)
    {
        var deploymentItemAttributes = new List<DeploymentItemAttribute>();

        foreach (KeyValuePair<string, string> deploymentItem in deploymentItems)
        {
            deploymentItemAttributes.Add(new DeploymentItemAttribute(deploymentItem.Key, deploymentItem.Value));
        }

        _mockReflectHelper.Setup(
            rh =>
            rh.GetAttributes<DeploymentItemAttribute>(memberInfo)).Returns(deploymentItemAttributes);
    }

    private static TestCase GetTestCase(string source)
    {
        var testCase = new TestCase("A.C.M", new Uri("executor://testExecutor"), source);
        KeyValuePair<string, string>[] kvpArray =
        [
            new KeyValuePair<string, string>(
                DefaultDeploymentItemPath,
                DefaultDeploymentItemOutputDirectory)
        ];
        testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, kvpArray);

        return testCase;
    }

    private TestDeployment CreateAndSetupDeploymentRelatedUtilities(out TestRunDirectories testRunDirectories)
    {
        string currentExecutingFolder = Path.GetDirectoryName(typeof(TestDeploymentTests).Assembly.Location)!;

        const bool isAppDomainCreationDisabled =
#if NETFRAMEWORK
            false;
#else
            true;
#endif

        testRunDirectories = new TestRunDirectories(currentExecutingFolder, Path.Combine(currentExecutingFolder, "asm.dll"), isAppDomainCreationDisabled);

        _mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !s.EndsWith(".dll")))).Returns(true);
        _mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
        var mockAssemblyUtility = new Mock<AssemblyUtility>();
#if NETFRAMEWORK
        mockAssemblyUtility.Setup(
           au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out _warnings))
           .Returns(Array.Empty<string>());
        mockAssemblyUtility.Setup(
            au => au.GetSatelliteAssemblies(It.IsAny<string>()))
            .Returns([]);
#endif
        _mockFileUtility.Setup(fu => fu.GetNextIterationDirectoryName(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(testRunDirectories.RootDeploymentDirectory);

        var deploymentItemUtility = new DeploymentItemUtility(_mockReflectHelper.Object);

        return new TestDeployment(
            deploymentItemUtility,
            new DeploymentUtility(deploymentItemUtility, mockAssemblyUtility.Object, _mockFileUtility.Object),
            _mockFileUtility.Object);
    }
    #endregion
}
#endif
