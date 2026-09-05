// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class TestHostControllerCancellationTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task RequestCancellation_CancelsChildApplication()
    {
        Mock<ILoggerFactory> loggerFactory = CreateLoggerFactory();
        SystemEnvironment environment = new();
        using var server = new TestHostControllerCancellationServer(
            authorizedSecurityIdentities: null,
            environment,
            loggerFactory.Object,
            new SystemTask());
        server.Start();
        using CTRLPlusCCancellationTokenSource applicationCancellationTokenSource = new();
        using var listener = new TestHostControllerCancellationListener(
            server.PipeName,
            applicationCancellationTokenSource,
            environment,
            new NopLogger());

        await server.WaitForRequestAsync().TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        server.RequestCancellation();

        Assert.IsTrue(
            applicationCancellationTokenSource.CancellationToken.WaitHandle.WaitOne(TimeoutHelper.DefaultHangTimeSpanTimeout),
            "The controller cancellation request was not propagated to the child application.");
        Assert.IsTrue(listener.WasCancellationRequestedByController);
    }

    [TestMethod]
    public async Task ServerDisposal_WhenClientConnectsWithoutSendingRequest_DoesNotHang()
    {
        Mock<ILoggerFactory> loggerFactory = CreateLoggerFactory();
        SystemEnvironment environment = new();
        var server = new TestHostControllerCancellationServer(
            authorizedSecurityIdentities: null,
            environment,
            loggerFactory.Object,
            new SystemTask());
        server.Start();
        using var client = new NamedPipeClient(server.PipeName, environment, exitProcessOnConnectionLoss: false);
        await client.ConnectAsync(TestContext.CancellationToken);

        await Task.Run(server.Dispose, TestContext.CancellationToken).TimeoutAfterAsync(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public async Task ServerDisposal_WhenClientKeepsConnectionOpenAfterRequest_DoesNotHang()
    {
        Mock<ILoggerFactory> loggerFactory = CreateLoggerFactory();
        SystemEnvironment environment = new();
        var server = new TestHostControllerCancellationServer(
            authorizedSecurityIdentities: null,
            environment,
            loggerFactory.Object,
            new SystemTask());
        server.Start();
        using var client = new System.IO.Pipes.NamedPipeClientStream(
            ".",
            server.PipeName,
            System.IO.Pipes.PipeDirection.InOut,
            System.IO.Pipes.PipeOptions.Asynchronous);
        await client.ConnectAsync(TestContext.CancellationToken);
        byte[] requestFrame = [.. BitConverter.GetBytes(sizeof(int)), .. BitConverter.GetBytes(13)];
        await client.WriteAsync(requestFrame, 0, requestFrame.Length, TestContext.CancellationToken);
        await client.FlushAsync(TestContext.CancellationToken);
        await server.WaitForRequestAsync().TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

        await Task.Run(server.Dispose, TestContext.CancellationToken).TimeoutAfterAsync(TimeSpan.FromSeconds(5));

        byte[] responseBuffer = new byte[64];
        int bytesRead = await client.ReadAsync(responseBuffer, 0, responseBuffer.Length, TestContext.CancellationToken);

        Assert.AreEqual(0, bytesRead);
    }

    [TestMethod]
    public async Task NormalServerDisposal_DoesNotCancelChildApplication()
    {
        for (int i = 0; i < 20; i++)
        {
            Mock<ILoggerFactory> loggerFactory = CreateLoggerFactory();
            SystemEnvironment environment = new();
            var server = new TestHostControllerCancellationServer(
                authorizedSecurityIdentities: null,
                environment,
                loggerFactory.Object,
                new SystemTask());
            server.Start();
            using CTRLPlusCCancellationTokenSource applicationCancellationTokenSource = new();
            using var listener = new TestHostControllerCancellationListener(
                server.PipeName,
                applicationCancellationTokenSource,
                environment,
                new NopLogger());

            await server.WaitForRequestAsync().TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
            server.Dispose();

            Assert.IsFalse(applicationCancellationTokenSource.CancellationToken.IsCancellationRequested);
            Assert.IsFalse(listener.WasCancellationRequestedByController);
        }
    }

    private static Mock<ILoggerFactory> CreateLoggerFactory()
    {
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new NopLogger());
        return loggerFactory;
    }
}
