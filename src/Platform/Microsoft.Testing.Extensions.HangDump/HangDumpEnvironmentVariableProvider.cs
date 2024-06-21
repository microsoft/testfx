// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed class HangDumpEnvironmentVariableProvider : ITestHostEnvironmentVariableProvider
{
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly HangDumpConfiguration _hangDumpConfiguration;

    public HangDumpEnvironmentVariableProvider(ICommandLineOptions commandLineOptions, HangDumpConfiguration hangDumpConfiguration)
    {
        _commandLineOptions = commandLineOptions;
        _hangDumpConfiguration = hangDumpConfiguration;
    }

    public string Uid => nameof(HangDumpEnvironmentVariableProvider);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.HangDumpExtensionDisplayName;

    public string Description => ExtensionResources.HangDumpExtensionDescription;

    public Task<bool> IsEnabledAsync() => Task.FromResult(_commandLineOptions.IsOptionSet(HangDumpCommandLineProvider.HangDumpOptionName) &&
        !_commandLineOptions.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey));

    public Task UpdateAsync(IEnvironmentVariables environmentVariables)
    {
        environmentVariables.SetVariable(
            new(_hangDumpConfiguration.PipeNameKey, _hangDumpConfiguration.PipeNameValue, false, true));
        environmentVariables.SetVariable(
            new(HangDumpConfiguration.MutexNameSuffix, _hangDumpConfiguration.MutexSuffix, false, true));
        return Task.CompletedTask;
    }

    public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables)
    {
        if (!environmentVariables.TryGetVariable(_hangDumpConfiguration.PipeNameKey, out OwnedEnvironmentVariable? envVar))
        {
            return Task.FromResult(
                ValidationResult.Invalid(
                    string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpEnvironmentVariableIsMissingErrorMessage, _hangDumpConfiguration.PipeNameKey)));
        }

        if (envVar.Value != _hangDumpConfiguration.PipeNameValue)
        {
            return Task.FromResult(
                ValidationResult.Invalid(
                    string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpEnvironmentVariableInvalidValueErrorMessage, _hangDumpConfiguration.PipeNameKey, envVar.Value, _hangDumpConfiguration.PipeNameKey)));
        }

        if (!environmentVariables.TryGetVariable(HangDumpConfiguration.MutexNameSuffix, out envVar))
        {
            return Task.FromResult(
                ValidationResult.Invalid(
                    string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpEnvironmentVariableIsMissingErrorMessage, HangDumpConfiguration.MutexNameSuffix)));
        }

        if (envVar.Value != _hangDumpConfiguration.MutexSuffix)
        {
            return Task.FromResult(
                ValidationResult.Invalid(
                    string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpEnvironmentVariableInvalidValueErrorMessage, HangDumpConfiguration.MutexNameSuffix, envVar.Value, _hangDumpConfiguration.MutexSuffix)));
        }

        // No problem found
        return ValidationResult.ValidTask;
    }
}
