// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        if (!_commandLineOptions.TryGetOptionArgumentList(RunSettingsCommandLineOptionsProvider.RunSettingsOptionName, out string[]? runsettings))
        {
            return false;
        }

        if (!_fileSystem.Exists(runsettings[0]))
        {
            return false;
        }

        using IFileStream fileStream = _fileSystem.NewFileStream(runsettings[0], FileMode.Open, FileAccess.Read);
#if NETCOREAPP
        _runSettings = await XDocument.LoadAsync(fileStream.Stream, LoadOptions.None, CancellationToken.None);
#else
        using StreamReader streamReader = new(fileStream.Stream);
        _runSettings = XDocument.Parse(await streamReader.ReadToEndAsync());
#endif
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
