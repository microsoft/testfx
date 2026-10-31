// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Hosting;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class HotReloadRegistrationBehaviorTests
{
    [TestMethod]
    [DataRow(false, DisplayName = "HotReload direct disabled registration")]
    [DataRow(true, DisplayName = "HotReload hook disabled registration")]
    public async Task DisabledRegisteredInvoker_ExecutesOneOriginalRequestWithoutHotReloadState(bool hook)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        if (hook)
        {
            HotReload.TestingPlatformBuilderHook.AddExtensions(builder, ["unused"]);
        }
        else
        {
            builder.AddHotReloadProvider();
        }

        var fixture = new ReportBehaviorFixture();
        var stopPolicies = new Mock<IStopPoliciesService>(MockBehavior.Strict);
        fixture.Services.AddService(stopPolicies.Object);
        ActionResult<ITestFrameworkInvoker> result = await ((TestHostManager)builder.TestHost).TryBuildTestAdapterInvokerAsync(fixture.Services);
        Assert.IsTrue(result.IsSuccess);
        var invoker = Assert.IsInstanceOfType<HotReloadTestHostTestFrameworkInvoker>(result.Result);
        using var cancellation = new CancellationTokenSource();
        var request = new RunTestExecutionRequest(new Microsoft.Testing.Platform.TestHost.TestSessionContext(new SessionUid("registered-session")));
        ExecuteRequestContext? received = null;
        fixture.Framework.Setup(value => value.ExecuteRequestAsync(It.IsAny<ExecuteRequestContext>()))
            .Callback<ExecuteRequestContext>(context =>
            {
                received = context;
                context.Complete();
            })
            .Returns(Task.CompletedTask);

        await invoker.ExecuteRequestAsync(fixture.Framework.Object, request, fixture.MessageBus.Object, cancellation.Token);

        Assert.IsNotNull(received);
        Assert.AreSame(request, received.Request);
        Assert.AreSame(fixture.MessageBus.Object, received.MessageBus);
        Assert.AreEqual(cancellation.Token, received.CancellationToken);
        fixture.Framework.Verify(value => value.ExecuteRequestAsync(It.IsAny<ExecuteRequestContext>()), Times.Once);
        stopPolicies.VerifyNoOtherCalls();
        Assert.IsEmpty(fixture.Artifacts);
        Assert.IsEmpty(fixture.Events);
    }
}
