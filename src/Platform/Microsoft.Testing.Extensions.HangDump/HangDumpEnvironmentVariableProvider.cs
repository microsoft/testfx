// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

    public Task<bool> IsEnabledAsync() => Task.FromResult(_commandLineOptions.IsOptionSet(HangDumpCommandLineProvider.HangDumpOptionName));

    public Task UpdateAsync(IEnvironmentVariables environmentVariables)
    {
        environmentVariables.SetVariable(
            new(_hangDumpConfiguration.PipeNameKey, _hangDumpConfiguration.PipeNameValue, false, true));
        environmentVariables.SetVariable(
            new(HangDumpConfiguration.NamedPipeNameSuffixEnvironmentVariable, _hangDumpConfiguration.NamedPipeSuffix, false, true));
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

        if (!environmentVariables.TryGetVariable(HangDumpConfiguration.NamedPipeNameSuffixEnvironmentVariable, out envVar))
        {
            return Task.FromResult(
                ValidationResult.Invalid(
                    string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpEnvironmentVariableIsMissingErrorMessage, HangDumpConfiguration.NamedPipeNameSuffixEnvironmentVariable)));
        }

        if (envVar.Value != _hangDumpConfiguration.NamedPipeSuffix)
        {
            return Task.FromResult(
                ValidationResult.Invalid(
                    string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpEnvironmentVariableInvalidValueErrorMessage, HangDumpConfiguration.NamedPipeNameSuffixEnvironmentVariable, envVar.Value, _hangDumpConfiguration.NamedPipeSuffix)));
        }

        // No problem found
        return ValidationResult.ValidTask;
    }
}
