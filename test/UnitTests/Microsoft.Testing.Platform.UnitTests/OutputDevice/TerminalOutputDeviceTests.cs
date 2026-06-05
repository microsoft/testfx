// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostControllers;
using Microsoft.Testing.Platform.UnitTests.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.OutputDevice;

[TestClass]
[DoNotParallelize]
[UnsupportedOSPlatform("browser")]
public sealed class TerminalOutputDeviceTests
{
    private static readonly SemaphoreSlim ConsoleErrorSemaphore = new(1, 1);

    [TestMethod]
    public async Task DisplayAsync_ListTestsJsonInAzureDevOps_WritesPlainErrorOnlyToStandardError()
        => await AssertListTestsJsonStandardErrorAsync(new ErrorMessageOutputDeviceData("boom"), "boom");

    [TestMethod]
    public async Task DisplayAsync_ListTestsJsonInAzureDevOps_WritesPlainExceptionOnlyToStandardError()
    {
        var exception = new InvalidOperationException("boom");

        await AssertListTestsJsonStandardErrorAsync(new ExceptionOutputDeviceData(exception), exception.Message);
    }

    private static async Task AssertListTestsJsonStandardErrorAsync(IOutputDeviceData data, string expectedMessage)
    {
        await ConsoleErrorSemaphore.WaitAsync();
        TextWriter originalError = Console.Error;
        using var errorWriter = new StringWriter(CultureInfo.InvariantCulture);
        Console.SetError(errorWriter);

        try
        {
            using TerminalOutputDevice outputDevice = CreateListTestsJsonAzureDevOpsOutputDevice();
            await outputDevice.InitializeAsync();

            await outputDevice.DisplayAsync(Mock.Of<IOutputDeviceDataProducer>(), data, CancellationToken.None);

            string standardError = errorWriter.ToString();
            Assert.Contains(expectedMessage, standardError);
            // Replace "##" so the assertion failure (if it fires) does not itself contain a literal
            // ##vso command that an Azure DevOps agent watching the test output could mistakenly act on.
            Assert.IsFalse(standardError.Contains("##vso[task.logissue", StringComparison.Ordinal), standardError.Replace("##", "[hash][hash]"));
        }
        finally
        {
            Console.SetError(originalError);
            ConsoleErrorSemaphore.Release();
        }
    }

    private static TerminalOutputDevice CreateListTestsJsonAzureDevOpsOutputDevice()
    {
        var testApplicationModuleInfo = new Mock<ITestApplicationModuleInfo>();
        testApplicationModuleInfo.Setup(x => x.GetDisplayName()).Returns("testhost");

        var environment = new Mock<IEnvironment>();
        environment.Setup(x => x.GetEnvironmentVariable(It.IsAny<string>()))
            .Returns<string>(name => name == "TF_BUILD" ? "true" : null);

        var stopPoliciesService = new Mock<IStopPoliciesService>();
        stopPoliciesService.Setup(x => x.RegisterOnAbortCallbackAsync(It.IsAny<Func<Task>>()))
            .Returns(Task.CompletedTask);

        var testApplicationCancellationTokenSource = new Mock<ITestApplicationCancellationTokenSource>();
        testApplicationCancellationTokenSource.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);

        return new TerminalOutputDevice(
            Mock.Of<IConsole>(),
            testApplicationModuleInfo.Object,
            Mock.Of<ITestHostControllerInfo>(),
            Mock.Of<IAsyncMonitor>(),
            Mock.Of<IRuntimeFeature>(),
            environment.Object,
            Mock.Of<IPlatformInformation>(),
            new TestCommandLineOptions(new Dictionary<string, string[]>
            {
                [PlatformCommandLineProvider.DiscoverTestsOptionKey] = [PlatformCommandLineProvider.DiscoverTestsJsonArgument],
            }),
            fileLoggerInformation: null,
            Mock.Of<ILoggerFactory>(),
            Mock.Of<IClock>(),
            stopPoliciesService.Object,
            testApplicationCancellationTokenSource.Object);
    }
}
