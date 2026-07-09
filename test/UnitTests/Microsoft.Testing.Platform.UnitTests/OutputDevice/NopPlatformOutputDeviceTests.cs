// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.OutputDevice;

[TestClass]
public sealed class NopPlatformOutputDeviceTests
{
    [TestMethod]
    public async Task IsEnabledAsync_ReturnsFalse_SoExtensionRegistrationSkipsIt()
    {
        var device = new NopPlatformOutputDevice();

        Assert.IsFalse(await device.IsEnabledAsync());
    }

    [TestMethod]
    public void Identity_IsStableAndNonEmpty()
    {
        var device = new NopPlatformOutputDevice();

        // Assert the stable UID/DisplayName as string literals (not nameof) so that renaming
        // NopPlatformOutputDevice trips this test instead of silently changing the runtime value.
        Assert.AreEqual("NopPlatformOutputDevice", device.Uid);
        Assert.AreEqual("NopPlatformOutputDevice", device.DisplayName);

        // NopPlatformOutputDevice.Version returns the Platform assembly's PlatformVersion.Version.
        // We can't assert equality against this test assembly's PlatformVersion.Version because it
        // is an [Embedded] per-assembly constant whose value differs from the Platform assembly's,
        // so we only check the version is present (matching this test's "NonEmpty" intent).
        Assert.IsFalse(string.IsNullOrWhiteSpace(device.Version));
        Assert.IsFalse(string.IsNullOrWhiteSpace(device.Description));
    }

    [TestMethod]
    public async Task DisplayMethods_AreNoOps_AndDoNotThrow()
    {
        var device = new NopPlatformOutputDevice();
        Mock<IOutputDeviceDataProducer> producerMock = new();
        Mock<IOutputDeviceData> dataMock = new();

        // All Display* and HandleProcessRole methods should be no-ops that complete successfully.
        await device.DisplayAsync(producerMock.Object, dataMock.Object, CancellationToken.None);
        await device.DisplayBannerAsync("banner", CancellationToken.None);
        await device.DisplayBannerAsync(bannerMessage: null, CancellationToken.None);
        await device.DisplayBeforeSessionStartAsync(CancellationToken.None);
        await device.DisplayAfterSessionEndRunAsync(CancellationToken.None);
        await device.HandleProcessRoleAsync(TestProcessRole.TestHost, CancellationToken.None);
        await device.HandleProcessRoleAsync(TestProcessRole.TestHostController, CancellationToken.None);
        await device.HandleProcessRoleAsync(TestProcessRole.TestHostOrchestrator, CancellationToken.None);

        // No interaction with the producer/data should have occurred.
        producerMock.VerifyNoOtherCalls();
        dataMock.VerifyNoOtherCalls();
    }
}
