// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.TestHostControllers;

internal interface ITestHostControllerInfo
{
    bool HasTestHostController { get; }

    bool CurrentProcessIsTestHostController { get; set; }

    int? GetTestHostControllerPID(bool throwIfMissing = true);
}
