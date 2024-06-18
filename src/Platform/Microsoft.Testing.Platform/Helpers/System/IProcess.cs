// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal interface IProcess : IDisposable
{
    public event EventHandler Exited;

    int Id { get; }

    int ExitCode { get; }

    bool HasExited { get; }

#if NETCOREAPP
    IMainModule? MainModule { get; }
#else
    IMainModule MainModule { get; }
#endif

#if NETCOREAPP
    Task WaitForExitAsync();

#endif
    void WaitForExit();

    void Kill();
}
