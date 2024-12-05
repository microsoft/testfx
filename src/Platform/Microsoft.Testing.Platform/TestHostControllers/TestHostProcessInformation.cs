// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.TestHostControllers;

internal sealed class TestHostProcessInformation : ITestHostProcessInformation
{
    private readonly int? _exitCode;
    private readonly bool? _hasExitedGracefully;

    public TestHostProcessInformation(int pid) => PID = pid;

    public TestHostProcessInformation(int pid, int exitCode, bool hasExitedGracefully)
    {
        PID = pid;
        _exitCode = exitCode;
        _hasExitedGracefully = hasExitedGracefully;
    }

    public int PID { get; }

    public int ExitCode
        => _exitCode ?? throw new InvalidOperationException(PlatformResources.ProcessHasNotYetExitedErrorMessage);

    public bool HasExitedGracefully
        => _hasExitedGracefully ?? throw new InvalidOperationException(PlatformResources.ProcessHasNotYetExitedErrorMessage);
}
