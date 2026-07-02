// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class GitHubActionsReporterTests
{
    [TestMethod]
    public async Task IsEnabledAsync_ReturnsTrue_WhenRunningOnGitHubActionsAsync()
    {
        GitHubActionsReporter reporter = CreateReporter(githubActions: true, options: []);
        Assert.IsTrue(await reporter.IsEnabledAsync().ConfigureAwait(false));
    }

    [TestMethod]
    public async Task IsEnabledAsync_ReturnsFalse_WhenNotOnGitHubActionsAsync()
    {
        GitHubActionsReporter reporter = CreateReporter(githubActions: false, options: []);
        Assert.IsFalse(await reporter.IsEnabledAsync().ConfigureAwait(false));
    }

    [TestMethod]
    public async Task IsEnabledAsync_ReturnsFalse_WhenGroupsExplicitlyOffAsync()
    {
        GitHubActionsReporter reporter = CreateReporter(githubActions: true, options: new Dictionary<string, string[]>
        {
            [GitHubActionsCommandLineOptions.GitHubActionsGroups] = ["off"],
        });
        Assert.IsFalse(await reporter.IsEnabledAsync().ConfigureAwait(false));
    }

    [TestMethod]
    public async Task SessionLifetime_EmitsGroupAndEndGroupAsync()
    {
        List<string> output = [];
        GitHubActionsReporter reporter = CreateReporter(githubActions: true, options: [], output, assemblyName: "MSTest.UnitTests");

        var context = new Mock<ITestSessionContext>();
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await reporter.OnTestSessionStartingAsync(context.Object).ConfigureAwait(false);
        await reporter.OnTestSessionFinishingAsync(context.Object).ConfigureAwait(false);

        Assert.HasCount(2, output);
        Assert.IsTrue(output[0].StartsWith("::group::Tests: MSTest.UnitTests", StringComparison.Ordinal), output[0]);
        Assert.AreEqual("::endgroup::", output[1]);
    }

    private static GitHubActionsReporter CreateReporter(bool githubActions, Dictionary<string, string[]> options, List<string>? output = null, string assemblyName = "Some.UnitTests")
    {
        var environment = new Mock<IEnvironment>();
        environment.Setup(e => e.GetEnvironmentVariable("GITHUB_ACTIONS")).Returns(githubActions ? "true" : null);

        // The extension is enabled only when both the GITHUB_ACTIONS env var and the --report-gh master switch
        // are set, so always seed the master switch here; these tests exercise the env/knob behavior on top of it.
        var commandLineOptions = new Dictionary<string, string[]>(options, StringComparer.OrdinalIgnoreCase)
        {
            [GitHubActionsCommandLineOptions.GitHubActionsOptionName] = [],
        };

        var outputDevice = new Mock<IOutputDevice>();
        outputDevice
            .Setup(o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, data, _) => output?.Add(((FormattedTextOutputDeviceData)data).Text))
            .Returns(Task.CompletedTask);

        var moduleInfo = new Mock<ITestApplicationModuleInfo>();
        moduleInfo.Setup(m => m.TryGetAssemblyName()).Returns(assemblyName);

        var logger = new Mock<ILogger>();
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(logger.Object);

        return new GitHubActionsReporter(
            new TestCommandLineOptions(commandLineOptions),
            environment.Object,
            outputDevice.Object,
            moduleInfo.Object,
            loggerFactory.Object);
    }
}
