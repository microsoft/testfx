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
    public const string PipeNameEnvironmentVariableName = "TESTINGPLATFORM_HANGDUMP_PIPENAME";

    private readonly ICommandLineOptions _commandLineOptions;
    private readonly string _pipeName;

    public HangDumpEnvironmentVariableProvider(ICommandLineOptions commandLineOptions, string pipeName)
    {
        _commandLineOptions = commandLineOptions;
        _pipeName = pipeName;
    }

    public string Uid => nameof(HangDumpEnvironmentVariableProvider);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.HangDumpExtensionDisplayName;

    public string Description => ExtensionResources.HangDumpExtensionDescription;

    public Task<bool> IsEnabledAsync() => Task.FromResult(_commandLineOptions.IsOptionSet(HangDumpCommandLineProvider.HangDumpOptionName));

    public Task UpdateAsync(IEnvironmentVariables environmentVariables)
    {
        environmentVariables.SetVariable(
            new EnvironmentVariable(PipeNameEnvironmentVariableName, _pipeName, isSecret: false, isLocked: true));
        return Task.CompletedTask;
    }

    public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables)
    {
        if (!environmentVariables.TryGetVariable(PipeNameEnvironmentVariableName, out OwnedEnvironmentVariable? envVar))
        {
            return Task.FromResult(
                ValidationResult.Invalid(
                    string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpEnvironmentVariableIsMissingErrorMessage, PipeNameEnvironmentVariableName)));
        }

        if (envVar.Value != _pipeName)
        {
            return Task.FromResult(
                ValidationResult.Invalid(
                    string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpEnvironmentVariableInvalidValueErrorMessage, PipeNameEnvironmentVariableName, envVar.Value, _pipeName)));
        }

        // No problem found
        return ValidationResult.ValidTask;
    }
}
