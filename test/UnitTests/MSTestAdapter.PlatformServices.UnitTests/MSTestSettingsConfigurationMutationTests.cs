// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class MSTestSettingsConfigurationMutationTests : TestContainer
{
#if !WINDOWS_UWP && !WIN_UI
    private readonly MSTestTelemetryDataCollector? _previousCollector = MSTestTelemetryDataCollector.Current;
#endif

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            MSTestSettings.Reset();
            MSTestSettingsProvider.Reset();
            MSTestAdapterSettings.Configuration = null;
#if !WINDOWS_UWP && !WIN_UI
            MSTestTelemetryDataCollector.Current = _previousCollector;
#endif
        }
    }

    public void PopulateSettingsReadsTheExecutionApartmentStateFromTheConfiguration()
    {
        foreach ((string text, ApartmentState expected) in new[]
        {
            ("STA", ApartmentState.STA),
            ("MTA", ApartmentState.MTA),
        })
        {
            MSTestSettings.Reset();

            MSTestSettings.PopulateSettings(
                settingsXml: null,
                logger: null,
                configuration: Configuration(new()
                {
                    ["mstest"] = "present",
                    ["mstest:execution:executionApartmentState"] = text,
                }));

            MSTestSettings.RunConfigurationSettings.ExecutionApartmentState.Should().Be(expected, because: text);
        }
    }

#if !WINDOWS_UWP && !WIN_UI
    public void PopulateSettingsTracksTheConfigurationSourceForTelemetry()
    {
        ConfigurationSourceOf(settingsXml: null, Configuration(new() { ["mstest"] = "present" })).Should().Be("testconfig.json");
        ConfigurationSourceOf("<RunSettings><MSTest /></RunSettings>", configuration: null).Should().Be("runsettings");
        ConfigurationSourceOf(settingsXml: null, configuration: null).Should().Be("none");
    }
#endif

    public void LaunchDebuggerOnAssertionFailureIsReadFromTheConfiguration()
    {
        foreach ((string text, DebuggerLaunchMode expected) in new[]
        {
            ("Enabled", DebuggerLaunchMode.Enabled),
            ("EnabledExcludingCI", DebuggerLaunchMode.EnabledExcludingCI),
        })
        {
            var logger = new Mock<IAdapterMessageLogger>(MockBehavior.Strict);
            var settings = new MSTestSettings();

            MSTestSettings.SetSettingsFromConfig(
                Configuration(new() { ["mstest:execution:launchDebuggerOnAssertionFailure"] = text }),
                logger.Object,
                settings);

            settings.LaunchDebuggerOnAssertionFailure.Should().Be(expected, because: text);
            logger.VerifyNoOtherCalls();
        }
    }

    public void InvalidLaunchDebuggerOnAssertionFailureWarnsAndKeepsTheDefault()
    {
        string warning = string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, "bogus", "execution:launchDebuggerOnAssertionFailure");
        var logger = new Mock<IAdapterMessageLogger>(MockBehavior.Strict);
        logger.Setup(value => value.SendMessage(MessageLevel.Warning, warning));
        var settings = new MSTestSettings();

        MSTestSettings.SetSettingsFromConfig(
            Configuration(new() { ["mstest:execution:launchDebuggerOnAssertionFailure"] = "bogus" }),
            logger.Object,
            settings);

        settings.LaunchDebuggerOnAssertionFailure.Should().Be(DebuggerLaunchMode.Disabled);
        logger.Verify(value => value.SendMessage(MessageLevel.Warning, warning), Times.Once);
        logger.VerifyNoOtherCalls();
    }

    public void InvalidRandomTestOrderSeedWarnsAndKeepsTheDefault()
    {
        string warning = string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, "abc", "execution:randomTestOrderSeed");
        var logger = new Mock<IAdapterMessageLogger>(MockBehavior.Strict);
        logger.Setup(value => value.SendMessage(MessageLevel.Warning, warning));
        var settings = new MSTestSettings();

        MSTestSettings.SetSettingsFromConfig(
            Configuration(new() { ["mstest:execution:randomTestOrderSeed"] = "abc" }),
            logger.Object,
            settings);

        settings.RandomTestOrderSeed.Should().BeNull();
        logger.Verify(value => value.SendMessage(MessageLevel.Warning, warning), Times.Once);
        logger.VerifyNoOtherCalls();
    }

    public void ParallelismWorkersMustBeANonNegativeInteger()
    {
        foreach (string workers in new[] { "abc", "-1" })
        {
            var settings = new MSTestSettings();
            Action act = () => MSTestSettings.SetSettingsFromConfig(
                Configuration(new() { ["mstest:parallelism:workers"] = workers }),
                logger: null,
                settings);

            AdapterSettingsException exception = act.Should().ThrowExactly<AdapterSettingsException>(because: workers).Which;

            exception.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Resource.InvalidParallelWorkersValue, workers));
        }
    }

    public void ParallelismWorkersOfZeroMeansOneWorkerPerProcessor()
    {
        foreach ((string text, int expected) in new[]
        {
            ("0", Environment.ProcessorCount),
            ("3", 3),
        })
        {
            var settings = new MSTestSettings();

            MSTestSettings.SetSettingsFromConfig(
                Configuration(new() { ["mstest:parallelism:workers"] = text }),
                logger: null,
                settings);

            settings.ParallelizationWorkers.Should().Be(expected, because: text);
        }
    }

    public void ParallelismScopeAcceptsAliasesAndScopeNamesCaseInsensitively()
    {
        foreach ((string text, ExecutionScope expected) in new[]
        {
            ("class", ExecutionScope.ClassLevel),
            ("CLASS", ExecutionScope.ClassLevel),
            ("ClassLevel", ExecutionScope.ClassLevel),
            ("method", ExecutionScope.MethodLevel),
            ("Method", ExecutionScope.MethodLevel),
            ("MethodLevel", ExecutionScope.MethodLevel),
        })
        {
            var settings = new MSTestSettings();

            MSTestSettings.SetSettingsFromConfig(
                Configuration(new() { ["mstest:parallelism:scope"] = text }),
                logger: null,
                settings);

            settings.ParallelizationScope.Should().Be(expected, because: text);
        }
    }

    public void InvalidParallelismScopeThrowsAndListsTheSupportedScopes()
    {
        var settings = new MSTestSettings();
        Action act = () => MSTestSettings.SetSettingsFromConfig(
            Configuration(new() { ["mstest:parallelism:scope"] = "abc" }),
            logger: null,
            settings);

        AdapterSettingsException exception = act.Should().ThrowExactly<AdapterSettingsException>().Which;

        exception.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Resource.InvalidParallelScopeValue, "abc", "ClassLevel, MethodLevel"));
    }

    public void SetSettingsFromConfigLoadsTheDeploymentSettings()
    {
        MSTestSettingsProvider.Reset();
        MSTestAdapterSettings.Configuration = null;
        IConfiguration configuration = Configuration(new()
        {
            ["mstest:deployment:enabled"] = "false",
            ["mstest:deployment:deployTestSourceDependencies"] = "false",
            ["mstest:deployment:deleteDeploymentDirectoryAfterTestRunIsComplete"] = "false",
        });

        MSTestSettings.SetSettingsFromConfig(configuration, logger: null, new MSTestSettings());

        MSTestAdapterSettings deploymentSettings = MSTestSettingsProvider.Settings;
        deploymentSettings.DeploymentEnabled.Should().BeFalse();
        deploymentSettings.DeployTestSourceDependencies.Should().BeFalse();
        deploymentSettings.DeleteDeploymentDirectoryAfterTestRunIsComplete.Should().BeFalse();
        MSTestAdapterSettings.Configuration.Should().BeSameAs(configuration);
    }

#if !WINDOWS_UWP && !WIN_UI
    private static string? ConfigurationSourceOf(string? settingsXml, IConfiguration? configuration)
    {
        var collector = new MSTestTelemetryDataCollector();
        MSTestTelemetryDataCollector.Current = collector;

        MSTestSettings.PopulateSettings(settingsXml, logger: null, configuration);

        return collector.ConfigurationSource;
    }
#endif

    private static IConfiguration Configuration(Dictionary<string, string> values)
    {
        var configuration = new Mock<IConfiguration>();
        configuration.Setup(value => value[It.IsAny<string>()])
            .Returns((string key) => values.TryGetValue(key, out string? value) ? value : null);
        return configuration.Object;
    }
}
