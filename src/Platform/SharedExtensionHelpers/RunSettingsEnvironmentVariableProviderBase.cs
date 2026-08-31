// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Resource-independent base for the test host environment-variable provider that applies the
/// <c>&lt;EnvironmentVariables&gt;</c> from a .runsettings file to the test host. All behavior is shared; concrete
/// providers exist only to keep a distinct type per package. Shared by the VSTest bridge and the MSTest adapter's
/// native Microsoft.Testing.Platform integration.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "The shared helper is linked into projects that are allowed to use MTP APIs")]
internal abstract class RunSettingsEnvironmentVariableProviderBase : ITestHostEnvironmentVariableProvider
{
    private readonly IExtension _extension;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IFileSystem _fileSystem;
    private readonly IEnvironment _environment;
    private XDocument? _runSettings;

    protected RunSettingsEnvironmentVariableProviderBase(IExtension extension, ICommandLineOptions commandLineOptions, IFileSystem fileSystem, IEnvironment environment)
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
            RunSettingsCommandLineOptionsProviderBase.RunSettingsOptionName).ConfigureAwait(false);

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
