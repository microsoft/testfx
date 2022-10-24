// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Reflection;
using System.Xml;

using FluentAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

using Moq;

using TestFramework.ForTestingMSTest;

namespace PlatformServices.Desktop.ComponentTests;
public class DesktopTestSourceHostTests : TestContainer
{
    private TestSourceHost _testSourceHost;

    public void ParentDomainShouldHonorSearchDirectoriesSpecifiedInRunsettings()
    {
        string runSettingXml =
        @"<RunSettings>
                <RunConfiguration>
                    <DisableAppDomain>True</DisableAppDomain>
                </RunConfiguration>
                <MSTestV2>
                    <AssemblyResolution>
                        <Directory path = "" % Temp %\directory"" includeSubDirectories = ""true"" />
                        <Directory path = ""C:\windows"" includeSubDirectories = ""false"" />
                        <Directory path = "".\ComponentTests"" />
                    </AssemblyResolution>
                </MSTestV2>
             </RunSettings>";

        var testSource = GetTestAssemblyPath("DesktopTestProjectx86Debug.dll");
        _testSourceHost = new TestSourceHost(testSource, GetMockedIRunSettings(runSettingXml).Object, null);
        _testSourceHost.SetupHost();

        // Loading SampleProjectForAssemblyResolution.dll should not throw.
        // It is present in  <Directory path = ".\ComponentTests" />  specified in runsettings
        Assembly.Load("SampleProjectForAssemblyResolution");
    }

    public void ChildDomainResolutionPathsShouldHaveSearchDirectoriesSpecifiedInRunsettings()
    {
        string runSettingXml =
        @"<RunSettings>
                <RunConfiguration>
                    <DisableAppDomain>False</DisableAppDomain>
                </RunConfiguration>
                <MSTestV2>
                    <AssemblyResolution>
                        <Directory path = "" % Temp %\directory"" includeSubDirectories = ""true"" />
                        <Directory path = ""C:\windows"" includeSubDirectories = ""false"" />
                        <Directory path = "".\ComponentTests"" />
                    </AssemblyResolution>
                </MSTestV2>
             </RunSettings>";

        var testSource = GetTestAssemblyPath("DesktopTestProjectx86Debug.dll");
        _testSourceHost = new TestSourceHost(testSource, GetMockedIRunSettings(runSettingXml).Object, null);
        _testSourceHost.SetupHost();

        var assemblyResolution = "ComponentTests\\SampleProjectForAssemblyResolution.dll";
        var asm = Assembly.LoadFrom(assemblyResolution);
        var type = asm.GetType("SampleProjectForAssemblyResolution.SerializableTypeThatShouldBeLoaded");

        // Creating instance of SampleProjectForAssemblyResolution should not throw.
        // It is present in  <Directory path = ".\ComponentTests" />  specified in runsettings
        AppDomainUtilities.CreateInstance(_testSourceHost.AppDomain, type, null);
    }

    public void DisposeShouldUnloadChildAppDomain()
    {
        var testSource = GetTestAssemblyPath("DesktopTestProjectx86Debug.dll");
        _testSourceHost = new TestSourceHost(testSource, null, null);
        _testSourceHost.SetupHost();

        // Check that child appdomain was indeed created
        _testSourceHost.AppDomain.Should().NotBeNull();
        _testSourceHost.Dispose();

        // Check that child-appdomain is now unloaded.
        _testSourceHost.AppDomain.Should().BeNull();
    }

    private static string GetTestAssemblyPath(string assemblyName)
    {
        var currentAssemblyDirectory = new FileInfo(typeof(DesktopTestSourceHostTests).Assembly.Location).Directory;
        var testAssetPath = Path.Combine(currentAssemblyDirectory.Parent.Parent.Parent.FullName, "TestAssets");

        return Path.Combine(testAssetPath, assemblyName);
    }

    private static Mock<IRunSettings> GetMockedIRunSettings(string runSettingXml)
    {
        var mockRunSettings = new Mock<IRunSettings>();
        mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingXml);

        StringReader stringReader = new(runSettingXml);
        XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        MSTestSettingsProvider mstestSettingsProvider = new();
        reader.ReadToFollowing("MSTestV2");
        mstestSettingsProvider.Load(reader);

        return mockRunSettings;
    }
}
