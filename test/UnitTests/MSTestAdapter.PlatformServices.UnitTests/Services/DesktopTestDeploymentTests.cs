// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using MSTestAdapter.PlatformServices.Tests.Utilities;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public class DesktopTestDeploymentTests : TestContainer
{
    private const string DefaultDeploymentItemPath = @"c:\temp";
    private const string DefaultDeploymentItemOutputDirectory = "out";

    private readonly Mock<ReflectionUtility> _mockReflectionUtility;
    private readonly Mock<FileUtility> _mockFileUtility;

#pragma warning disable IDE0052 // Remove unread private members
    private IList<string> _warnings;
#pragma warning restore IDE0052 // Remove unread private members

    public DesktopTestDeploymentTests()
    {
        _mockReflectionUtility = new Mock<ReflectionUtility>();
        _mockFileUtility = new Mock<FileUtility>();
        _warnings = new List<string>();

        // Reset adapter settings.
        MSTestSettingsProvider.Reset();
    }

    #region Deploy tests

    public void DeployShouldDeployFilesInASourceAndReturnTrue()
    {
        TestCase testCase = GetTestCase(Assembly.GetExecutingAssembly().Location);

        // Setup mocks.
        TestDeployment testDeployment = CreateAndSetupDeploymentRelatedUtilities(out TestRunDirectories testRunDirectories);

        var mockRunContext = new Mock<IRunContext>();
        mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

        Verify(testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object));

        string warning;
        string sourceFile = Assembly.GetExecutingAssembly().GetName().Name + ".dll";
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
        TestCase testCase1 = GetTestCase(Assembly.GetExecutingAssembly().Location);
        string sourceFile2 = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "a.dll");
        TestCase testCase2 = GetTestCase(sourceFile2);

        // Setup mocks.
        TestDeployment testDeployment = CreateAndSetupDeploymentRelatedUtilities(out TestRunDirectories testRunDirectories);

        var mockRunContext = new Mock<IRunContext>();
        mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

        Verify(testDeployment.Deploy(new List<TestCase> { testCase1, testCase2 }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object));

        string warning;
        string sourceFile1 = Assembly.GetExecutingAssembly().GetName().Name + ".dll";
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
        TestCase testCase = GetTestCase(typeof(DesktopTestDeploymentTests).Assembly.Location);

        // Setup mocks.
        TestDeployment testDeployment = CreateAndSetupDeploymentRelatedUtilities(out TestRunDirectories testRunDirectories);

        var mockRunContext = new Mock<IRunContext>();
        mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

        Verify(testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object));

        // matched twice because root deployment and out directory are same in net core
        _mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(testRunDirectories.RootDeploymentDirectory), Times.Once);
    }

    #endregion

    #region private methods

#pragma warning disable IDE0051 // Remove unused private members
    private void SetupDeploymentItems(MemberInfo memberInfo, KeyValuePair<string, string>[] deploymentItems)
#pragma warning restore IDE0051 // Remove unused private members
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

    private TestCase GetTestCase(string source)
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
        string currentExecutingFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        testRunDirectories = new TestRunDirectories(currentExecutingFolder);

        _mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !s.EndsWith(".dll")))).Returns(true);
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

        var deploymentItemUtility = new DeploymentItemUtility(_mockReflectionUtility.Object);

        return new TestDeployment(
            deploymentItemUtility,
            new DeploymentUtility(deploymentItemUtility, mockAssemblyUtility.Object, _mockFileUtility.Object),
            _mockFileUtility.Object);
    }
    #endregion
}

#endif
