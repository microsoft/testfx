// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

public interface ITestHostEnvironmentVariableProvider : ITestHostControllersExtension
{
    void Update(IEnvironmentVariables environmentVariables);

    bool AreValid(IReadOnlyEnvironmentVariables environmentVariables, out string? errorMessage);
}
