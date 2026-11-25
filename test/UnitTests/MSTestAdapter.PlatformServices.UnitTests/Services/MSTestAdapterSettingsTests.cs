// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests;

#pragma warning disable SA1649 // File name must match first type name
public class MSTestAdapterSettingsTests : TestContainer

#pragma warning restore SA1649 // File name must match first type name
{
    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            MSTestSettingsProvider.Reset();
        }
    }

    #region ResolveEnvironmentVariableAndReturnFullPathIfExist tests.

    public void ResolveEnvironmentVariableShouldResolvePathWhenPassedAbsolutePath()
    {
        string root = Path.GetFullPath("resolutionRoot");
        string path = Path.Combine(root, "unitTesting", "..", "MsTest", "Adapter");
        string? baseDirectory = null;
        string expectedResult = Path.Combine(root, "MsTest", "Adapter");

        var adapterSettings = new TestableMSTestAdapterSettings
        {
            DoesDirectoryExistSetter = _ => true,
        };

        string? result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedResult, options => options.IgnoringCase());
    }

    public void ResolveEnvironmentVariableShouldResolvePathWithAnEnvironmentVariable()
    {
        string path = Path.Combine("%temp%", "unitTesting", "MsTest", "Adapter");
        string? baseDirectory = null;
        string root = Path.GetFullPath("foo");
        string expectedResult = Path.Combine(root, "unitTesting", "MsTest", "Adapter");

        var adapterSettings = new TestableMSTestAdapterSettings
        {
            ExpandEnvironmentVariablesSetter = str => str.Replace("%temp%", root),
            DoesDirectoryExistSetter = _ => true,
        };

        string? result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedResult, options => options.IgnoringCase());
    }

    public void ResolveEnvironmentVariableShouldResolvePathWhenPassedRelativePathWithoutDot()
    {
        string path = Path.Combine("MsTest", "Adapter");
        string baseDirectory = Path.GetFullPath("unitTesting");
        string expectedResult = Path.Combine(baseDirectory, "MsTest", "Adapter");

        var adapterSettings = new TestableMSTestAdapterSettings
        {
            DoesDirectoryExistSetter = _ => true,
        };

        string? result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedResult, options => options.IgnoringCase());
    }

    public void ResolveEnvironmentVariableShouldResolvePathWhenPassedRelativePathWithDot()
    {
        string path = Path.Combine(".", "MsTest", "Adapter");
        string baseDirectory = Path.GetFullPath("unitTesting");
        string expectedResult = Path.Combine(baseDirectory, "MsTest", "Adapter");

        var adapterSettings = new TestableMSTestAdapterSettings
        {
            DoesDirectoryExistSetter = _ => true,
        };

        string? result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedResult, options => options.IgnoringCase());
    }

    public void ResolveEnvironmentVariableShouldResolvePathWhenPassedRelativePath()
    {
        string path = Path.DirectorySeparatorChar + Path.Combine("MsTest", "Adapter");
        string baseDirectory = Path.GetFullPath("unitTesting");
        // A leading separator resolves against the current drive or filesystem root, not the supplied base directory.
        string expectedResult = Path.Combine(Path.GetPathRoot(Directory.GetCurrentDirectory())!, "MsTest", "Adapter");

        var adapterSettings = new TestableMSTestAdapterSettings
        {
            DoesDirectoryExistSetter = _ => true,
        };

        string? result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedResult, options => options.IgnoringCase());
    }

    public void ResolveEnvironmentVariableShouldResolvePathWhenPassedNetworkPath()
    {
        string path = @"\\MsTest\Adapter";
        string baseDirectory = Path.GetFullPath("unitTesting");
        // A UNC path is rooted on Windows; on Unix the backslashes are literal filename characters.
        string expectedResult = Path.DirectorySeparatorChar == '\\' ? path : Path.Combine(baseDirectory, path);

        var adapterSettings = new TestableMSTestAdapterSettings
        {
            DoesDirectoryExistSetter = _ => true,
        };

        string? result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedResult, options => options.IgnoringCase());
    }

    public void ResolveEnvironmentVariableShouldReturnFalseForInvalidPath()
    {
        string path = @"Z:\Program Files (x86)\MsTest\Adapter";
        string baseDirectory = @"C:\unitTesting";

        string? result = new TestableMSTestAdapterSettings().ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        result.Should().BeNull();
    }

    #endregion

    #region GetDirectoryListWithRecursiveProperty tests.

    public void GetDirectoryListWithRecursivePropertyShouldReadRunSettingCorrectly()
    {
        string baseDirectory = Path.GetFullPath("unitTesting");

        List<RecursiveDirectoryPath> expectedResult =
        [
            new RecursiveDirectoryPath(Path.GetFullPath(Path.Combine("MsTest", "Adapter")), true),
            new RecursiveDirectoryPath(Path.Combine(baseDirectory, "MsTest", "Adapter"), false),
        ];

        var adapterSettings = new TestableMSTestAdapterSettings(expectedResult)
        {
            DoesDirectoryExistSetter = _ => true,
        };

        IList<RecursiveDirectoryPath> result = adapterSettings.GetDirectoryListWithRecursiveProperty(baseDirectory);
        result.Should().NotBeNull();
        result.Count.Should().Be(2);

        for (int i = 0; i < 2; i++)
        {
            result[i].DirectoryPath.Should().BeEquivalentTo(expectedResult[i].DirectoryPath, options => options.IgnoringCase());
            result[i].IncludeSubDirectories.Should().Be(expectedResult[i].IncludeSubDirectories);
        }
    }

    #endregion

    #region ToSettings tests.

    public void ToSettingsShouldNotThrowExceptionWhenRunSettingsXmlUnderTagMSTestV2IsWrong()
    {
        string runSettingsXml =
            """
            <MSTestV2>
              <IgnoreTestImpact>true</IgnoreTestImpact>
              <AssemblyResolutionBug>
                <Directory  path="C:\\MsTest\\Adapter" includeSubDirectories ="true" />
                <Directory  path="%temp%\\unitTesting\\MsTest\\Adapter" includeSubDirectories = "false" />
                <Directory path="*MsTest\Adapter" />
              </AssemblyResolutionBug>
              <InProcMode>true</InProcMode>
              <CleanUpCommunicationChannels>false</CleanUpCommunicationChannels>
            </MSTestV2>
            """;

        StringReader stringReader = new(runSettingsXml);
        var reader = XmlReader.Create(stringReader, RunSettingsUtilities.ReaderSettings);
        reader.Read();

        MSTestAdapterSettings.ToSettings(reader);
    }

    public void ToSettingsShouldThrowExceptionWhenRunSettingsXmlIsWrong()
    {
        string runSettingsXml =
            """
            <MSTestV2>
              <AssemblyResolution>
                <DirectoryBug  path="C:\\MsTest\\Adapter" includeSubDirectories ="true" />
                <Directory  path="%temp%\\unitTesting\\MsTest\\Adapter" includeSubDirectories = "false" />
                <Directory path="*MsTest\Adapter" />
              </AssemblyResolution>
            </MSTestV2>
            """;

        StringReader stringReader = new(runSettingsXml);
        var reader = XmlReader.Create(stringReader, RunSettingsUtilities.ReaderSettings);
        reader.Read();

        void ShouldThrowException() => MSTestAdapterSettings.ToSettings(reader);

        new Action(ShouldThrowException).Should().Throw<AdapterSettingsException>();
    }

    #endregion

    #region DeploymentEnabled tests.

    public void DeploymentEnabledIsByDefaultTrueWhenNotSpecified()
    {
        string runSettingsXml =
            """
            <MSTestV2>
            </MSTestV2>
            """;
        StringReader stringReader = new(runSettingsXml);
        var reader = XmlReader.Create(stringReader, RunSettingsUtilities.ReaderSettings);
        reader.Read();
        var adapterSettings = MSTestAdapterSettings.ToSettings(reader);
        adapterSettings.DeploymentEnabled.Should().BeTrue();
    }

    public void DeploymentEnabledShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingsXml =
            """
            <MSTestV2>
              <DeploymentEnabled>False</DeploymentEnabled>
            </MSTestV2>
            """;
        StringReader stringReader = new(runSettingsXml);
        var reader = XmlReader.Create(stringReader, RunSettingsUtilities.ReaderSettings);
        reader.Read();
        var adapterSettings = MSTestAdapterSettings.ToSettings(reader);
        adapterSettings.DeploymentEnabled.Should().BeFalse();
    }

    public void CreateDeploymentDirectoriesWithoutDeploymentItemsIsEnabledByDefault()
    {
        string runSettingsXml =
            """
            <MSTestV2>
            </MSTestV2>
            """;
        StringReader stringReader = new(runSettingsXml);
        var reader = XmlReader.Create(stringReader, RunSettingsUtilities.ReaderSettings);
        reader.Read();
        var adapterSettings = MSTestAdapterSettings.ToSettings(reader);
        adapterSettings.CreateDeploymentDirectoriesWithoutDeploymentItems.Should().BeTrue();
    }

    public void CreateDeploymentDirectoriesWithoutDeploymentItemsShouldBeConsumedFromRunSettings()
    {
        string runSettingsXml =
            """
            <MSTestV2>
              <CreateDeploymentDirectoriesWithoutDeploymentItems>False</CreateDeploymentDirectoriesWithoutDeploymentItems>
            </MSTestV2>
            """;
        StringReader stringReader = new(runSettingsXml);
        var reader = XmlReader.Create(stringReader, RunSettingsUtilities.ReaderSettings);
        reader.Read();
        var adapterSettings = MSTestAdapterSettings.ToSettings(reader);
        adapterSettings.CreateDeploymentDirectoriesWithoutDeploymentItems.Should().BeFalse();
    }

    #endregion

    #region DeployTestSourceDependencies tests

    public void DeployTestSourceDependenciesIsEnabledByDefault()
    {
        string runSettingsXml =
            """
            <MSTestV2>
            </MSTestV2>
            """;
        StringReader stringReader = new(runSettingsXml);
        var reader = XmlReader.Create(stringReader, RunSettingsUtilities.ReaderSettings);
        reader.Read();
        var adapterSettings = MSTestAdapterSettings.ToSettings(reader);
        adapterSettings.DeployTestSourceDependencies.Should().BeTrue();
    }

    public void DeployTestSourceDependenciesWhenFalse()
    {
        string runSettingsXml =
            """
            <MSTestV2>
              <DeployTestSourceDependencies>False</DeployTestSourceDependencies>
            </MSTestV2>
            """;
        StringReader stringReader = new(runSettingsXml);
        var reader = XmlReader.Create(stringReader, RunSettingsUtilities.ReaderSettings);
        reader.Read();
        var adapterSettings = MSTestAdapterSettings.ToSettings(reader);
        adapterSettings.DeployTestSourceDependencies.Should().BeFalse();
    }

    public void DeployTestSourceDependenciesWhenTrue()
    {
        string runSettingsXml =
            """
            <MSTestV2>
              <DeployTestSourceDependencies>True</DeployTestSourceDependencies>
            </MSTestV2>
            """;
        StringReader stringReader = new(runSettingsXml);
        var reader = XmlReader.Create(stringReader, RunSettingsUtilities.ReaderSettings);
        reader.Read();
        var adapterSettings = MSTestAdapterSettings.ToSettings(reader);
        adapterSettings.DeployTestSourceDependencies.Should().BeTrue();
    }

    #endregion

    #region ConfigJson
    public void ToSettings_ShouldInitializeDeploymentAndAssemblyResolutionSettingsCorrectly()
    {
        // Arrange
        var configDictionary = new Dictionary<string, string>
        {
            { "mstest:deployment:enabled", "true" },
            { "mstest:deployment:createDirectoriesWithoutDeploymentItems", "false" },
            { "mstest:deployment:deployTestSourceDependencies", "true" },
            { "mstest:deployment:deleteDeploymentDirectoryAfterTestRunIsComplete", "false" },
            { "mstest:assemblyResolution:0:path", "C:\\project\\dependencies" },
            { "mstest:assemblyResolution:0:includeSubDirectories", "true" },
            { "mstest:assemblyResolution:1:path", "C:\\project\\libs" },
            { "mstest:assemblyResolution:1:includeSubDirectories", "false" },
            { "mstest:assemblyResolution:2:path", "C:\\project\\plugins" },
        };

        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(config => config[It.IsAny<string>()])
                  .Returns((string key) => configDictionary.TryGetValue(key, out string? value) ? value : null);

        // Act
        var settings = MSTestAdapterSettings.ToSettings(mockConfig.Object);

        // Assert
        settings.DeploymentEnabled.Should().BeTrue();
        settings.CreateDeploymentDirectoriesWithoutDeploymentItems.Should().BeFalse();
        settings.DeployTestSourceDependencies.Should().BeTrue();
        settings.DeleteDeploymentDirectoryAfterTestRunIsComplete.Should().BeFalse();
    }

    public void IsAppDomainCreationDisabled_ShouldPreferJsonConfigurationOverSettingsXml()
    {
        // Arrange
        string settingsXml =
            """
        <RunSettings>
            <RunConfiguration>
                <DisableAppDomain>false</DisableAppDomain>
            </RunConfiguration>
        </RunSettings>
        """;

        var configDictionary = new Dictionary<string, string>
        {
            { "mstest:execution:disableAppDomain", "true" },
        };
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(config => config[It.IsAny<string>()])
                  .Returns((string key) => configDictionary.TryGetValue(key, out string? value) ? value : null);

        // Act
        MSTestAdapterSettings.Configuration = mockConfig.Object;
        bool result = MSTestAdapterSettings.IsAppDomainCreationDisabled(settingsXml);

        // Assert
        result.Should().BeTrue();
    }

    #endregion
}

internal class TestableMSTestAdapterSettings : MSTestAdapterSettings
{
    public TestableMSTestAdapterSettings()
    {
    }

    public TestableMSTestAdapterSettings(List<RecursiveDirectoryPath> expectedResult) => SearchDirectories.AddRange(expectedResult);

    public Func<string, bool>? DoesDirectoryExistSetter { get; set; }

    public Func<string, string>? ExpandEnvironmentVariablesSetter { get; set; }

    protected override bool DoesDirectoryExist(string path) => DoesDirectoryExistSetter?.Invoke(path) ?? base.DoesDirectoryExist(path);

    protected override string ExpandEnvironmentVariables(string path) => ExpandEnvironmentVariablesSetter is null ? base.ExpandEnvironmentVariables(path) : ExpandEnvironmentVariablesSetter(path);
}
