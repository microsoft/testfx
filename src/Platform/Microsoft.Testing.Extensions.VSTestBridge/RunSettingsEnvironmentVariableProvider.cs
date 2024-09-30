// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Extensions.VSTestBridge;

internal sealed class RunSettingsEnvironmentVariableProvider : ITestHostEnvironmentVariableProvider
{
    public RunSettingsEnvironmentVariableProvider(IExtension extension)
    {
        Uid = extension.Uid;
        Version = extension.Version;
        DisplayName = extension.DisplayName;
        Description = extension.Description;
    }

    public string Uid { get; }

    public string Version { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public Task<bool> IsEnabledAsync() => throw new NotImplementedException();

    public Task UpdateAsync(IEnvironmentVariables environmentVariables) => throw new NotImplementedException();

    public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables) => throw new NotImplementedException();
}
