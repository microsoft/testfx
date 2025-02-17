// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal interface IProcessHandler
{
    [UnsupportedOSPlatform("browser")]
    IProcess GetProcessById(int pid);

    [UnsupportedOSPlatform("browser")]
    IProcess GetCurrentProcess();

    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    IProcess Start(ProcessStartInfo startInfo);
}
