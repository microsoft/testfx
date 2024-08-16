// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Microsoft.Testing.Extensions.VSTestBridge.CommandLine;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

/// <summary>
/// A Microsoft Testing Platform oriented implementation of the VSTest <see cref="IRunSettings"/>.
/// </summary>
internal sealed class RunSettingsAdapter : IRunSettings
{
    public RunSettingsAdapter(
        ICommandLineOptions commandLineOptions,
        IFileSystem fileSystem,
        IConfiguration configuration,
        IClientInfo client,
        ILoggerFactory loggerFactory)
    {
        string? runSettingsXml =
            commandLineOptions.TryGetOptionArgumentList(RunSettingsCommandLineOptionsProvider.RunSettingsOptionName, out string[]? fileNames)
            && fileNames is not null
            && fileNames.Length == 1
            && fileSystem.Exists(fileNames[0])
                ? fileSystem.ReadAllText(fileNames[0])
                : Environment.GetEnvironmentVariable("TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS");

        runSettingsXml = RunSettingsPatcher.Patch(runSettingsXml, configuration, client, commandLineOptions).ToString();

        loggerFactory.CreateLogger<RunSettingsAdapter>().LogDebug($"Execution will use the following runsettings:{Environment.NewLine}{runSettingsXml}");

        SettingsXml = runSettingsXml;
    }

    /// <inheritdoc />
    public string? SettingsXml { get; }

    /// <inheritdoc />
    // TODO: Needs to be implemented if used by adapters. It is not used by MSTest.
    public ISettingsProvider? GetSettings(string? settingsName) => throw new NotImplementedException();
}
