// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Platform.TestHostControllers;

internal class TestHostProcessInformation : ITestHostProcessInformation
{
    private readonly int? _exitCode;
    private readonly bool? _hasExitedGracefully;

    public TestHostProcessInformation(int pid)
    {
        PID = pid;
    }

    public TestHostProcessInformation(int pid, int exitCode, bool hasExitedGracefully)
    {
        PID = pid;
        _exitCode = exitCode;
        _hasExitedGracefully = hasExitedGracefully;
    }

    public int PID { get; }

    public int ExitCode
        => _exitCode is null
            ? throw new InvalidOperationException("Process must exit before requested information can be determined.")
            : _exitCode.Value;

    public bool HasExitedGracefully => _hasExitedGracefully is null
        ? throw new InvalidOperationException("Process must exit before requested information can be determined.")
        : _hasExitedGracefully.Value;
}
