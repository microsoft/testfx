// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

using Moq;

using MSTestAdapter.PlatformServices.UnitTests.Utilities;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public class DesktopTestDeploymentTests : TestContainer
{
    private const string DefaultDeploymentItemPath = @"c:\temp";
    private const string DefaultDeploymentItemOutputDirectory = "out";

    private readonly Mock<IReflectionOperations> _mockReflectionOperations;
    private readonly Mock<FileUtility> _mockFileUtility;

#pragma warning disable IDE0052 // Remove unread private members
    private IList<string> _warnings;
#pragma warning restore IDE0052 // Remove unread private members

    public DesktopTestDeploymentTests()
    {
        _mockReflectionOperations = new Mock<IReflectionOperations>();
        _mockFileUtility = new Mock<FileUtility>();
        _warnings = [];

        // Reset adapter settings.
        MSTestSettingsProvider.Reset();
    }

    #region Deploy tests

    public void DeployShouldDeployFilesInASourceAndReturnTrue()
    {
        UnitTestElement testCase = GetTestCase(Assembly.GetExecutingAssembly().Location);

        // Setup mocks.
        TestDeployment testDeployment = CreateAndSetupDeploymentRelatedUtilities(out TestRunDirectories testRunDirectories);

        testDeployment.Deploy(new List<UnitTestElement> { testCase }, new DeploymentContext(testRunDirectories.RootDeploymentDirectory, null), new Mock<IAdapterMessageLogger>().Object).Should().BeTrue();

        string? warning;
        string sourceFile = Assembly.GetExecutingAssembly().GetName().Name + ".exe";
        _mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(sourceFile)),
                Path.Combine(testRunDirectories.OutDirectory, sourceFile),
                out warning),
            Times.Once);
    }

    public void DeployShouldDeployFilesInMultipleSourcesAndReturnTrue()
    {
        UnitTestElement testCase1 = GetTestCase(Assembly.GetExecutingAssembly().Location);
        string sourceFile2 = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "a.dll");
        UnitTestElement testCase2 = GetTestCase(sourceFile2);

        // Setup mocks.
        TestDeployment testDeployment = CreateAndSetupDeploymentRelatedUtilities(out TestRunDirectories testRunDirectories);

        testDeployment.Deploy(new List<UnitTestElement> { testCase1, testCase2 }, new DeploymentContext(testRunDirectories.RootDeploymentDirectory, null), new Mock<IAdapterMessageLogger>().Object).Should().BeTrue();

        string? warning;
        string sourceFile1 = Assembly.GetExecutingAssembly().GetName().Name + ".exe";
        _mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(sourceFile1)),
                Path.Combine(testRunDirectories.OutDirectory, sourceFile1),
                out warning),
            Times.Once);
        _mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains("a.dll")),
                Path.Combine(testRunDirectories.OutDirectory, "a.dll"),
                out warning),
            Times.Once);
    }

    public void DeployShouldCreateDeploymentDirectories()
    {
        UnitTestElement testCase = GetTestCase(typeof(DesktopTestDeploymentTests).Assembly.Location);

        // Setup mocks.
        TestDeployment testDeployment = CreateAndSetupDeploymentRelatedUtilities(out TestRunDirectories testRunDirectories);

        testDeployment.Deploy(new List<UnitTestElement> { testCase }, new DeploymentContext(testRunDirectories.RootDeploymentDirectory, null), new Mock<IAdapterMessageLogger>().Object).Should().BeTrue();

        // matched twice because root deployment and out directory are same in net core
        _mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(testRunDirectories.RootDeploymentDirectory), Times.Once);
    }

    #endregion

    #region private methods

    private static UnitTestElement GetTestCase(string source)
    {
        var testCase = new UnitTestElement(new TestMethod("M", "C", source, displayName: null));
        KeyValuePair<string, string>[] kvpArray =
        [
            new KeyValuePair<string, string>(
                        DefaultDeploymentItemPath,
                        DefaultDeploymentItemOutputDirectory)
        ];
        testCase.DeploymentItems = kvpArray;

        return testCase;
    }

    private TestDeployment CreateAndSetupDeploymentRelatedUtilities(out TestRunDirectories testRunDirectories)
    {
        string currentExecutingFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        testRunDirectories = new TestRunDirectories(currentExecutingFolder, Path.Combine(currentExecutingFolder, "asm.dll"), isAppDomainCreationDisabled: false);

        _mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !s.EndsWith(".dll") && !s.EndsWith(".exe")))).Returns(true);
        _mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
        var mockAssemblyUtility = new Mock<AssemblyUtility>();
        mockAssemblyUtility.Setup(
            au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out _warnings))
            .Returns(Array.Empty<string>());
        mockAssemblyUtility.Setup(
            au => au.GetSatelliteAssemblies(It.IsAny<string>()))
            .Returns([]);
        _mockFileUtility.Setup(fu => fu.GetNextIterationDirectoryName(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(testRunDirectories.RootDeploymentDirectory);

        var deploymentItemUtility = new DeploymentItemUtility(_mockReflectionOperations.Object);

        return new TestDeployment(
            deploymentItemUtility,
            new DeploymentUtility(deploymentItemUtility, mockAssemblyUtility.Object, _mockFileUtility.Object),
            _mockFileUtility.Object);
    }
    #endregion
}

#endif
