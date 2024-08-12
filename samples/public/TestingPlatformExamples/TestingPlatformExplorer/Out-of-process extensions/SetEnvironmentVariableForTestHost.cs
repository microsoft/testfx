// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace TestingPlatformExplorer.OutOfProcess;

internal class SetEnvironmentVariableForTestHost : ITestHostEnvironmentVariableProvider
{
    public string Uid => nameof(SetEnvironmentVariableForTestHost);

    public string Version => "1.0.0";

    public string DisplayName => nameof(SetEnvironmentVariableForTestHost);

    public string Description => "Example of setting environment variables for the test host.";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task UpdateAsync(IEnvironmentVariables environmentVariables)
    {
        environmentVariables.SetVariable(new EnvironmentVariable("SAMPLE", "SAMPLE_VALUE", false, true));
        return Task.CompletedTask;
    }

    public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables)
        => environmentVariables.TryGetVariable("SAMPLE", out OwnedEnvironmentVariable? value) && value.Value == "SAMPLE_VALUE"
            ? ValidationResult.ValidTask
            : ValidationResult.InvalidTask("The environment variable 'SAMPLE' is not set to 'SAMPLE_VALUE'.");
}
