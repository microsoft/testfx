// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Telemetry;

internal sealed class TelemetryManager : ITelemetryManager, IOutputDeviceDataProducer
{
    private Func<IServiceProvider, ITelemetryCollector>? _telemetryFactory;

    public string Uid => nameof(TelemetryManager);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public void AddTelemetryCollectorProvider(Func<IServiceProvider, ITelemetryCollector> telemetryFactory)
    {
        if (_telemetryFactory is not null)
        {
            throw new InvalidOperationException("Telemetry provider already set");
        }

        _telemetryFactory = telemetryFactory;
    }

    public async Task<ITelemetryCollector> BuildAsync(ServiceProvider serviceProvider, ILoggerFactory loggerFactory, TestApplicationOptions testApplicationOptions)
    {
        bool isTelemetryOptedOut = !testApplicationOptions.EnableTelemetry;

        ILogger<TelemetryManager> logger = loggerFactory.CreateLogger<TelemetryManager>();
        await logger.LogInformationAsync(string.Format(CultureInfo.InvariantCulture, PlatformResources.TelemetryManagerTestApplicationOptionsTelemetryEnabled, testApplicationOptions.EnableTelemetry));

        // If the environment variable is not set or is set to 0, telemetry is opted in.
        IEnvironment environment = serviceProvider.GetEnvironment();
        string? telemetryOptOut = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT);
        await logger.LogInformationAsync(string.Format(CultureInfo.InvariantCulture, PlatformResources.TelemetryManagerEnvironmentVariableValue, EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, telemetryOptOut));
        isTelemetryOptedOut = (telemetryOptOut is not null and ("1" or "true")) || isTelemetryOptedOut;

        string? cli_telemetryOptOut = environment.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_CLI_TELEMETRY_OPTOUT);
        await logger.LogInformationAsync(string.Format(CultureInfo.InvariantCulture, PlatformResources.TelemetryManagerEnvironmentVariableValue, EnvironmentVariableConstants.DOTNET_CLI_TELEMETRY_OPTOUT, cli_telemetryOptOut));
        isTelemetryOptedOut = (cli_telemetryOptOut is not null and ("1" or "true")) || isTelemetryOptedOut;

        // NO_LOGO

        // If the environment variable is not set or is set to 0, telemetry is opted in.
        ICommandLineOptions commandLineOptions = serviceProvider.GetCommandLineOptions();
        bool dontShowLogo = commandLineOptions.IsOptionSet(PlatformCommandLineProvider.NoBannerOptionKey);

        string? noBanner = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_NOBANNER);
        await logger.LogInformationAsync(string.Format(CultureInfo.InvariantCulture, PlatformResources.TelemetryManagerEnvironmentVariableValue, EnvironmentVariableConstants.TESTINGPLATFORM_NOBANNER, noBanner));
        dontShowLogo = (noBanner is not null and ("1" or "true")) || dontShowLogo;

        string? dotnet_noLogo = environment.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_NOLOGO);
        await logger.LogInformationAsync(string.Format(CultureInfo.InvariantCulture, PlatformResources.TelemetryManagerEnvironmentVariableValue, EnvironmentVariableConstants.DOTNET_NOLOGO, dotnet_noLogo));
        dontShowLogo = (dotnet_noLogo is not null and ("1" or "true")) || dontShowLogo;

        await logger.LogInformationAsync(string.Format(
            CultureInfo.InvariantCulture,
            PlatformResources.TelemetryManagerTelemetryStatus,
            !isTelemetryOptedOut ? PlatformResources.TelemetryManagerTelemetryStatusEnabled : PlatformResources.TelemetryManagerTelemetryStatusDisabled));

        if (!isTelemetryOptedOut && !dontShowLogo)
        {
            IRuntime runtime = serviceProvider.GetRuntime();
            IFileSystem fileSystem = serviceProvider.GetFileSystem();
            IOutputDevice outputDevice = serviceProvider.GetOutputDevice();

            string fileName = Path.ChangeExtension(Path.GetFileName(runtime.GetCurrentModuleInfo().GetCurrentTestApplicationFullPath()), "testingPlatformFirstTimeUseSentinel");
            string? directory = environment.GetEnvironmentVariable("LOCALAPPDATA") ?? environment.GetEnvironmentVariable("HOME");
            if (directory is not null)
            {
                directory = Path.Combine(directory, "Microsoft", "TestingPlatform");
            }

            bool sentinelIsNotPresent =
                TAString.IsNullOrWhiteSpace(directory)
                || !fileSystem.Exists(Path.Combine(directory, fileName));

            if (!dontShowLogo && sentinelIsNotPresent)
            {
                await outputDevice.DisplayAsync(this, new TextOutputDeviceData(PlatformResources.TelemetryManagerTelemetryCollectionNotice));

                string? path = null;
                try
                {
                    // See if we should write the file, and write it.
                    if (!TAString.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);

                        // Write empty file.
                        path = Path.Combine(directory, fileName);
                        using (fileSystem.NewFileStream(path, FileMode.Create, FileAccess.Write))
                        {
                        }
                    }
                }
                catch (Exception exception) when (exception is IOException or SystemException)
                {
                    await logger.LogErrorAsync(string.Format(CultureInfo.InvariantCulture, PlatformResources.TelemetryManagerFailedToWriteSentinelFile, path ?? PlatformResources.TelemetryManagerSentinelFileUnknownPath), exception);
                }
            }
        }

        if (!isTelemetryOptedOut)
        {
            serviceProvider.TryAddService(new TelemetryInformation(true, TelemetryProperties.VersionValue));
        }
        else
        {
            serviceProvider.TryAddService(new TelemetryInformation(false, TelemetryProperties.VersionValue));
        }

        ITelemetryCollector telemetryCollector = _telemetryFactory is null
            ? new NopTelemetryService(!isTelemetryOptedOut)
            : !isTelemetryOptedOut ? _telemetryFactory(serviceProvider) : new NopTelemetryService(!isTelemetryOptedOut);

        if (!isTelemetryOptedOut)
        {
            await logger.LogInformationAsync(string.Format(CultureInfo.InvariantCulture, PlatformResources.TelemetryManagerTelemetryCollectorProvider, telemetryCollector.GetType()));
        }

        return telemetryCollector;
    }

    public Task<bool> IsEnabledAsync() => throw new NotImplementedException();
}
