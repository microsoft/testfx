// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class WasiOutputDeviceTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WasiFacade_UsesInjectedConsoleForAllSeverityRoutesAndDynamicIdentity(bool dynamicRuntime)
    {
        List<string?> written = [];
        Mock<IConsole> console = new();
        console.Setup(c => c.WriteLine(It.IsAny<string?>())).Callback<string?>(written.Add);
        Mock<ITestApplicationModuleInfo> module = new();
        module.Setup(m => m.GetDisplayName()).Returns("wasi-test.dll");
        Mock<IRuntimeFeature> runtime = new();
        runtime.SetupGet(r => r.IsDynamicCodeSupported).Returns(dynamicRuntime);
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariable("MTP_PROGRESS_SLOW_TEST_SECONDS")).Returns("0");
        using SystemAsyncMonitor monitor = new();
        Mock<IStopPoliciesService> policies = new();
        policies.Setup(p => p.RegisterOnAbortCallbackAsync(It.IsAny<Func<Task>>())).Returns(Task.CompletedTask);
        // This facade only calls the supplied IConsole; no WASI-only runtime or native API is invoked.
#pragma warning disable CA1416
        SimplifiedConsoleOutputDeviceBase device = new WasiOutputDevice(console.Object, module.Object, monitor, runtime.Object, environment.Object, new Mock<IPlatformInformation>().Object, policies.Object);
#pragma warning restore CA1416
        await device.InitializeAsync();
        Assert.IsTrue(await device.IsEnabledAsync());
        Assert.AreEqual("WasiOutputDevice", device.Uid);
        Assert.AreEqual("Test Platform WASI Console Service", device.DisplayName);
        Assert.AreEqual("Test Platform WASI console service", device.Description);
        Assert.AreEqual("2.6.0-dev", device.Version);
        await device.DisplayBannerAsync("banner", CancellationToken.None);
        await device.DisplayBannerAsync("must not repeat", CancellationToken.None);
        IOutputDeviceDataProducer producer = new Mock<IOutputDeviceDataProducer>().Object;
        await device.DisplayAsync(producer, new TextOutputDeviceData("information"), CancellationToken.None);
        await device.DisplayAsync(producer, new WarningMessageOutputDeviceData("warning"), CancellationToken.None);
        await device.DisplayAsync(producer, new ErrorMessageOutputDeviceData("error"), CancellationToken.None);
        await device.DisplayBeforeSessionStartAsync(CancellationToken.None);

        string identity;
        if (dynamicRuntime)
        {
#if NETFRAMEWORK
            const string Target = "net462";
            string architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
#elif NET11_0_OR_GREATER
            const string Target = "net11.0";
            string architecture = RuntimeInformation.RuntimeIdentifier;
#elif NET10_0_OR_GREATER
            const string Target = "net10.0";
            string architecture = RuntimeInformation.RuntimeIdentifier;
#elif NET9_0_OR_GREATER
            const string Target = "net9.0";
            string architecture = RuntimeInformation.RuntimeIdentifier;
#else
            const string Target = "net8.0";
            string architecture = RuntimeInformation.RuntimeIdentifier;
#endif
            identity = "wasi-test.dll (" + Target + "|" + architecture + ")";
        }
        else
        {
            identity = "wasi-test.dll";
        }

        Assert.AreSequenceEqual<string?>(["banner", "information", "warning", "error", identity], written);
        environment.Verify(e => e.SetEnvironmentVariable("TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER", "1"), Times.Once);
        policies.Verify(p => p.RegisterOnAbortCallbackAsync(It.IsAny<Func<Task>>()), Times.Once);
    }

    [TestMethod]
    public void Banner_OptionalIdentityAndDynamicRuntimePreserveExactUtcPresentation()
    {
        Mock<IPlatformInformation> platform = new();
        platform.SetupGet(p => p.Name).Returns("Fixture");
        platform.SetupGet(p => p.Version).Returns("2.3");
        platform.SetupGet(p => p.CommitHash).Returns("0123456789abcdef");
        platform.SetupGet(p => p.BuildDate).Returns(new DateTimeOffset(2025, 2, 8, 1, 2, 0, TimeSpan.FromHours(2)));
        Mock<IRuntimeFeature> runtime = new();
        runtime.SetupGet(r => r.IsDynamicCodeSupported).Returns(true);
#if NETFRAMEWORK
        string expectedDate = new DateTime(2025, 2, 7).ToString("d", CultureInfo.CurrentCulture);
#else
        const string ExpectedDate = "02/07/2025";
        string expectedDate = ExpectedDate;
#endif
        Assert.AreEqual(
            "Fixture v2.3+0123456789 (UTC " + expectedDate + ") [test-arch - net8.0]",
            OutputDeviceBannerHelper.BuildBannerText(platform.Object, runtime.Object, "test-arch", "net8.0"));
        platform.SetupGet(p => p.CommitHash).Returns("abc");
        platform.SetupGet(p => p.BuildDate).Returns((DateTimeOffset?)null);
        Assert.AreEqual("Fixture v2.3+abc [test-arch - net8.0]", OutputDeviceBannerHelper.BuildBannerText(platform.Object, runtime.Object, "test-arch", "net8.0"));
        platform.SetupGet(p => p.Version).Returns((string?)null);
        runtime.SetupGet(r => r.IsDynamicCodeSupported).Returns(false);
        Assert.AreEqual("Fixture", OutputDeviceBannerHelper.BuildBannerText(platform.Object, runtime.Object, "must not show", "must not show"));
    }
}
