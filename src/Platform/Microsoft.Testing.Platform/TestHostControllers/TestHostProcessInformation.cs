// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.TestHostControllers;

internal sealed class TestHostProcessInformation : ITestHostProcessInformation
{
    private readonly int? _exitCode;
    private readonly bool? _testHostCompletedReceived;

    public TestHostProcessInformation(int pid) => PID = pid;

    public TestHostProcessInformation(int pid, int exitCode, bool testHostCompletedReceived)
    {
        PID = pid;
        _exitCode = exitCode;
        _testHostCompletedReceived = testHostCompletedReceived;
    }

    public int PID { get; }

    public int ExitCode
        => _exitCode ?? throw new InvalidOperationException(PlatformResources.ProcessHasNotYetExitedErrorMessage);

    public bool HasExitedGracefully
        => (_testHostCompletedReceived ?? throw new InvalidOperationException(PlatformResources.ProcessHasNotYetExitedErrorMessage))
        && !WasTestHostKilled;

    private bool WasTestHostKilled
        // On Windows, Process.Kill exits with ExitCode -1.
        // https://github.com/dotnet/runtime/blob/ad38fcdefa44d7110b5065d4c46d892b1a3341ea/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.Windows.cs#L100
        //
        // On Unix, Process.Kill exits with ExitCode 137.
        // https://github.com/dotnet/runtime/blob/ad38fcdefa44d7110b5065d4c46d892b1a3341ea/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.Unix.cs#L76
        // SIGKILL is 9, and exit code is 128 + signal number.
        => OperatingSystem.IsWindows() ? ExitCode == -1 : ExitCode == 137;
}
