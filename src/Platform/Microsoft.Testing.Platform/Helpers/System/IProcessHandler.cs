﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal interface IProcessHandler
{
    IProcess GetProcessById(int pid);

    IProcess GetCurrentProcess();

    IProcess Start(ProcessStartInfo startInfo);
}
