// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

using Moq;

using TestFramework.ForTestingMSTest;

using FluentAssertions;

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
        string path = @"C:\unitTesting\..\MsTest\Adapter";
        string? baseDirectory = null;
        string expectedResult = @"C:\MsTest\Adapter";

        var adapterSettings = new TestableMSTestAdapterSettings
        {
            DoesDirectoryExistSetter = _ => true,
        };

        string? result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedResult, StringComparison.OrdinalIgnoreCase);
    }

    public void ResolveEnvironmentVariableShouldResolvePathWithAnEnvironmentVariable()
    {
        string path = @"%temp%\unitTesting\MsTest\Adapter";
        string? baseDirectory = null;
        string expectedResult = @"C:\foo\unitTesting\MsTest\Adapter";

        var adapterSettings = new TestableMSTestAdapterSettings
        {
            ExpandEnvironmentVariablesSetter = str => str.Replace("%temp%", "C:\\foo"),
            DoesDirectoryExistSetter = _ => true,
        };

        string? result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedResult, StringComparison.OrdinalIgnoreCase);
    }

    public void ResolveEnvironmentVariableShouldResolvePathWhenPassedRelativePathWithoutDot()
    {
        string path = @"MsTest\Adapter";
        string baseDirectory = @"C:\unitTesting";
        string expectedResult = @"C:\unitTesting\MsTest\Adapter";

        var adapterSettings = new TestableMSTestAdapterSettings
        {
            DoesDirectoryExistSetter = _ => true,
        };

        string? result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedResult, StringComparison.OrdinalIgnoreCase);
    }

    public void ResolveEnvironmentVariableShouldResolvePathWhenPassedRelativePathWithDot()
    {
        string path = @".\MsTest\Adapter";
        string baseDirectory = @"C:\unitTesting";
        string expectedResult = @"C:\unitTesting\MsTest\Adapter";

        var adapterSettings = new TestableMSTestAdapterSettings
        {
            DoesDirectoryExistSetter = _ => true,
        };

        string? result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedResult, StringComparison.OrdinalIgnoreCase);
    }

    public void ResolveEnvironmentVariableShouldResolvePathWhenPassedRelativePath()
    {
        string path = @"\MsTest\Adapter";
        string baseDirectory = @"C:\unitTesting";

        // instead of returning "C:\unitTesting\MsTest\Adapter", it will return "(Drive from where test is running):\MsTest\Adapter",
        // because path is starting with "\"
        // this is how Path.GetFullPath works
        string currentDirectory = Directory.GetCurrentDirectory();
        string currentDrive = currentDirectory.Split('\\').First() + "\\";
        string expectedResult = Path.Combine(currentDrive, @"MsTest\Adapter");

        var adapterSettings = new TestableMSTestAdapterSettings
        {
            DoesDirectoryExistSetter = _ => true,
        };

        string? result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedResult, StringComparison.OrdinalIgnoreCase);
    }

    public void ResolveEnvironmentVariableShouldResolvePathWhenPassedNetworkPath()
    {
        string path = @"\\MsTest\Adapter";
        string baseDirectory = @"C:\unitTesting";

        string expectedResult = path;

        var adapterSettings = new TestableMSTestAdapterSettings
        {
            DoesDirectoryExistSetter = _ => true,
        };

        string? result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedResult, StringComparison.OrdinalIgnoreCase);
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
        string baseDirectory = @"C:\unitTesting";

        List<RecursiveDirectoryPath> expectedResult =
        [
            new RecursiveDirectoryPath(@"C:\MsTest\Adapter", true),
            new RecursiveDirectoryPath(@"C:\foo\unitTesting\MsTest\Adapter", false),
        ];

        var adapterSettings = new TestableMSTestAdapterSettings(expectedResult)
        {
            ExpandEnvironmentVariablesSetter = str => str.Replace("%temp%", "C:\\foo"),
            DoesDirectoryExistSetter = _ => true,
        };

        IList<RecursiveDirectoryPath> result = adapterSettings.GetDirectoryListWithRecursiveProperty(baseDirectory);
        result.Should().NotBeNull();
        result.Count.Should().Be(2);

        for (int i = 0; i < 2; i++)
        {
            string.Equals(result[i].DirectoryPath, expectedResult[i].DirectoryPath, StringComparison.OrdinalIgnoreCase));
        Verify(result[i].IncludeSubDirectories.Should().Be(expectedResult[i].IncludeSubDirectories);
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
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
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
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        reader.Read();

        void ShouldThrowException() => MSTestAdapterSettings.ToSettings(reader);

        ShouldThrowException.Should().Throw<SettingsException>();
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
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
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
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        reader.Read();
        var adapterSettings = MSTestAdapterSettings.ToSettings(reader);
        !adapterSettings.DeploymentEnabled.Should().BeTrue();
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
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
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
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        reader.Read();
        var adapterSettings = MSTestAdapterSettings.ToSettings(reader);
        !adapterSettings.DeployTestSourceDependencies.Should().BeTrue();
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
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
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
        settings.DeployTestSourceDependencies.Should().BeTrue();
        !settings.DeleteDeploymentDirectoryAfterTestRunIsComplete.Should().BeTrue();
    }

    public void IsAppDomainCreationDisabled_ShouldPreferJsonConfigurationOverSettingsXml()
    {
        // Arrange
        string settingsXml =
            """
        <RunSettings>
            <MSTest>
                <DisableAppDomain>false</DisableAppDomain>
            </MSTest>
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

public class TestableMSTestAdapterSettings : MSTestAdapterSettings
{
    public TestableMSTestAdapterSettings()
    {
    }

    public TestableMSTestAdapterSettings(List<RecursiveDirectoryPath> expectedResult) => SearchDirectories.AddRange(expectedResult);

    public Func<string, bool>? DoesDirectoryExistSetter { get; set; }

    public Func<string, string>? ExpandEnvironmentVariablesSetter { get; set; }

    protected override bool DoesDirectoryExist(string path) => DoesDirectoryExistSetter?.Invoke(path) ?? base.DoesDirectoryExist(path);

    protected override string ExpandEnvironmentVariables(string path) => ExpandEnvironmentVariablesSetter == null ? base.ExpandEnvironmentVariables(path) : ExpandEnvironmentVariablesSetter(path);
}
