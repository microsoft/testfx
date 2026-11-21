// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class TestApplicationBuilderRegistrationGuardTests
{
    [TestMethod]
    public async Task BuildAsync_WhenAHostWasAlreadyBuilt_RejectsASecondBuild()
    {
        TestApplicationBuilder builder = new(
            new ApplicationLoggingState(LogLevel.None, CommandLineParseResult.Empty),
            DateTimeOffset.UtcNow,
            new TestApplicationOptions { EnableTelemetry = false },
            Mock.Of<IUnhandledExceptionsHandler>(),
            ["--list-tests"]);
        _ = builder.RegisterTestFramework(
            _ => Mock.Of<ITestFrameworkCapabilities>(),
            (_, _) => Mock.Of<ITestFramework>());
        using ITestApplication first = await builder.BuildAsync();

        _ = await Assert.ThrowsExactlyAsync<InvalidOperationException>(builder.BuildAsync);
    }
}
