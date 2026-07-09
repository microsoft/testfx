// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;

using IEnvironment = Microsoft.Testing.Platform.Helpers.IEnvironment;
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
        _runSettings = await RunSettingsProviderHelper.TryLoadRunSettingsAsync(
            _commandLineOptions,
            _fileSystem,
            _environment,
            MSTestRunSettingsCommandLineOptionsProvider.RunSettingsOptionName).ConfigureAwait(false);

        return _runSettings is not null && RunSettingsProviderHelper.HasEnvironmentVariables(_runSettings);
    }

    public Task UpdateAsync(IEnvironmentVariables environmentVariables)
    {
        RunSettingsProviderHelper.ApplyEnvironmentVariables(_runSettings!, environmentVariables);
        return Task.CompletedTask;
    }

    public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables)
        => ValidationResult.ValidTask;
}
#endif
