// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
using System.Reflection;
using System.Security.Policy;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests;

public class DesktopTestSourceHostTests : TestContainer
{
    public void GetResolutionPathsShouldAddPublicAndPrivateAssemblyPath()
    {
        // Setup
        TestSourceHost sut = new(null, null, null);

        // Execute
        // It should return public and private path if it is not running in portable mode.
        List<string> result = sut.GetResolutionPaths("DummyAssembly.dll", isPortableMode: false);

        // Assert
        if (!string.IsNullOrWhiteSpace(VSInstallationUtilities.PathToPublicAssemblies))
        {
            Verify(result.Contains(VSInstallationUtilities.PathToPublicAssemblies));
        }

        if (!string.IsNullOrWhiteSpace(VSInstallationUtilities.PathToPrivateAssemblies))
        {
            Verify(result.Contains(VSInstallationUtilities.PathToPrivateAssemblies));
        }
    }

    public void GetResolutionPathsShouldNotAddPublicAndPrivateAssemblyPathInPortableMode()
    {
        // Setup
        TestSourceHost sut = new(null, null, null);

        // Execute
        // It should not return public and private path if it is running in portable mode.
        List<string> result = sut.GetResolutionPaths("DummyAssembly.dll", isPortableMode: true);

        // Assert
        Verify(!result.Contains(VSInstallationUtilities.PathToPublicAssemblies));
        Verify(!result.Contains(VSInstallationUtilities.PathToPrivateAssemblies));
    }

    public void GetResolutionPathsShouldAddAdapterFolderPath()
    {
        // Setup
        TestSourceHost sut = new(null, null, null);

        // Execute
        List<string> result = sut.GetResolutionPaths("DummyAssembly.dll", isPortableMode: false);

        // Assert
        Verify(!result.Contains(typeof(TestSourceHost).Assembly.Location));
    }

    public void GetResolutionPathsShouldAddTestPlatformFolderPath()
    {
        // Setup
        TestSourceHost sut = new(null, null, null);

        // Execute
        List<string> result = sut.GetResolutionPaths("DummyAssembly.dll", isPortableMode: false);

        // Assert
        Verify(!result.Contains(typeof(AssemblyHelper).Assembly.Location));
    }

    public void CreateInstanceForTypeShouldCreateTheTypeInANewAppDomain()
    {
        // Setup
        DummyClass dummyClass = new();
        int currentAppDomainId = dummyClass.AppDomainId;

        TestSourceHost sut = new(Assembly.GetExecutingAssembly().Location, null, null);
        sut.SetupHost();

        // Execute
        int newAppDomainId = currentAppDomainId + 10;  // not equal to currentAppDomainId
        if (sut.CreateInstanceForType(typeof(DummyClass), null) is DummyClass expectedObject)
        {
            newAppDomainId = expectedObject.AppDomainId;
        }

        // Assert
        Verify(currentAppDomainId != newAppDomainId);
    }

    public void SetupHostShouldSetChildDomainsAppBaseToTestSourceLocation()
    {
        // Arrange
        _ = new DummyClass();

        string location = typeof(TestSourceHost).Assembly.Location;
        Mock<TestSourceHost> sourceHost = new(location, null, null) { CallBase = true };

        try
        {
            // Act
            sourceHost.Object.SetupHost();
            var expectedObject = sourceHost.Object.CreateInstanceForType(typeof(DummyClass), null) as DummyClass;

            // Assert
            Verify(Path.GetDirectoryName(typeof(DesktopTestSourceHostTests).Assembly.Location) == expectedObject.AppDomainAppBase);
        }
        finally
        {
            sourceHost.Object.Dispose();
        }
    }

    public void SetupHostShouldHaveParentDomainsAppBaseSetToTestSourceLocation()
    {
        // Arrange
        DummyClass dummyClass = new();
        string runSettingxml =
            """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>False</DisableAppDomain>
              </RunConfiguration>
            </RunSettings>
            """;

        string location = typeof(TestSourceHost).Assembly.Location;
        var mockRunSettings = new Mock<IRunSettings>();
        mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);

        TestSourceHost sourceHost = new(location, mockRunSettings.Object, null);

        try
        {
            // Act
            sourceHost.SetupHost();
            var expectedObject = sourceHost.CreateInstanceForType(typeof(DummyClass), null) as DummyClass;

            // Assert
            Verify(Path.GetDirectoryName(typeof(DesktopTestSourceHostTests).Assembly.Location) == expectedObject.AppDomainAppBase);
        }
        finally
        {
            sourceHost.Dispose();
        }
    }

    public void SetupHostShouldSetResolutionsPaths()
    {
        // Arrange
        DummyClass dummyClass = new();

        string location = typeof(TestSourceHost).Assembly.Location;
        Mock<TestSourceHost> sourceHost = new(location, null, null) { CallBase = true };

        try
        {
            // Act
            sourceHost.Object.SetupHost();

            // Assert
            sourceHost.Verify(sh => sh.GetResolutionPaths(location, It.IsAny<bool>()), Times.Once);
        }
        finally
        {
            sourceHost.Object.Dispose();
        }
    }

    public void DisposeShouldSetTestHostShutdownOnIssueWithAppDomainUnload()
    {
        // Arrange
        var frameworkHandle = new Mock<IFrameworkHandle>();
        var testableAppDomain = new Mock<IAppDomain>();

        testableAppDomain.Setup(ad => ad.CreateDomain(It.IsAny<string>(), It.IsAny<Evidence>(), It.IsAny<AppDomainSetup>())).Returns(AppDomain.CurrentDomain);
        testableAppDomain.Setup(ad => ad.Unload(It.IsAny<AppDomain>())).Throws(new CannotUnloadAppDomainException());
        var sourceHost = new TestSourceHost(typeof(DesktopTestSourceHostTests).Assembly.Location, null, frameworkHandle.Object, testableAppDomain.Object);
        sourceHost.SetupHost();

        // Act
        sourceHost.Dispose();

        // Assert
        frameworkHandle.VerifySet(fh => fh.EnableShutdownAfterTestRun = true);
    }

    public void NoAppDomainShouldGetCreatedWhenDisableAppDomainIsSetToTrue()
    {
        // Arrange
        DummyClass dummyClass = new();
        string runSettingxml =
        @"<RunSettings>
                <RunConfiguration>
                    <DisableAppDomain>True</DisableAppDomain>
                </RunConfiguration>
            </RunSettings>";

        string location = typeof(TestSourceHost).Assembly.Location;
        var mockRunSettings = new Mock<IRunSettings>();
        mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);

        Mock<TestSourceHost> testSourceHost = new(location, mockRunSettings.Object, null) { CallBase = true };

        try
        {
            // Act
            testSourceHost.Object.SetupHost();
            Verify(testSourceHost.Object.AppDomain is null);
        }
        finally
        {
            testSourceHost.Object.Dispose();
        }
    }

    public void AppDomainShouldGetCreatedWhenDisableAppDomainIsSetToFalse()
    {
        // Arrange
        DummyClass dummyClass = new();
        string runSettingxml =
            """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>False</DisableAppDomain>
              </RunConfiguration>
            </RunSettings>
            """;

        string location = typeof(TestSourceHost).Assembly.Location;
        var mockRunSettings = new Mock<IRunSettings>();
        mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);

        Mock<TestSourceHost> testSourceHost = new(location, mockRunSettings.Object, null) { CallBase = true };

        try
        {
            // Act
            testSourceHost.Object.SetupHost();
            Verify(testSourceHost.Object.AppDomain is not null);
        }
        finally
        {
            testSourceHost.Object.Dispose();
        }
    }
}

public class DummyClass : MarshalByRefObject
{
    public int AppDomainId => AppDomain.CurrentDomain.Id;

    public string AppDomainAppBase => AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
}
#endif
