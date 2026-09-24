// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class TestHostHandleToProcessAdapterTests
{
    [TestMethod]
    public void TrustedProcessId_ForLocalProcessHandle_ReturnsLauncherProcessId()
    {
        using var adapter = new TestHostHandleToProcessAdapter(new LocalTestHostHandle(42));

        Assert.AreEqual(42, adapter.TrustedProcessId);
    }

    [TestMethod]
    public void TrustedProcessId_ForMechanismAgnosticHandle_IsNull()
    {
        using var adapter = new TestHostHandleToProcessAdapter(new RemoteTestHostHandle());

        Assert.IsNull(adapter.TrustedProcessId);
    }

    private class RemoteTestHostHandle : ITestHostHandle
    {
        public string? Identifier => "remote:42";

        public int ExitCode => 0;

        public bool HasExited => false;

        public Task WaitForExitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Terminate()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class LocalTestHostHandle(int processId) : RemoteTestHostHandle, ILocalTestHostHandle
    {
        public int ProcessId { get; } = processId;
    }
}
