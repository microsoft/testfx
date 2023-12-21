// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

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
        string path = @"C:\unitTesting\..\MsTest\Adapter";
        string baseDirectory = null;
        string expectedResult = @"C:\MsTest\Adapter";

        var adapterSettings = new TestableMSTestAdapterSettings
        {
            DoesDirectoryExistSetter = (str) => true,
        };

        string result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        Verify(result is not null);
        Verify(string.Equals(result, expectedResult, StringComparison.OrdinalIgnoreCase));
    }

    public void ResolveEnvironmentVariableShouldResolvePathWithAnEnvironmentVariable()
    {
        string path = @"%temp%\unitTesting\MsTest\Adapter";
        string baseDirectory = null;
        string expectedResult = @"C:\foo\unitTesting\MsTest\Adapter";

        var adapterSettings = new TestableMSTestAdapterSettings
        {
            ExpandEnvironmentVariablesSetter = (str) => str.Replace("%temp%", "C:\\foo"),
            DoesDirectoryExistSetter = (str) => true,
        };

        string result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        Verify(result is not null);
        Verify(string.Equals(result, expectedResult, StringComparison.OrdinalIgnoreCase));
    }

    public void ResolveEnvironmentVariableShouldResolvePathWhenPassedRelativePathWithoutDot()
    {
        string path = @"MsTest\Adapter";
        string baseDirectory = @"C:\unitTesting";
        string expectedResult = @"C:\unitTesting\MsTest\Adapter";

        var adapterSettings = new TestableMSTestAdapterSettings
        {
            DoesDirectoryExistSetter = (str) => true,
        };

        string result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        Verify(result is not null);
        Verify(string.Equals(result, expectedResult, StringComparison.OrdinalIgnoreCase));
    }

    public void ResolveEnvironmentVariableShouldResolvePathWhenPassedRelativePathWithDot()
    {
        string path = @".\MsTest\Adapter";
        string baseDirectory = @"C:\unitTesting";
        string expectedResult = @"C:\unitTesting\MsTest\Adapter";

        var adapterSettings = new TestableMSTestAdapterSettings
        {
            DoesDirectoryExistSetter = (str) => true,
        };

        string result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        Verify(result is not null);
        Verify(string.Equals(result, expectedResult, StringComparison.OrdinalIgnoreCase));
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
            DoesDirectoryExistSetter = (str) => true,
        };

        string result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        Verify(result is not null);
        Verify(string.Equals(result, expectedResult, StringComparison.OrdinalIgnoreCase));
    }

    public void ResolveEnvironmentVariableShouldResolvePathWhenPassedNetworkPath()
    {
        string path = @"\\MsTest\Adapter";
        string baseDirectory = @"C:\unitTesting";

        string expectedResult = path;

        var adapterSettings = new TestableMSTestAdapterSettings
        {
            DoesDirectoryExistSetter = (str) => true,
        };

        string result = adapterSettings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        Verify(result is not null);
        Verify(string.Equals(result, expectedResult, StringComparison.OrdinalIgnoreCase));
    }

    public void ResolveEnvironmentVariableShouldReturnFalseForInvalidPath()
    {
        string path = @"Z:\Program Files (x86)\MsTest\Adapter";
        string baseDirectory = @"C:\unitTesting";

        string result = new TestableMSTestAdapterSettings().ResolveEnvironmentVariableAndReturnFullPathIfExist(path, baseDirectory);

        Verify(result is null);
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
            ExpandEnvironmentVariablesSetter = (str) => str.Replace("%temp%", "C:\\foo"),
            DoesDirectoryExistSetter = (str) => true,
        };

        IList<RecursiveDirectoryPath> result = adapterSettings.GetDirectoryListWithRecursiveProperty(baseDirectory);
        Verify(result is not null);
        Verify(result.Count == 2);

        for (int i = 0; i < 2; i++)
        {
            Verify(string.Equals(result[i].DirectoryPath, expectedResult[i].DirectoryPath, StringComparison.OrdinalIgnoreCase));
            Verify(result[i].IncludeSubDirectories == expectedResult[i].IncludeSubDirectories);
        }
    }

    #endregion

    #region ToSettings tests.

    public void ToSettingsShouldNotThrowExceptionWhenRunSettingsXmlUnderTagMSTestv2IsWrong()
    {
        string runSettingxml =
              @"<MSTestV2>
                    <IgnoreTestImpact>true</IgnoreTestImpact>
                    <AssemblyResolutionBug>
                        <Directory  path=""C:\\MsTest\\Adapter"" includeSubDirectories =""true"" />
                        <Directory  path=""%temp%\\unitTesting\\MsTest\\Adapter"" includeSubDirectories = ""false"" />
                        <Directory path=""*MsTest\Adapter"" />
                    </AssemblyResolutionBug>
                    <InProcMode>true</InProcMode>
                    <CleanUpCommunicationChannels>false</CleanUpCommunicationChannels>
                  </MSTestV2>";

        StringReader stringReader = new(runSettingxml);
        XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        reader.Read();

        MSTestAdapterSettings.ToSettings(reader);
    }

    public void ToSettingsShouldThrowExceptionWhenRunSettingsXmlIsWrong()
    {
        string runSettingxml =
              @"<MSTestV2>
                    <AssemblyResolution>
                        <DirectoryBug  path=""C:\\MsTest\\Adapter"" includeSubDirectories =""true"" />
                        <Directory  path=""%temp%\\unitTesting\\MsTest\\Adapter"" includeSubDirectories = ""false"" />
                        <Directory path=""*MsTest\Adapter"" />
                    </AssemblyResolution>
                  </MSTestV2>";

        StringReader stringReader = new(runSettingxml);
        XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        reader.Read();

        void ShouldThrowException() => MSTestAdapterSettings.ToSettings(reader);

        var ex = VerifyThrows(ShouldThrowException);
        Verify(ex is SettingsException);
    }

    #endregion

    #region DeploymentEnabled tests.

    public void DeploymentEnabledIsByDefaultTrueWhenNotSpecified()
    {
        string runSettingxml =
            @"<MSTestV2>
                  </MSTestV2>";
        StringReader stringReader = new(runSettingxml);
        XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        reader.Read();
        MSTestAdapterSettings adapterSettings = MSTestAdapterSettings.ToSettings(reader);
        Verify(adapterSettings.DeploymentEnabled);
    }

    public void DeploymentEnabledShouldBeConsumedFromRunSettingsWhenSpecified()
    {
        string runSettingxml =
            @"<MSTestV2>
                        <DeploymentEnabled>False</DeploymentEnabled>
                  </MSTestV2>";
        StringReader stringReader = new(runSettingxml);
        XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        reader.Read();
        MSTestAdapterSettings adapterSettings = MSTestAdapterSettings.ToSettings(reader);
        Verify(!adapterSettings.DeploymentEnabled);
    }

    #endregion

    #region DeployTestSourceDependencies tests

    public void DeployTestSourceDependenciesIsEnabledByDefault()
    {
        string runSettingxml =
            @"<MSTestV2>
                  </MSTestV2>";
        StringReader stringReader = new(runSettingxml);
        XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        reader.Read();
        MSTestAdapterSettings adapterSettings = MSTestAdapterSettings.ToSettings(reader);
        Verify(adapterSettings.DeployTestSourceDependencies);
    }

    public void DeployTestSourceDependenciesWhenFalse()
    {
        string runSettingxml =
            @"<MSTestV2>
                     <DeployTestSourceDependencies>False</DeployTestSourceDependencies>
                  </MSTestV2>";
        StringReader stringReader = new(runSettingxml);
        XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        reader.Read();
        MSTestAdapterSettings adapterSettings = MSTestAdapterSettings.ToSettings(reader);
        Verify(!adapterSettings.DeployTestSourceDependencies);
    }

    public void DeployTestSourceDependenciesWhenTrue()
    {
        string runSettingxml =
            @"<MSTestV2>
                     <DeployTestSourceDependencies>True</DeployTestSourceDependencies>
                  </MSTestV2>";
        StringReader stringReader = new(runSettingxml);
        XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        reader.Read();
        MSTestAdapterSettings adapterSettings = MSTestAdapterSettings.ToSettings(reader);
        Verify(adapterSettings.DeployTestSourceDependencies);
    }

    #endregion
}

public class TestableMSTestAdapterSettings : MSTestAdapterSettings
{
    public TestableMSTestAdapterSettings()
    {
    }

    public TestableMSTestAdapterSettings(List<RecursiveDirectoryPath> expectedResult)
    {
        SearchDirectories.AddRange(expectedResult);
    }

    public Func<string, bool> DoesDirectoryExistSetter { get; set; }

    public Func<string, string> ExpandEnvironmentVariablesSetter { get; set; }

    protected override bool DoesDirectoryExist(string path)
    {
        return DoesDirectoryExistSetter == null ? base.DoesDirectoryExist(path) : DoesDirectoryExistSetter(path);
    }

    protected override string ExpandEnvironmentVariables(string path)
    {
        return ExpandEnvironmentVariablesSetter == null ? base.ExpandEnvironmentVariables(path) : ExpandEnvironmentVariablesSetter(path);
    }
}
