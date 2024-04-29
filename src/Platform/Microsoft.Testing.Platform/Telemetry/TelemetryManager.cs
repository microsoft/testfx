// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        ArgumentGuard.IsNotNull(telemetryFactory);
        _telemetryFactory = telemetryFactory;
    }

    public async Task<ITelemetryCollector> BuildAsync(ServiceProvider serviceProvider, ILoggerFactory loggerFactory, TestApplicationOptions testApplicationOptions)
    {
        bool isTelemetryOptedOut = !testApplicationOptions.EnableTelemetry;

        ILogger<TelemetryManager> logger = loggerFactory.CreateLogger<TelemetryManager>();
        await logger.LogInformationAsync($"TestApplicationOptions.EnableTelemetry: {testApplicationOptions.EnableTelemetry}");

        // If the environment variable is not set or is set to 0, telemetry is opted in.
        IEnvironment environment = serviceProvider.GetEnvironment();
        string? telemetryOptOut = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT);
        await logger.LogInformationAsync($"{EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT} environment variable: '{telemetryOptOut}'");
        isTelemetryOptedOut = (telemetryOptOut is not null and ("1" or "true")) || isTelemetryOptedOut;

        string? cli_telemetryOptOut = environment.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_CLI_TELEMETRY_OPTOUT);
        await logger.LogInformationAsync($"{EnvironmentVariableConstants.DOTNET_CLI_TELEMETRY_OPTOUT} environment variable: '{cli_telemetryOptOut}'");
        isTelemetryOptedOut = (cli_telemetryOptOut is not null and ("1" or "true")) || isTelemetryOptedOut;

        // NO_LOGO

        // If the environment variable is not set or is set to 0, telemetry is opted in.
        ICommandLineOptions commandLineOptions = serviceProvider.GetCommandLineOptions();
        bool doNotShowLogo = commandLineOptions.IsOptionSet(PlatformCommandLineProvider.NoBannerOptionKey);

        string? noBannerEnvVar = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_NOBANNER);
        await logger.LogInformationAsync($"{EnvironmentVariableConstants.TESTINGPLATFORM_NOBANNER} environment variable: '{noBannerEnvVar}'");
        doNotShowLogo = (noBannerEnvVar is not null and ("1" or "true")) || doNotShowLogo;

        string? dotnetNoLogoEnvVar = environment.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_NOLOGO);
        await logger.LogInformationAsync($"{EnvironmentVariableConstants.DOTNET_NOLOGO} environment variable: '{dotnetNoLogoEnvVar}'");
        doNotShowLogo = (dotnetNoLogoEnvVar is not null and ("1" or "true")) || doNotShowLogo;

        await logger.LogInformationAsync($"Telemetry is '{(!isTelemetryOptedOut ? "ENABLED" : "DISABLED")}'");

        if (!isTelemetryOptedOut && !doNotShowLogo)
        {
            ITestApplicationModuleInfo testApplicationModuleInfo = serviceProvider.GetTestApplicationModuleInfo();
#pragma warning disable RS0030 // Do not use banned APIs - There is no easy way to disable it for all members
            string? directory = environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);
#pragma warning restore RS0030 // Do not use banned APIs
            if (directory is not null)
            {
                directory = Path.Combine(directory, "Microsoft", "TestingPlatform");
            }

            IFileSystem fileSystem = serviceProvider.GetFileSystem();
            string fileName = Path.ChangeExtension(Path.GetFileName(testApplicationModuleInfo.GetCurrentTestApplicationFullPath()), "testingPlatformFirstTimeUseSentinel");
            bool sentinelIsNotPresent =
                RoslynString.IsNullOrWhiteSpace(directory)
                || !fileSystem.Exists(Path.Combine(directory, fileName));

            if (!doNotShowLogo && sentinelIsNotPresent)
            {
                IOutputDevice outputDevice = serviceProvider.GetOutputDevice();
                await outputDevice.DisplayAsync(this, new TextOutputDeviceData(PlatformResources.TelemetryNotice));

                string? path = null;
                try
                {
                    // See if we should write the file, and write it.
                    if (!RoslynString.IsNullOrWhiteSpace(directory))
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
                    await logger.LogErrorAsync($"Could not write sentinel file for telemetry to path,'{path ?? "<unknown>"}'.", exception);
                }
            }
        }

        serviceProvider.TryAddService(new TelemetryInformation(!isTelemetryOptedOut, TelemetryProperties.VersionValue));

        ITelemetryCollector telemetryCollector = _telemetryFactory is null || isTelemetryOptedOut
            ? new NopTelemetryService(!isTelemetryOptedOut)
            : _telemetryFactory(serviceProvider);

        if (!isTelemetryOptedOut)
        {
            await logger.LogInformationAsync($"Telemetry collector provider: '{telemetryCollector.GetType()}'");
        }

        return telemetryCollector;
    }

    public Task<bool> IsEnabledAsync() => throw new NotImplementedException();
}
