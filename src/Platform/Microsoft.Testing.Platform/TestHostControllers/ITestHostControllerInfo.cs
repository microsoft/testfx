// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.TestHostControllers;

internal interface ITestHostControllerInfo
{
    bool HasTestHostController { get; }

    /// <summary>
    /// Gets information whether the current process is a test controller or not.
    /// When null the value has not been set yet, it is figured out while we are building the test application.
    /// </summary>
    bool? CurrentProcessIsTestHostController { get; }

    int? GetTestHostControllerPID(bool throwIfMissing = true);
}
