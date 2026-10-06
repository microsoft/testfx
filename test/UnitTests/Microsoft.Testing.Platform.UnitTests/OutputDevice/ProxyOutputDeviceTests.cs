// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.OutputDevice;

[TestClass]
public sealed class ProxyOutputDeviceTests
{
    [TestMethod]
    public async Task Dispose_DisposesServerModeDeviceButNotOriginalDevice()
    {
        Mock<IPlatformOutputDevice> originalDevice = new();
        Mock<IDisposable> originalDisposable = originalDevice.As<IDisposable>();
        var serverModeDevice = new ServerModePerCallOutputDevice(
            fileLoggerProvider: null,
            Mock.Of<IStopPoliciesService>());
        var proxy = new ProxyOutputDevice(originalDevice.Object, serverModeDevice);

        proxy.Dispose();

        originalDisposable.Verify(x => x.Dispose(), Times.Never);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            () => serverModeDevice.DisplayAsync(
                Mock.Of<IOutputDeviceDataProducer>(producer => producer.Uid == "producer"),
                new ProgressMessageOutputDeviceData("key", "message"),
                CancellationToken.None));
    }
}
