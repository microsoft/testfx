// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.TestHostControllers;

internal sealed class SystemEnvironmentVariableProvider(IEnvironment environment) : ITestHostEnvironmentVariableProvider
{
    private readonly SystemExtension _systemExtension = new();
    private readonly IEnvironment _environment = environment;

    public string Uid => _systemExtension.Uid;

    public string Version => _systemExtension.Version;

    public string DisplayName => _systemExtension.DisplayName;

    public string Description => _systemExtension.Description;

    public async Task<bool> IsEnabledAsync() => await _systemExtension.IsEnabledAsync();

    public Task UpdateAsync(IEnvironmentVariables environmentVariables)
    {
        foreach (DictionaryEntry entry in _environment.GetEnvironmentVariables())
        {
            environmentVariables.SetVariable(new(entry.Key.ToString()!, entry.Value!.ToString(), false, false));
        }

        return Task.CompletedTask;
    }

    public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables)
        => ValidationResult.ValidTask;
}
