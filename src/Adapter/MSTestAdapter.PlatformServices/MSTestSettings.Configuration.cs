// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using DebuggerLaunchMode = Microsoft.VisualStudio.TestTools.UnitTesting.DebuggerLaunchMode;
using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using StringEx = Microsoft.VisualStudio.TestTools.UnitTesting.StringEx;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

internal sealed partial class MSTestSettings
{
    /// <summary>
    /// Populate adapter settings from the context.
    /// </summary>
    /// <param name="context">The discovery context.</param>
    /// <param name="logger"> The logger for messages. </param>
    /// <param name="configuration">The configuration.</param>
    internal static void PopulateSettings(IDiscoveryContext? context, IMessageLogger? logger, IConfiguration? configuration)
    {
#if !WINDOWS_UWP
        if (configuration?["mstest"] is not null
            && context?.RunSettings is not null
            && RunSettingsFileHasMSTestSettings(context.RunSettings.SettingsXml))
        {
            throw new InvalidOperationException(Resource.DuplicateConfigurationError);
        }
#endif

        var settings = new MSTestSettings();
        var runConfigurationSettings = RunConfigurationSettings.GetSettings(context?.RunSettings?.SettingsXml);

#if !WINDOWS_UWP
        if (!StringEx.IsNullOrEmpty(context?.RunSettings?.SettingsXml) && configuration?["mstest"] is null)
#else
        if (!StringEx.IsNullOrEmpty(context?.RunSettings?.SettingsXml))
#endif
        {
            MSTestSettings? aliasSettings = GetSettings(context.RunSettings.SettingsXml, SettingsNameAlias, logger);
            settings = aliasSettings ?? GetSettings(context.RunSettings.SettingsXml, SettingsName, logger) ?? new MSTestSettings();
            SetGlobalSettings(context.RunSettings.SettingsXml, settings, logger);
        }
#if !WINDOWS_UWP
        else if (configuration?["mstest"] is not null)
        {
            RunConfigurationSettings.SetRunConfigurationSettingsFromConfig(configuration, runConfigurationSettings);
            SetSettingsFromConfig(configuration, logger, settings);
        }
#endif

        CurrentSettings = settings;
        RunConfigurationSettings = runConfigurationSettings;

        if (settings.RandomizeTestOrder && settings.OrderTestsByNameInClass)
        {
            logger?.SendMessage(TestMessageLevel.Warning, Resource.RandomTestOrderAndOrderTestsByNameInClassConflict);
        }

        // Track configuration source for telemetry.
#if !WINDOWS_UWP && !WIN_UI
        if (MSTestTelemetryDataCollector.Current is { } telemetry)
        {
            telemetry.ConfigurationSource = configuration?["mstest"] is not null
                ? "testconfig.json"
                : !StringEx.IsNullOrEmpty(context?.RunSettings?.SettingsXml)
                    ? "runsettings"
                    : "none";
        }
#endif
    }

    private static void SetGlobalSettings(string runsettingsXml, MSTestSettings settings, IMessageLogger? logger)
    {
        XElement? runConfigElement = XDocument.Parse(runsettingsXml).Element("RunSettings")?.Element("RunConfiguration");
        if (runConfigElement == null)
        {
            return;
        }

        string? disableParallelizationString = runConfigElement.Element("DisableParallelization")?.Value;
        if (bool.TryParse(disableParallelizationString, out bool disableParallelization))
        {
            settings.DisableParallelization = disableParallelization;
        }
        else if (disableParallelizationString is not null)
        {
            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, disableParallelizationString, "DisableParallelization"));
        }
    }

#if !WINDOWS_UWP
    private static void ParseBooleanSetting(IConfiguration configuration, string key, IMessageLogger? logger, Action<bool> setSetting)
    {
        if (configuration[$"mstest:{key}"] is not string value)
        {
            return;
        }

        if (bool.TryParse(value, out bool result))
        {
            setSetting(result);
        }
        else
        {
            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, key));
        }
    }

    private static void ParseDebuggerLaunchModeSetting(IConfiguration configuration, string key, IMessageLogger? logger, Action<DebuggerLaunchMode> setSetting)
    {
        if (configuration[$"mstest:{key}"] is not string value)
        {
            return;
        }

        if (TryParseEnum(value, out DebuggerLaunchMode mode))
        {
            setSetting(mode);
        }
        else
        {
            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, key));
        }
    }

    private static void ParseTimeoutSetting(IConfiguration configuration, string key, IMessageLogger? logger, Action<int> setSetting)
    {
        if (configuration[$"mstest:{key}"] is not string value)
        {
            return;
        }

        // This helper is only used for timeout settings, which must be strictly positive (in milliseconds).
        // A value of 0 (or less) is rejected on purpose: omitting the key already means "no timeout" (the
        // default is 0 internally), so accepting an explicit 0 here would be redundant and ambiguous. Invalid
        // values are ignored with a warning rather than throwing.
        if (int.TryParse(value, out int result) && result > 0)
        {
            setSetting(result);
        }
        else
        {
            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidTimeoutValue, value, key));
        }
    }

    private static void ParseSignedIntegerSetting(IConfiguration configuration, string key, IMessageLogger? logger, Action<int> setSetting)
    {
        if (configuration[$"mstest:{key}"] is not string value)
        {
            return;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
        {
            setSetting(result);
        }
        else
        {
            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, key));
        }
    }

    /// <summary>
    /// Convert the parameter xml to TestSettings.
    /// </summary>
    /// <param name="configuration">Configuration to load the settings from.</param>
    /// <param name="logger"> The logger for messages. </param>
    /// <param name="settings">The MSTest settings.</param>
    internal static void SetSettingsFromConfig(IConfiguration configuration, IMessageLogger? logger, MSTestSettings settings)
    {
        // 'orderTestsByNameInClass' has moved under 'execution:' for consistency with the other execution settings.
        // Prefer the new 'mstest:execution:orderTestsByNameInClass' key. Only fall back to the deprecated flat
        // 'mstest:orderTestsByNameInClass' key (emitting a deprecation warning) when the new key is absent, so users
        // can keep both keys for cross-version compatibility without being warned and without spurious parse warnings.
        if (configuration["mstest:execution:orderTestsByNameInClass"] is not null)
        {
            ParseBooleanSetting(configuration, "execution:orderTestsByNameInClass", logger, value => settings.OrderTestsByNameInClass = value);
        }
        else if (configuration["mstest:orderTestsByNameInClass"] is not null)
        {
            logger?.SendMessage(TestMessageLevel.Warning, Resource.DeprecatedFlatOrderTestsByNameInClassKey);
            ParseBooleanSetting(configuration, "orderTestsByNameInClass", logger, value => settings.OrderTestsByNameInClass = value);
        }

        ParseBooleanSetting(configuration, "execution:randomizeTestOrder", logger, value => settings.RandomizeTestOrder = value);
        ParseSignedIntegerSetting(configuration, "execution:randomTestOrderSeed", logger, value => settings.RandomTestOrderSeed = value);
        ParseBooleanSetting(configuration, "output:captureTrace", logger, value => settings.CaptureDebugTraces = value);
        ParseBooleanSetting(configuration, "parallelism:enabled", logger, value => settings.DisableParallelization = !value);
        ParseBooleanSetting(configuration, "execution:mapInconclusiveToFailed", logger, value => settings.MapInconclusiveToFailed = value);
        ParseBooleanSetting(configuration, "execution:mapNotRunnableToFailed", logger, value => settings.MapNotRunnableToFailed = value);
        ParseBooleanSetting(configuration, "execution:treatDiscoveryWarningsAsErrors", logger, value => settings.TreatDiscoveryWarningsAsErrors = value);
        ParseBooleanSetting(configuration, "execution:considerEmptyDataSourceAsInconclusive", logger, value => settings.ConsiderEmptyDataSourceAsInconclusive = value);
        ParseDebuggerLaunchModeSetting(configuration, "execution:launchDebuggerOnAssertionFailure", logger, value => settings.LaunchDebuggerOnAssertionFailure = value);
        ParseBooleanSetting(configuration, "timeout:useCooperativeCancellation", logger, value => settings.CooperativeCancellationTimeout = value);
        ParseTimeoutSetting(configuration, "timeout:test", logger, value => settings.TestTimeout = value);
        ParseTimeoutSetting(configuration, "timeout:assemblyCleanup", logger, value => settings.AssemblyCleanupTimeout = value);
        ParseTimeoutSetting(configuration, "timeout:assemblyInitialize", logger, value => settings.AssemblyInitializeTimeout = value);
        ParseTimeoutSetting(configuration, "timeout:classInitialize", logger, value => settings.ClassInitializeTimeout = value);
        ParseTimeoutSetting(configuration, "timeout:classCleanup", logger, value => settings.ClassCleanupTimeout = value);
        ParseTimeoutSetting(configuration, "timeout:testInitialize", logger, value => settings.TestInitializeTimeout = value);
        ParseTimeoutSetting(configuration, "timeout:testCleanup", logger, value => settings.TestCleanupTimeout = value);

        if (configuration["mstest:parallelism:workers"] is string workers)
        {
            if (!int.TryParse(workers, out int parallelWorkers) || parallelWorkers < 0)
            {
                throw new AdapterSettingsException(string.Format(CultureInfo.CurrentCulture, Resource.InvalidParallelWorkersValue, workers));
            }

            settings.ParallelizationWorkers = parallelWorkers == 0 ? Environment.ProcessorCount : parallelWorkers;
        }

        if (configuration["mstest:parallelism:scope"] is string value)
        {
            value = value.Equals("class", StringComparison.OrdinalIgnoreCase) ? "ClassLevel"
                : value.Equals("method", StringComparison.OrdinalIgnoreCase) ? "MethodLevel"
                : value;

            if (!TryParseEnum(value, out ExecutionScope scope))
            {
                throw new AdapterSettingsException(string.Format(CultureInfo.CurrentCulture, Resource.InvalidParallelScopeValue, value, string.Join(", ", Enum.GetNames(typeof(ExecutionScope)))));
            }

            settings.ParallelizationScope = scope;
        }

        MSTestSettingsProvider.Load(configuration);
    }
#endif
}
