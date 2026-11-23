// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class CTRLPlusCConsoleCancelTests
{
    [TestMethod]
    public void CancelKeyPress_SuppressesTheRuntimeDefaultTermination_OnTheFirstAndTheSecondPress()
    {
        Mock<IConsole> console = new();
        Mock<IEnvironment> environment = new();
        using var source = new CTRLPlusCCancellationTokenSource(console.Object, logger: null, environment.Object);

        ConsoleCancelEventArgs first = CreateCancelEventArgs();
        console.Raise(c => c.CancelKeyPress += null, console.Object, first);
        Assert.IsTrue(first.Cancel, "The first press must set e.Cancel so the runtime does not terminate the process.");
        environment.Verify(e => e.Exit(It.IsAny<int>()), Times.Never);

        ConsoleCancelEventArgs second = CreateCancelEventArgs();
        console.Raise(c => c.CancelKeyPress += null, console.Object, second);
        Assert.IsTrue(second.Cancel, "The second press must also set e.Cancel so that only our force exit terminates the process.");
        environment.Verify(e => e.Exit((int)ExitCode.TestSessionAborted), Times.Once);
    }

    [TestMethod]
    public void Dispose_DisposesTheUnderlyingCancellationTokenSource()
    {
        var source = new CTRLPlusCCancellationTokenSource();
        Assert.IsFalse(source.CancellationToken.IsCancellationRequested);

        source.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = source.CancellationToken);
    }

    // ConsoleCancelEventArgs has no public constructor; an uninitialized instance has Cancel == false and
    // SpecialKey == ControlC, which is all the handler under test needs.
    private static ConsoleCancelEventArgs CreateCancelEventArgs()
#if NETFRAMEWORK
        => (ConsoleCancelEventArgs)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ConsoleCancelEventArgs));
#else
        => (ConsoleCancelEventArgs)RuntimeHelpers.GetUninitializedObject(typeof(ConsoleCancelEventArgs));
#endif
}
