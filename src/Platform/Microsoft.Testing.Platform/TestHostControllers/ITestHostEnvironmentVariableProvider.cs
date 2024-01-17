// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

public interface ITestHostEnvironmentVariableProvider : ITestHostControllersExtension
{
    Task UpdateAsync(IEnvironmentVariables environmentVariables);

    Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables);
}
