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
        => _testHostCompletedReceived ?? throw new InvalidOperationException(PlatformResources.ProcessHasNotYetExitedErrorMessage);
}
