// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public sealed class MSTestAdapterSettingsMutationTests : TestContainer
{
    private readonly IConfiguration? _previousConfiguration = MSTestAdapterSettings.Configuration;

    public MSTestAdapterSettingsMutationTests()
    {
        MSTestSettingsProvider.Reset();
        MSTestAdapterSettings.Configuration = null;
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            MSTestSettingsProvider.Reset();
            MSTestAdapterSettings.Configuration = _previousConfiguration;
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    public void ToSettingsFromConfigurationShouldReadOnlyTheDeployTestSourceDependenciesSetting()
    {
        var settings = MSTestAdapterSettings.ToSettings(Configuration(new() { ["mstest:deployment:deployTestSourceDependencies"] = "false" }));

        settings.DeployTestSourceDependencies.Should().BeFalse();
        settings.DeploymentEnabled.Should().BeTrue();
        settings.DeleteDeploymentDirectoryAfterTestRunIsComplete.Should().BeTrue();
    }

    public void ToSettingsFromXmlShouldKeepReadingSettingsThatFollowTheAssemblyResolutionSection()
    {
        MSTestAdapterSettings settings = Read("""
            <MSTest>
              <AssemblyResolution>
                <Directory path="dependencies" />
              </AssemblyResolution>
              <DeploymentEnabled>False</DeploymentEnabled>
            </MSTest>
            """);

        settings.DeploymentEnabled.Should().BeFalse();
    }

    public void ReadAssemblyResolutionPathShouldRejectANullXmlReader()
    {
        MethodInfo? method = typeof(MSTestAdapterSettings).GetMethod(
            "ReadAssemblyResolutionPath",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(XmlReader)],
            modifiers: null);
        method.Should().NotBeNull();

        Action action = () => method!.Invoke(new MSTestAdapterSettings(), [null]);

        action.Should().ThrowExactly<TargetInvocationException>()
            .WithInnerException<ArgumentNullException>()
            .Which.ParamName.Should().Be("reader");
    }

    public void IsAppDomainCreationDisabledShouldHonorRunSettingsWhenNoConfigurationIsAvailable()
    {
        MSTestAdapterSettings.Configuration = null;

        MSTestAdapterSettings.IsAppDomainCreationDisabled(
            "<RunSettings><RunConfiguration><DisableAppDomain>true</DisableAppDomain></RunConfiguration></RunSettings>").Should().BeTrue();
    }

    public void IsAppDomainCreationDisabledShouldHonorConfigurationWhenRunSettingsAreMissing()
    {
        MSTestAdapterSettings.Configuration = Configuration(new() { ["mstest:execution:disableAppDomain"] = "true" });

        MSTestAdapterSettings.IsAppDomainCreationDisabled(null).Should().BeTrue();
        MSTestAdapterSettings.IsAppDomainCreationDisabled(string.Empty).Should().BeTrue();
    }

#if !NETFRAMEWORK
    public void IsAppDomainCreationDisabledShouldDefaultToTrueWhenRunSettingsDoNotSpecifyIt()
    {
        MSTestAdapterSettings.Configuration = null;

        MSTestAdapterSettings.IsAppDomainCreationDisabled("<RunSettings><RunConfiguration></RunConfiguration></RunSettings>").Should().BeTrue();
    }
#endif

    private static MSTestAdapterSettings Read(string xml)
    {
        using var text = new StringReader(xml);
        using var reader = XmlReader.Create(text, RunSettingsUtilities.ReaderSettings);
        reader.Read();
        return MSTestAdapterSettings.ToSettings(reader);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values)
    {
        var configuration = new Mock<IConfiguration>();
        configuration.Setup(setup => setup[It.IsAny<string>()]).Returns((string key) => values.TryGetValue(key, out string? value) ? value : null);
        return configuration.Object;
    }
}
