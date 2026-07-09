// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;

using IEnvironment = Microsoft.Testing.Platform.Helpers.IEnvironment;
using IFileStream = Microsoft.Testing.Platform.Helpers.IFileStream;
using IFileSystem = Microsoft.Testing.Platform.Helpers.IFileSystem;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;

/// <summary>
/// MSTest-native test host environment-variable provider that applies the <c>&lt;EnvironmentVariables&gt;</c> from a
/// .runsettings file to the test host. Mirrors the VSTest bridge's <c>RunSettingsEnvironmentVariableProvider</c>
/// without depending on the bridge.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestRunSettingsEnvironmentVariableProvider : ITestHostEnvironmentVariableProvider
{
    private readonly IExtension _extension;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IFileSystem _fileSystem;
    private readonly IEnvironment _environment;
    private XDocument? _runSettings;

    public MSTestRunSettingsEnvironmentVariableProvider(IExtension extension, ICommandLineOptions commandLineOptions, IFileSystem fileSystem, IEnvironment environment)
    {
        _extension = extension;
        _commandLineOptions = commandLineOptions;
        _fileSystem = fileSystem;
        _environment = environment;
    }

    public string Uid => _extension.Uid;

    public string Version => _extension.Version;

    public string DisplayName => _extension.DisplayName;

    public string Description => _extension.Description;

    public async Task<bool> IsEnabledAsync()
    {
        string? runSettingsFilePath = null;
        string? runSettingsContent = null;

        if (_commandLineOptions.TryGetOptionArgumentList(MSTestRunSettingsCommandLineOptionsProvider.RunSettingsOptionName, out string[]? runsettings)
            && runsettings.Length > 0
            && _fileSystem.ExistFile(runsettings[0]))
        {
            runSettingsFilePath = runsettings[0];
        }

        if (runSettingsFilePath is null)
        {
            runSettingsContent = _environment.GetEnvironmentVariable("TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS");
        }

        if (runSettingsFilePath is null && string.IsNullOrEmpty(runSettingsContent))
        {
            string? envVarFilePath = _environment.GetEnvironmentVariable("TESTINGPLATFORM_VSTESTBRIDGE_RUNSETTINGS_FILE");
            if (!string.IsNullOrEmpty(envVarFilePath) && _fileSystem.ExistFile(envVarFilePath!))
            {
                runSettingsFilePath = envVarFilePath;
            }
        }

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
        else if (!string.IsNullOrEmpty(runSettingsContent))
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
#endif
