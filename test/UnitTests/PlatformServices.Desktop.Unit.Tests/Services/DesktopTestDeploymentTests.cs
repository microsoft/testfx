// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
namespace MSTestAdapter.PlatformServices.Desktop.UnitTests.Services;

extern alias FrameworkV1;
extern alias FrameworkV2;
extern alias FrameworkV2Extension;

using System.Collections.Generic;
using System.IO;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

using Moq;

using MSTestAdapter.PlatformServices.Tests.Utilities;

using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
using Ignore = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.IgnoreAttribute;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestFrameworkV2Extension = FrameworkV2Extension::Microsoft.VisualStudio.TestTools.UnitTesting;
using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

[TestClass]
public class DesktopTestDeploymentTests
{
    private const string DefaultDeploymentItemPath = @"c:\temp";
    private const string DefaultDeploymentItemOutputDirectory = "out";

    private Mock<ReflectionUtility> _mockReflectionUtility;
    private Mock<FileUtility> _mockFileUtility;

    private IList<string> _warnings;

    [TestInitialize]
    public void TestInit()
    {
        _mockReflectionUtility = new Mock<ReflectionUtility>();
        _mockFileUtility = new Mock<FileUtility>();
        _warnings = new List<string>();

        // Reset adapter settings.
        MSTestSettingsProvider.Reset();
    }

#region Deploy tests

    [TestMethod]
    public void DeployShouldDeployFilesInASourceAndReturnTrue()
    {
        var testCase = GetTestCase(Assembly.GetExecutingAssembly().Location);

        // Setup mocks.
        var testDeployment = CreateAndSetupDeploymentRelatedUtilities(out var testRunDirectories);

        var mockRunContext = new Mock<IRunContext>();
        mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

        Assert.IsTrue(testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object));

        string warning;
        var sourceFile = Assembly.GetExecutingAssembly().GetName().Name + ".dll";
        _mockFileUtility.Verify(
            fu =>
            fu.CopyFileOverwrite(
                It.Is<string>(s => s.Contains(sourceFile)),
                Path.Combine(testRunDirectories.OutDirectory, sourceFile),
                out warning),
            Times.Once);
    }

    [TestMethod]
    public void DeployShouldDeployFilesInMultipleSourcesAndReturnTrue()
    {
        var testCase1 = GetTestCase(Assembly.GetExecutingAssembly().Location);
        var sourceFile2 = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "a.dll");
        var testCase2 = GetTestCase(sourceFile2);

        // Setup mocks.
        var testDeployment = CreateAndSetupDeploymentRelatedUtilities(out var testRunDirectories);

        var mockRunContext = new Mock<IRunContext>();
        mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

        Assert.IsTrue(testDeployment.Deploy(new List<TestCase> { testCase1, testCase2 }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object));

        string warning;
        var sourceFile1 = Assembly.GetExecutingAssembly().GetName().Name + ".dll";
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

    [TestMethod]
    public void DeployShouldCreateDeploymentDirectories()
    {
        var testCase = GetTestCase(typeof(DesktopTestDeploymentTests).GetTypeInfo().Assembly.Location);

        // Setup mocks.
        var testDeployment = CreateAndSetupDeploymentRelatedUtilities(out var testRunDirectories);

        var mockRunContext = new Mock<IRunContext>();
        mockRunContext.Setup(rc => rc.TestRunDirectory).Returns(testRunDirectories.RootDeploymentDirectory);

        Assert.IsTrue(testDeployment.Deploy(new List<TestCase> { testCase }, mockRunContext.Object, new Mock<IFrameworkHandle>().Object));

        // matched twice because root deployment and out directory are same in net core
        _mockFileUtility.Verify(fu => fu.CreateDirectoryIfNotExists(testRunDirectories.RootDeploymentDirectory), Times.Once);
    }

#endregion

#region private methods

    private void SetupDeploymentItems(MemberInfo memberInfo, KeyValuePair<string, string>[] deploymentItems)
    {
        var deploymentItemAttributes = new List<TestFrameworkV2Extension.DeploymentItemAttribute>();

        foreach (var deploymentItem in deploymentItems)
        {
            deploymentItemAttributes.Add(new TestFrameworkV2Extension.DeploymentItemAttribute(deploymentItem.Key, deploymentItem.Value));
        }

        _mockReflectionUtility.Setup(
            ru =>
            ru.GetCustomAttributes(
                memberInfo,
                typeof(TestFrameworkV2Extension.DeploymentItemAttribute))).Returns((object[])deploymentItemAttributes.ToArray());
    }

    private TestCase GetTestCase(string source)
    {
        var testCase = new TestCase("A.C.M", new System.Uri("executor://testExecutor"), source);
        var kvpArray = new[]
                {
                    new KeyValuePair<string, string>(
                        DefaultDeploymentItemPath,
                        DefaultDeploymentItemOutputDirectory)
                };
        testCase.SetPropertyValue(DeploymentItemUtilityTests.DeploymentItemsProperty, kvpArray);

        return testCase;
    }

    private TestDeployment CreateAndSetupDeploymentRelatedUtilities(out TestRunDirectories testRunDirectories)
    {
        var currentExecutingFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        testRunDirectories = new TestRunDirectories(currentExecutingFolder);

        _mockFileUtility.Setup(fu => fu.DoesDirectoryExist(It.Is<string>(s => !s.EndsWith(".dll")))).Returns(true);
        _mockFileUtility.Setup(fu => fu.DoesFileExist(It.IsAny<string>())).Returns(true);
        var mockAssemblyUtility = new Mock<AssemblyUtility>();
        mockAssemblyUtility.Setup(
            au => au.GetFullPathToDependentAssemblies(It.IsAny<string>(), It.IsAny<string>(), out _warnings))
            .Returns(System.Array.Empty<string>());
        mockAssemblyUtility.Setup(
            au => au.GetSatelliteAssemblies(It.IsAny<string>()))
            .Returns(new List<string> { });
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
