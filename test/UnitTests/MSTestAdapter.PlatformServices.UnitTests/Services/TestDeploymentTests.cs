// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Xml;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using MSTestAdapter.PlatformServices.Tests.Utilities;
using MSTestAdapter.PlatformServices.UnitTests.Utilities;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.Tests.Services;

public class TestDeploymentTests : TestContainer
{
    private const string DefaultDeploymentItemPath = @"c:\temp";
    private const string DefaultDeploymentItemOutputDirectory = "out";

    private readonly Mock<ReflectionUtility> _mockReflectionUtility;
    private readonly Mock<FileUtility> _mockFileUtility;

#pragma warning disable IDE0044 // Add readonly modifier
    private IList<string> _warnings;
#pragma warning restore IDE0044 // Add readonly modifier

    public TestDeploymentTests()
    {
        _mockReflectionUtility = new Mock<ReflectionUtility>();
        _mockFileUtility = new Mock<FileUtility>();
        _warnings = new List<string>();

        // Reset adapter settings.
        MSTestSettingsProvider.Reset();
    }

    #region GetDeploymentItems tests.

    public void GetDeploymentItemsReturnsNullWhenNoDeploymentItems()
    {
        MethodInfo methodInfo =
            typeof(TestDeploymentTests).GetMethod("GetDeploymentItemsReturnsNullWhenNoDeploymentItems");

        Verify(new TestDeployment().GetDeploymentItems(methodInfo, typeof(TestDeploymentTests), _warnings) is null);
    }

    public void GetDeploymentItemsReturnsDeploymentItems()
    {
        // Arrange.
        var testDeployment = new TestDeployment(new DeploymentItemUtility(_mockReflectionUtility.Object), null, null);

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
        MethodInfo memberInfo =
            typeof(TestDeploymentTests).GetMethod(
                "GetDeploymentItemsReturnsDeploymentItems");
        SetupDeploymentItems(memberInfo, methodLevelDeploymentItems);
        SetupDeploymentItems(typeof(TestDeploymentTests), classLevelDeploymentItems);

        // Act.
        KeyValuePair<string, string>[] deploymentItems = testDeployment.GetDeploymentItems(memberInfo, typeof(TestDeploymentTests), _warnings);

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

        Verify(expectedDeploymentItems.SequenceEqual(deploymentItems));
    }

    #endregion

    #region Cleanup tests

    public void CleanupShouldNotDeleteDirectoriesIfRunDirectoriesIsNull()
    {
        var testDeployment = new TestDeployment(null, null, _mockFileUtility.Object);

        testDeployment.Cleanup();

        _mockFileUtility.Verify(fu => fu.DeleteDirectories(It.IsAny<string>()), Times.Never);
    }

    public void CleanupShouldNotDeleteDirectoriesIfRunSettingsSpecifiesSo()
    {
        string runSettingXml =
            "<DeleteDeploymentDirectoryAfterTestRunIsComplete>False</DeleteDeploymentDirectoryAfterTestRunIsComplete>";
        StringReader stringReader = new(runSettingXml);
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        MSTestSettingsProvider mstestSettingsProvider = new();
        mstestSettingsProvider.Load(reader);

        TestCase testCase = GetTestCase(typeof(TestDeploymentTests).Assembly.Location);

        // Setup mocks.
        TestDeployment testDeployment = CreateAndSetupDeploymentRelatedUtilities(out TestRunDirectories testRunDirectories);

        var mockRunContext = new Mock<IRunContext>();
        mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

        Verify(testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object));

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

        Verify(testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object));

        // Act.
        testDeployment.Cleanup();

        _mockFileUtility.Verify(fu => fu.DeleteDirectories(testRunDirectories.RootDeploymentDirectory), Times.Once);
    }

    #endregion

    #region GetDeploymentDirectory tests

    public void GetDeploymentDirectoryShouldReturnNullIfDeploymentDirectoryIsNull() => Verify(new TestDeployment().GetDeploymentDirectory() is null);

    public void GetDeploymentDirectoryShouldReturnDeploymentOutputDirectory()
    {
        TestCase testCase = GetTestCase(typeof(TestDeploymentTests).Assembly.Location);

        // Setup mocks.
        TestDeployment testDeployment = CreateAndSetupDeploymentRelatedUtilities(out TestRunDirectories testRunDirectories);

        var mockRunContext = new Mock<IRunContext>();
        mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

        Verify(testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object));

        // Act.
        Verify(testRunDirectories.OutDirectory == testDeployment.GetDeploymentDirectory());
    }

    #endregion

    #region Deploy tests

    public void DeployShouldReturnFalseWhenDeploymentEnabledSetToFalseButHasDeploymentItems()
    {
        var testCase = new TestCase("A.C.M", new Uri("executor://testExecutor"), "A");
        KeyValuePair<string, string>[] kvpArray =
        [
            new KeyValuePair<string, string>(
                DefaultDeploymentItemPath,
                DefaultDeploymentItemOutputDirectory)
        ];
        testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, kvpArray);

        var testDeployment = new TestDeployment(
            new DeploymentItemUtility(_mockReflectionUtility.Object),
            new DeploymentUtility(),
            _mockFileUtility.Object);

        string runSettingXml =
             "<DeploymentEnabled>False</DeploymentEnabled>";
        StringReader stringReader = new(runSettingXml);
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        MSTestSettingsProvider mstestSettingsProvider = new();
        mstestSettingsProvider.Load(reader);

        // Deployment should not happen
        Verify(!testDeployment.Deploy(new List<TestCase> { testCase }, null, null));

        // Deployment directories should not be created
        Verify(testDeployment.GetDeploymentDirectory() is null);
    }

    public void DeployShouldReturnFalseWhenDeploymentEnabledSetToFalseAndHasNoDeploymentItems()
    {
        var testCase = new TestCase("A.C.M", new Uri("executor://testExecutor"), "A");
        testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, null);
        var testDeployment = new TestDeployment(
            new DeploymentItemUtility(_mockReflectionUtility.Object),
            new DeploymentUtility(),
            _mockFileUtility.Object);

        string runSettingXml =
            "<DeploymentEnabled>False</DeploymentEnabled>";
        StringReader stringReader = new(runSettingXml);
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        MSTestSettingsProvider mstestSettingsProvider = new();
        mstestSettingsProvider.Load(reader);

        // Deployment should not happen
        Verify(!testDeployment.Deploy(new List<TestCase> { testCase }, null, null));

        // Deployment directories should get created
        Verify(testDeployment.GetDeploymentDirectory() is not null);
    }

    public void DeployShouldReturnFalseWhenDeploymentEnabledSetToTrueButHasNoDeploymentItems()
    {
        var testCase = new TestCase("A.C.M", new Uri("executor://testExecutor"), "A");
        testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, null);
        var testDeployment = new TestDeployment(
            new DeploymentItemUtility(_mockReflectionUtility.Object),
            new DeploymentUtility(),
            _mockFileUtility.Object);

        string runSettingXml =
            "<DeploymentEnabled>True</DeploymentEnabled>";
        StringReader stringReader = new(runSettingXml);
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        MSTestSettingsProvider mstestSettingsProvider = new();
        mstestSettingsProvider.Load(reader);

        // Deployment should not happen
        Verify(!testDeployment.Deploy(new List<TestCase> { testCase }, null, null));

        // Deployment directories should get created
        Verify(testDeployment.GetDeploymentDirectory() is not null);
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
            new DeploymentItemUtility(_mockReflectionUtility.Object),
            new DeploymentUtility(),
            _mockFileUtility.Object);

        string runSettingXml =
            "<DeploymentEnabled>True</DeploymentEnabled>";
        StringReader stringReader = new(runSettingXml);
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        MSTestSettingsProvider mstestSettingsProvider = new();
        mstestSettingsProvider.Load(reader);

        // Deployment should happen
        Verify(testDeployment.Deploy(new List<TestCase> { testCase }, null, new Mock<IFrameworkHandle>().Object));

        // Deployment directories should get created
        Verify(testDeployment.GetDeploymentDirectory() is not null);
    }

    #endregion

    #region GetDeploymentInformation tests

    public void GetDeploymentInformationShouldReturnAppBaseDirectoryIfRunDirectoryIsNull()
    {
        TestDeployment.Reset();
        IDictionary<string, object> properties = TestDeployment.GetDeploymentInformation(typeof(TestDeploymentTests).Assembly.Location);

        string applicationBaseDirectory = Path.GetDirectoryName(typeof(TestDeploymentTests).Assembly.Location);
        var expectedProperties = new Dictionary<string, object>
        {
            [TestContext.TestRunDirectoryLabel] = applicationBaseDirectory,
            [TestContext.DeploymentDirectoryLabel] = applicationBaseDirectory,
            [TestContext.ResultsDirectoryLabel] = applicationBaseDirectory,
            [TestContext.TestRunResultsDirectoryLabel] = applicationBaseDirectory,
            [TestContext.TestResultsDirectoryLabel] = applicationBaseDirectory,
#pragma warning disable CS0618 // Type or member is obsolete
            [TestContext.TestDirLabel] = applicationBaseDirectory,
            [TestContext.TestDeploymentDirLabel] = applicationBaseDirectory,
            [TestContext.TestLogsDirLabel] = applicationBaseDirectory,
#pragma warning restore CS0618 // Type or member is obsolete
        };
        Verify(properties is not null);
        Verify(expectedProperties.SequenceEqual(properties));
    }

    public void GetDeploymentInformationShouldReturnRunDirectoryInformationIfSourceIsNull()
    {
        // Arrange.
        TestCase testCase = GetTestCase(typeof(TestDeploymentTests).Assembly.Location);

        // Setup mocks.
        TestDeployment testDeployment = CreateAndSetupDeploymentRelatedUtilities(out TestRunDirectories testRunDirectories);

        var mockRunContext = new Mock<IRunContext>();
        mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

        Verify(testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object));

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
#pragma warning disable CS0618 // Type or member is obsolete
            [TestContext.TestDirLabel] = testRunDirectories.RootDeploymentDirectory,
            [TestContext.TestDeploymentDirLabel] = testRunDirectories.OutDirectory,
            [TestContext.TestLogsDirLabel] = testRunDirectories.InMachineNameDirectory,
#pragma warning restore CS0618 // Type or member is obsolete
        };

        Verify(properties is not null);
        Verify(expectedProperties.SequenceEqual(properties));
    }

    public void GetDeploymentInformationShouldReturnRunDirectoryInformationIfSourceIsNotNull()
    {
        // Arrange.
        TestCase testCase = GetTestCase(typeof(TestDeploymentTests).Assembly.Location);

        // Setup mocks.
        TestDeployment testDeployment = CreateAndSetupDeploymentRelatedUtilities(out TestRunDirectories testRunDirectories);

        var mockRunContext = new Mock<IRunContext>();
        mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

        Verify(testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object));

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
#pragma warning disable CS0618 // Type or member is obsolete
            [TestContext.TestDirLabel] = testRunDirectories.RootDeploymentDirectory,
            [TestContext.TestDeploymentDirLabel] = testRunDirectories.OutDirectory,
            [TestContext.TestLogsDirLabel] = testRunDirectories.InMachineNameDirectory,
#pragma warning restore CS0618 // Type or member is obsolete
        };

        Verify(properties is not null);
        Verify(expectedProperties.SequenceEqual(properties));
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

        _mockReflectionUtility.Setup(
            ru =>
            ru.GetCustomAttributes(
                memberInfo,
                typeof(DeploymentItemAttribute))).Returns(deploymentItemAttributes.ToArray());
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
        string currentExecutingFolder = Path.GetDirectoryName(typeof(TestDeploymentTests).Assembly.Location);

        testRunDirectories = new TestRunDirectories(currentExecutingFolder);

        _mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !s.EndsWith(".dll")))).Returns(true);
        _mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
        var mockAssemblyUtility = new Mock<AssemblyUtility>();
#if NET462
        mockAssemblyUtility.Setup(
           au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out _warnings))
           .Returns(Array.Empty<string>());
        mockAssemblyUtility.Setup(
            au => au.GetSatelliteAssemblies(It.IsAny<string>()))
            .Returns([]);
#endif
        _mockFileUtility.Setup(fu => fu.GetNextIterationDirectoryName(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(testRunDirectories.RootDeploymentDirectory);

        var deploymentItemUtility = new DeploymentItemUtility(_mockReflectionUtility.Object);

        return new TestDeployment(
            deploymentItemUtility,
            new DeploymentUtility(deploymentItemUtility, mockAssemblyUtility.Object, _mockFileUtility.Object),
            _mockFileUtility.Object);
    }
    #endregion
}
