// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public sealed class MSTestAdapterSettingsContinuationTests : TestContainer
{
    private readonly IPlatformServiceProvider _previousPlatform = PlatformServiceProvider.Instance;
    private readonly IConfiguration? _previousConfiguration = MSTestAdapterSettings.Configuration;
    private readonly TestablePlatformServiceProvider _platform = new();

    public MSTestAdapterSettingsContinuationTests()
    {
        MSTestSettingsProvider.Reset();
        PlatformServiceProvider.Instance = _platform;
    }

    public void NullReadersAreRejectedWithoutMutatingLoadedSettings()
    {
        MSTestAdapterSettings original = Read("<MSTest><DeploymentEnabled>false</DeploymentEnabled></MSTest>");
        Action direct = () => MSTestAdapterSettings.ToSettings((XmlReader)null!);
        Action provider = () => new MSTestSettingsProvider().Load(null!);

        direct.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("reader");
        provider.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("reader");
        MSTestSettingsProvider.Settings.Should().BeSameAs(original);
        original.DeploymentEnabled.Should().BeFalse();
    }

    public void XmlThenJsonLoadsRetainSingletonIdentityAndAccumulateResolutionDirectories()
    {
        using SettingsDirectory directory = new();
        string xmlDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "xml")).FullName;
        string jsonDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "json")).FullName;
        string defaultDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "default")).FullName;
        string invalidBooleanDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "invalid-bool")).FullName;
        string xml = $"""
            <MSTest>
              <DeploymentEnabled>false</DeploymentEnabled>
              <DeployTestSourceDependencies>false</DeployTestSourceDependencies>
              <DeleteDeploymentDirectoryAfterTestRunIsComplete>false</DeleteDeploymentDirectoryAfterTestRunIsComplete>
              <AssemblyResolution><Directory path="{xmlDirectory}" includeSubDirectories="true" /></AssemblyResolution>
            </MSTest>
            """;
        MSTestAdapterSettings original = MSTestSettingsProvider.Settings;
        using (var text = new StringReader(xml))
        using (var reader = XmlReader.Create(text, RunSettingsUtilities.ReaderSettings))
        {
            reader.Read();
            new MSTestSettingsProvider().Load(reader);
        }

        IConfiguration configuration = Configuration(new()
        {
            ["mstest:deployment:enabled"] = "true",
            ["mstest:deployment:deployTestSourceDependencies"] = "invalid",
            ["mstest:assemblyResolution:0:path"] = jsonDirectory,
            ["mstest:assemblyResolution:0:includeSubDirectories"] = "TRUE",
            ["mstest:assemblyResolution:1:path"] = defaultDirectory,
            ["mstest:assemblyResolution:2:path"] = invalidBooleanDirectory,
            ["mstest:assemblyResolution:2:includeSubDirectories"] = "not-a-boolean",
        });
        MSTestSettingsProvider.Load(configuration);

        MSTestSettingsProvider.Settings.Should().BeSameAs(original);
        MSTestAdapterSettings.Configuration.Should().BeSameAs(configuration);
        original.DeploymentEnabled.Should().BeTrue();
        original.DeployTestSourceDependencies.Should().BeFalse();
        original.DeleteDeploymentDirectoryAfterTestRunIsComplete.Should().BeFalse();
        List<RecursiveDirectoryPath> paths = original.GetDirectoryListWithRecursiveProperty(directory.Path);
        paths.Select(value => value.DirectoryPath).Should().Equal(xmlDirectory, jsonDirectory, defaultDirectory, invalidBooleanDirectory);
        paths.Select(value => value.IncludeSubDirectories).Should().Equal(true, true, false, false);
    }

    public void InvalidDeploymentValuesPreserveEarlierXmlValues()
    {
        MSTestAdapterSettings defaults = Read("""
            <MSTest>
              <DeploymentEnabled>invalid</DeploymentEnabled>
              <DeployTestSourceDependencies>1</DeployTestSourceDependencies>
              <DeleteDeploymentDirectoryAfterTestRunIsComplete />
            </MSTest>
            """);
        defaults.DeploymentEnabled.Should().BeTrue();
        defaults.DeployTestSourceDependencies.Should().BeTrue();
        defaults.DeleteDeploymentDirectoryAfterTestRunIsComplete.Should().BeTrue();
        MSTestAdapterSettings.ToSettings(Configuration(new()
        {
            ["mstest:deployment:enabled"] = "invalid",
            ["mstest:deployment:deployTestSourceDependencies"] = "1",
            ["mstest:deployment:deleteDeploymentDirectoryAfterTestRunIsComplete"] = "invalid",
        })).Should().BeSameAs(defaults);
        defaults.DeploymentEnabled.Should().BeTrue();
        defaults.DeployTestSourceDependencies.Should().BeTrue();
        defaults.DeleteDeploymentDirectoryAfterTestRunIsComplete.Should().BeTrue();

        MSTestAdapterSettings settings = Read("""
            <MSTest>
              <DeploymentEnabled>false</DeploymentEnabled>
              <DeployTestSourceDependencies>false</DeployTestSourceDependencies>
              <DeleteDeploymentDirectoryAfterTestRunIsComplete>false</DeleteDeploymentDirectoryAfterTestRunIsComplete>
            </MSTest>
            """);

        Read("""
            <MSTest>
              <DeploymentEnabled>invalid</DeploymentEnabled>
              <DeployTestSourceDependencies>1</DeployTestSourceDependencies>
              <DeleteDeploymentDirectoryAfterTestRunIsComplete />
              <Ignored><DeploymentEnabled>true</DeploymentEnabled></Ignored>
            </MSTest>
            """).Should().BeSameAs(settings);
        MSTestAdapterSettings.ToSettings(Configuration(new()
        {
            ["mstest:deployment:enabled"] = "TRUE",
            ["mstest:deployment:deployTestSourceDependencies"] = "1",
            ["mstest:deployment:deleteDeploymentDirectoryAfterTestRunIsComplete"] = "invalid",
        })).Should().BeSameAs(settings);

        settings.DeploymentEnabled.Should().BeTrue();
        settings.DeployTestSourceDependencies.Should().BeFalse();
        settings.DeleteDeploymentDirectoryAfterTestRunIsComplete.Should().BeFalse();
    }

    public void XmlResolutionRejectsUnknownElementsAfterPreservingEarlierSettings()
    {
        Action load = () => Read("""
            <MSTest>
              <DeploymentEnabled>false</DeploymentEnabled>
              <AssemblyResolution><NotDirectory path="ignored" /></AssemblyResolution>
              <DeployTestSourceDependencies>false</DeployTestSourceDependencies>
            </MSTest>
            """);

        load.Should().ThrowExactly<AdapterSettingsException>().WithMessage(
            string.Format(CultureInfo.CurrentCulture, Resource.InvalidSettingsXmlElement, "NotDirectory", "AssemblyResolution"));
        MSTestSettingsProvider.Settings.DeploymentEnabled.Should().BeFalse();
        MSTestSettingsProvider.Settings.DeployTestSourceDependencies.Should().BeTrue();
    }

    public void AppDomainConfigurationHonorsJsonPrecedenceAndInvalidValueFallback()
    {
        _ = MSTestSettingsProvider.Settings;
        MSTestAdapterSettings.IsAppDomainCreationDisabled(null).Should().BeFalse();
        MSTestAdapterSettings.IsAppDomainCreationDisabled(string.Empty).Should().BeFalse();
        const string XmlFalse = "<RunSettings><RunConfiguration><DisableAppDomain>false</DisableAppDomain></RunConfiguration></RunSettings>";
        const string XmlTrue = "<RunSettings><RunConfiguration><DisableAppDomain>true</DisableAppDomain></RunConfiguration></RunSettings>";

        MSTestAdapterSettings.Configuration = Configuration(new() { ["mstest:execution:disableAppDomain"] = "true" });
        MSTestAdapterSettings.IsAppDomainCreationDisabled(XmlFalse).Should().BeTrue();
        MSTestAdapterSettings.Configuration = Configuration(new() { ["mstest:execution:disableAppDomain"] = "false" });
        MSTestAdapterSettings.IsAppDomainCreationDisabled(XmlTrue).Should().BeFalse();
        foreach (string? value in new[] { null, string.Empty, "not-a-boolean" })
        {
            MSTestAdapterSettings.Configuration = Configuration(new() { ["mstest:execution:disableAppDomain"] = value });
            MSTestAdapterSettings.IsAppDomainCreationDisabled(XmlFalse).Should().BeFalse();
            MSTestAdapterSettings.IsAppDomainCreationDisabled(XmlTrue).Should().BeTrue();
        }
    }

    public void ResolutionTrimsExpandsAndCanonicalizesBeforeCheckingExistence()
    {
        string root = Path.Combine(Path.GetTempPath(), "resolution-root");
        string unexpanded = Path.Combine("%ROOT%", "unused", "..", "Adapter");
        var settings = new ResolutionSettings(
            value => value.Replace("%ROOT%", root),
            _ => true);

        string? resolved = settings.ResolveEnvironmentVariableAndReturnFullPathIfExist(" \t" + unexpanded + "\t ", null);

        resolved.Should().Be(Path.Combine(root, "Adapter"));
        settings.Expanded.Should().Equal(unexpanded);
        settings.Checked.Should().Equal(Path.Combine(root, "Adapter"));
    }

    public void EmptyAndRelativePathsAreRejectedBeforeExistenceChecks()
    {
        var warnings = new List<string>();
        _platform.MockTraceLogger.SetupGet(logger => logger.IsWarningEnabled).Returns(true);
        _platform.MockTraceLogger.Setup(logger => logger.Warning(It.IsAny<string>())).Callback<string>(warnings.Add);
        var settings = new ResolutionSettings(value => value, _ => true);
        foreach (string? path in new[] { null, string.Empty, " \t " })
        {
            settings.ResolveEnvironmentVariableAndReturnFullPathIfExist(path, null).Should().BeNull();
        }

        settings.Expanded.Should().BeEmpty();
        settings.ResolveEnvironmentVariableAndReturnFullPathIfExist("relative", null).Should().BeNull();
        warnings.Should().Equal("The Directory: relative, has following problem: This is not an absolute path. A base directory should be provided for this to be used as a relative path.");
        settings.Checked.Should().BeEmpty();

        _platform.MockTraceLogger.SetupGet(logger => logger.IsWarningEnabled).Returns(false);
        settings.ResolveEnvironmentVariableAndReturnFullPathIfExist("relative", string.Empty).Should().BeNull();
        warnings.Should().HaveCount(1);
        settings.Expanded.Should().Equal("relative", "relative");
        settings.Checked.Should().BeEmpty();
    }

    public void MalformedPathsReportTheOriginalPathAndBclFailure()
    {
#if NETFRAMEWORK
        string invalid = Path.GetTempPath() + "bad:part";
        Action canonicalize = () => Path.GetFullPath(invalid);
        string bclFailure = canonicalize.Should().ThrowExactly<NotSupportedException>().Subject.Single().Message;
#else
        string invalid = Path.GetTempPath() + "bad\0path";
        Action canonicalize = () => Path.GetFullPath(invalid);
        string bclFailure = canonicalize.Should().Throw<ArgumentException>().Subject.Single().Message;
#endif
        var warnings = new List<string>();
        _platform.MockTraceLogger.SetupGet(logger => logger.IsWarningEnabled).Returns(true);
        _platform.MockTraceLogger.Setup(logger => logger.Warning(It.IsAny<string>())).Callback<string>(warnings.Add);
        var settings = new ResolutionSettings(value => value, _ => true);

        settings.ResolveEnvironmentVariableAndReturnFullPathIfExist(invalid, null).Should().BeNull();

        warnings.Should().Equal($"The Directory: {invalid}, has following problem: {bclFailure}");
        settings.Checked.Should().BeEmpty();
        _platform.MockTraceLogger.SetupGet(logger => logger.IsWarningEnabled).Returns(false);
        settings.ResolveEnvironmentVariableAndReturnFullPathIfExist(invalid, null).Should().BeNull();
        warnings.Should().HaveCount(1);
        settings.Checked.Should().BeEmpty();
    }

    public void MissingDirectoriesWarnOnlyWhenWarningTracingIsEnabled()
    {
        string missing = Path.Combine(Path.GetTempPath(), "missing-resolution-root");
        var warnings = new List<string>();
        _platform.MockTraceLogger.SetupGet(logger => logger.IsWarningEnabled).Returns(true);
        _platform.MockTraceLogger.Setup(logger => logger.Warning(It.IsAny<string>())).Callback<string>(warnings.Add);
        var settings = new ResolutionSettings(value => value, _ => false);

        settings.ResolveEnvironmentVariableAndReturnFullPathIfExist("missing-resolution-root", Path.GetTempPath()).Should().BeNull();
        warnings.Should().Equal($"The Directory: {missing}, does not exist.");
        _platform.MockTraceLogger.SetupGet(logger => logger.IsWarningEnabled).Returns(false);
        settings.ResolveEnvironmentVariableAndReturnFullPathIfExist(missing, null).Should().BeNull();

        warnings.Should().HaveCount(1);
        settings.Checked.Should().Equal(missing, missing);
    }

    public void RealEnvironmentExpansionFeedsExistingDirectoriesIntoResolution()
    {
        const string Variable = "MSTEST_CONTEXT_CONTINUATION_RESOLUTION_ROOT";
        string? previous = Environment.GetEnvironmentVariable(Variable);
        using SettingsDirectory directory = new();
        string child = Directory.CreateDirectory(Path.Combine(directory.Path, "real")).FullName;
        try
        {
            Environment.SetEnvironmentVariable(Variable, directory.Path);
            MSTestAdapterSettings settings = Read($"""
                <MSTest><AssemblyResolution>
                  <Directory path="%{Variable}%{Path.DirectorySeparatorChar}real" includeSubDirectories="TrUe" />
                  <Directory path="{directory.Path}{Path.DirectorySeparatorChar}missing" />
                  <Directory />
                </AssemblyResolution></MSTest>
                """);

            List<RecursiveDirectoryPath> paths = settings.GetDirectoryListWithRecursiveProperty(directory.Path);

            paths.Select(value => value.DirectoryPath).Should().Equal(child);
            paths.Select(value => value.IncludeSubDirectories).Should().Equal(true);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Variable, previous);
        }
    }

    public void ProviderResetStartsFreshWithoutChangingPreviouslyLoadedSettings()
    {
        MSTestAdapterSettings previous = Read("<MSTest><DeploymentEnabled>false</DeploymentEnabled><DeployTestSourceDependencies>false</DeployTestSourceDependencies></MSTest>");

        MSTestSettingsProvider.Reset();
        MSTestAdapterSettings fresh = MSTestSettingsProvider.Settings;

        fresh.Should().NotBeSameAs(previous);
        fresh.DeploymentEnabled.Should().BeTrue();
        fresh.DeployTestSourceDependencies.Should().BeTrue();
        fresh.DeleteDeploymentDirectoryAfterTestRunIsComplete.Should().BeTrue();
        previous.DeploymentEnabled.Should().BeFalse();
        previous.DeployTestSourceDependencies.Should().BeFalse();
    }

#if NETFRAMEWORK
    public void InvalidCharactersRejectedDuringRootClassificationDoNotCheckExistence()
    {
        string invalid = Path.GetTempPath() + "bad\0path";
        var settings = new ResolutionSettings(value => value, _ => true);
        Action resolve = () => settings.ResolveEnvironmentVariableAndReturnFullPathIfExist(invalid, null);

        resolve.Should().ThrowExactly<ArgumentException>().WithMessage("Illegal characters in path.");
        settings.Expanded.Should().Equal(invalid);
        settings.Checked.Should().BeEmpty();
        _platform.MockTraceLogger.Verify(logger => logger.Warning(It.IsAny<string>()), Times.Never);
    }
#endif

    protected override void Dispose(bool disposing)
    {
        try
        {
            MSTestSettingsProvider.Reset();
            MSTestAdapterSettings.Configuration = _previousConfiguration;
            PlatformServiceProvider.Instance = _previousPlatform;
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

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
        configuration.Setup(value => value[It.IsAny<string>()])
            .Returns((string key) => values.TryGetValue(key, out string? value) ? value : null);
        return configuration.Object;
    }

    private sealed class ResolutionSettings(Func<string, string> expand, Func<string, bool> exists) : MSTestAdapterSettings
    {
        public List<string> Expanded { get; } = [];

        public List<string> Checked { get; } = [];

        protected override string ExpandEnvironmentVariables(string path)
        {
            Expanded.Add(path);
            return expand(path);
        }

        protected override bool DoesDirectoryExist(string path)
        {
            Checked.Add(path);
            return exists(path);
        }
    }

    private sealed class SettingsDirectory : IDisposable
    {
        public SettingsDirectory() => Path = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "s" + Guid.NewGuid().ToString("N"))).FullName;

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
