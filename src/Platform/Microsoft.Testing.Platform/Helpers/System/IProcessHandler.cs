// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal interface IProcessHandler
{
    Task KillAsync(int id);

    int GetCurrentProcessId();

    (int Id, string Name) GetCurrentProcessInfo();

    string? GetCurrentProcessFileName();

    IProcess Start(ProcessStartInfo startInfo);
}
