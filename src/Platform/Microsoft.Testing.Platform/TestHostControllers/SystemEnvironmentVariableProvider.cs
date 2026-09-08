// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.TestHostControllers;

internal sealed class SystemEnvironmentVariableProvider(IEnvironment environment) : ITestHostEnvironmentVariableProvider
{
    private static readonly string[] ReservedDeadlineVariables =
    [
        EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE,
        EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE_STOP_MARGIN,
        EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE_DUMP_MARGIN,
    ];

    private readonly SystemExtension _systemExtension = new();
    private readonly IEnvironment _environment = environment;

    public string Uid => _systemExtension.Uid;

    public string Version => _systemExtension.Version;

    public string DisplayName => _systemExtension.DisplayName;

    public string Description => _systemExtension.Description;

    public async Task<bool> IsEnabledAsync() => await _systemExtension.IsEnabledAsync().ConfigureAwait(false);

    public Task UpdateAsync(IEnvironmentVariables environmentVariables)
    {
        StringComparer environmentVariableComparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var reservedDeadlineVariables = new HashSet<string>(ReservedDeadlineVariables, environmentVariableComparer);
        foreach (DictionaryEntry entry in _environment.GetEnvironmentVariables())
        {
            string variable = entry.Key.ToString()!;
            bool isReservedDeadlineVariable = reservedDeadlineVariables.Remove(variable);
            environmentVariables.SetVariable(new(variable, entry.Value!.ToString(), false, isReservedDeadlineVariable));
        }

        // A child-only provider must not activate deadline handling when the controller did not.
        // Reserve absent values as empty and locked so every child observes the controller snapshot.
        foreach (string variable in reservedDeadlineVariables)
        {
            environmentVariables.SetVariable(new(variable, string.Empty, false, true));
        }

        return Task.CompletedTask;
    }

    public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables)
        => ValidationResult.ValidTask;
}
