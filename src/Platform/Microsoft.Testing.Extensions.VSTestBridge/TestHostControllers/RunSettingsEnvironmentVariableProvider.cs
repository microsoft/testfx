// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

using Microsoft.Testing.Extensions.VSTestBridge.CommandLine;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.VSTestBridge.TestHostControllers;

internal sealed class RunSettingsEnvironmentVariableProvider : ITestHostEnvironmentVariableProvider
{
    private readonly IExtension _extension;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IFileSystem _fileSystem;
    private XDocument? _runSettings;

    public RunSettingsEnvironmentVariableProvider(IExtension extension, ICommandLineOptions commandLineOptions, IFileSystem fileSystem)
    {
        _extension = extension;
        _commandLineOptions = commandLineOptions;
        _fileSystem = fileSystem;
    }

    public string Uid => _extension.Uid;

    public string Version => _extension.Version;

    public string DisplayName => _extension.DisplayName;

    public string Description => _extension.Description;

    public async Task<bool> IsEnabledAsync()
    {
        string? runSettingsFilePath = null;
        string? runSettingsContent = null;

        // Try to get runsettings from command line
        if (_commandLineOptions.TryGetOptionArgumentList(RunSettingsCommandLineOptionsProvider.RunSettingsOptionName, out string[]? runsettings)
            && runsettings.Length > 0)
        {
            if (_fileSystem.ExistFile(runsettings[0]))
            {
                runSettingsFilePath = runsettings[0];
            }
        }

        // If not from command line, try environment variable with content
        if (runSettingsFilePath is null)
        {
            runSettingsContent = Environment.GetEnvironmentVariable("TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS");
        }

        // If not from content env var, try environment variable with file path
        if (runSettingsFilePath is null && RoslynString.IsNullOrEmpty(runSettingsContent))
        {
            string? envVarFilePath = Environment.GetEnvironmentVariable("TESTINGPLATFORM_VSTESTBRIDGE_RUNSETTINGS_FILE");
            if (!RoslynString.IsNullOrEmpty(envVarFilePath) && _fileSystem.ExistFile(envVarFilePath))
            {
                runSettingsFilePath = envVarFilePath;
            }
        }

        // If we have a file path, read from file
        if (runSettingsFilePath is not null)
        {
            using IFileStream fileStream = _fileSystem.NewFileStream(runSettingsFilePath, FileMode.Open, FileAccess.Read);
#if NETCOREAPP
            _runSettings = await XDocument.LoadAsync(fileStream.Stream, LoadOptions.None, CancellationToken.None).ConfigureAwait(false);
#else
            using StreamReader streamReader = new(fileStream.Stream);
            _runSettings = XDocument.Parse(await streamReader.ReadToEndAsync().ConfigureAwait(false));
#endif
        }
        // If we have content, parse it directly
        else if (!RoslynString.IsNullOrEmpty(runSettingsContent))
        {
            _runSettings = XDocument.Parse(runSettingsContent);
        }
        else
        {
            return false;
        }

        return _runSettings.Element("RunSettings")?.Element("RunConfiguration")?.Element("EnvironmentVariables") is not null;
    }

    public Task UpdateAsync(IEnvironmentVariables environmentVariables)
    {
        foreach (XElement element in _runSettings!.Element("RunSettings")!.Element("RunConfiguration")!.Element("EnvironmentVariables")!.Elements())
        {
            environmentVariables.SetVariable(new(element.Name.ToString(), element.Value, true, true));
        }

        return Task.CompletedTask;
    }

    public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables)
        => ValidationResult.ValidTask;
}
