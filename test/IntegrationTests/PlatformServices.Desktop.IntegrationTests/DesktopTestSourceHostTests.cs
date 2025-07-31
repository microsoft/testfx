// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

using Moq;

using TestFramework.ForTestingMSTest;

namespace PlatformServices.Desktop.ComponentTests;

public class DesktopTestSourceHostTests : TestContainer
{
    private TestSourceHost? _testSourceHost;

    public void ParentDomainShouldHonorSearchDirectoriesSpecifiedInRunsettings()
    {
        string sampleProjectDirPath = Path.GetDirectoryName(GetTestAssemblyPath("SampleProjectForAssemblyResolution"));
        string runSettingsXml =
            $"""
             <RunSettings>
                <RunConfiguration>
                    <DisableAppDomain>True</DisableAppDomain>
                </RunConfiguration>
                <MSTestV2>
                    <AssemblyResolution>
                        <Directory path = " % Temp %\directory" includeSubDirectories = "true" />
                        <Directory path = "C:\windows" includeSubDirectories = "false" />
                        <Directory path = "{sampleProjectDirPath}" />
                    </AssemblyResolution>
                </MSTestV2>
             </RunSettings>
             """;

        _testSourceHost = new TestSourceHost(
            GetTestAssemblyPath("DesktopTestProjectx86Debug"),
            GetMockedIRunSettings(runSettingsXml).Object,
            null,
            new Mock<IAdapterTraceLogger>().Object);
        _testSourceHost.SetupHost();

        // Loading SampleProjectForAssemblyResolution.dll should not throw.
        // It is present in  <Directory path = ".\ComponentTests" />  specified in runsettings
        Assembly.Load("SampleProjectForAssemblyResolution");
    }

    public void ChildDomainResolutionPathsShouldHaveSearchDirectoriesSpecifiedInRunsettings()
    {
        string sampleProjectPath = GetTestAssemblyPath("SampleProjectForAssemblyResolution");
        string sampleProjectDirPath = Path.GetDirectoryName(sampleProjectPath);
        string runSettingsXml =
            $"""
             <RunSettings>
               <RunConfiguration>
                 <DisableAppDomain>False</DisableAppDomain>
               </RunConfiguration>
               <MSTestV2>
                 <AssemblyResolution>
                   <Directory path = " % Temp %\directory" includeSubDirectories = "true" />
                   <Directory path = "C:\windows" includeSubDirectories = "false" />
                   <Directory path = "{sampleProjectDirPath}" />
                 </AssemblyResolution>
               </MSTestV2>
             </RunSettings>
             """;

        _testSourceHost = new TestSourceHost(
            GetTestAssemblyPath("DesktopTestProjectx86Debug"),
            GetMockedIRunSettings(runSettingsXml).Object,
            null,
            // Cannot use mock of IAdapterTraceLogger here because of AppDomain serialization
            new AdapterTraceLogger());
        _testSourceHost.SetupHost();

        var asm = Assembly.LoadFrom(sampleProjectPath);
        Type type = asm.GetType("SampleProjectForAssemblyResolution.SerializableTypeThatShouldBeLoaded");

        // Creating instance of SampleProjectForAssemblyResolution should not throw.
        // It is present in  <Directory path = ".\ComponentTests" />  specified in runsettings
        AppDomainUtilities.CreateInstance(_testSourceHost.AppDomain!, type, null);
    }

    public void DisposeShouldUnloadChildAppDomain()
    {
        string testSourceHandler = GetTestAssemblyPath("DesktopTestProjectx86Debug");
        // Cannot use mock of IAdapterTraceLogger here because of AppDomain serialization
        _testSourceHost = new TestSourceHost(testSourceHandler, null, null, new AdapterTraceLogger());
        _testSourceHost.SetupHost();

        // Check that child appdomain was indeed created
        _testSourceHost.AppDomain.Should().NotBeNull();
        _testSourceHost.Dispose();

        // Check that child-appdomain is now unloaded.
        _testSourceHost.AppDomain.Should().BeNull();
    }

    private static string GetArtifactsBinDir()
    {
        string artifactsBinDirPath = Path.GetFullPath(Path.Combine(
            typeof(DesktopTestSourceHostTests).Assembly.Location,
            "..",
            "..",
            "..",
            ".."));
        Directory.Exists(artifactsBinDirPath).Should().BeTrue($"artifacts bin dir '{artifactsBinDirPath}' should exist");

        return artifactsBinDirPath;
    }

    private static string GetTestAssemblyPath(string assetName)
    {
        string testAssetPath = Path.Combine(
            GetArtifactsBinDir(),
            assetName,
#if DEBUG
            "Debug",
#else
            "Release",
#endif
            "net462",
            assetName + ".dll");

        File.Exists(testAssetPath).Should().BeTrue($"Test asset '{testAssetPath}' should exist");

        return testAssetPath;
    }

    private static Mock<IRunSettings> GetMockedIRunSettings(string runSettingsXml)
    {
        var mockRunSettings = new Mock<IRunSettings>();
        mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);

        StringReader stringReader = new(runSettingsXml);
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        MSTestSettingsProvider mstestSettingsProvider = new();
        reader.ReadToFollowing("MSTestV2");
        mstestSettingsProvider.Load(reader);

        return mockRunSettings;
    }
}
