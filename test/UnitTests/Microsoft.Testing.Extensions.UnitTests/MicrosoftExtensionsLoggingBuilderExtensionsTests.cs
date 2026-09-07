// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Testing.Extensions.Logging;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

using Moq;

using MelILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;
using MelLoggerFactory = Microsoft.Extensions.Logging.LoggerFactory;
using MelLoggingBuilder = Microsoft.Extensions.Logging.ILoggingBuilder;
using MtpILogger = Microsoft.Testing.Platform.Logging.ILogger;
using MtpILoggerFactory = Microsoft.Testing.Platform.Logging.ILoggerFactory;
using MtpLogLevel = Microsoft.Testing.Platform.Logging.LogLevel;

namespace Microsoft.Testing.Extensions.UnitTests;

/// <summary>
/// Tests for the two <see cref="MicrosoftExtensionsLoggingBuilderExtensions.AddMicrosoftExtensionsLogging"/>
/// overloads. Both overloads only register a provider factory with <see cref="ILoggingManager"/>; this test
/// drives that factory the same way <c>TestHostBuilder.CommonServices</c> does in production - by calling the
/// internal <see cref="LoggingManager.BuildAsync"/> - so the provider construction logic (short-circuiting on
/// <see cref="MtpLogLevel.None"/>, ownership of the created <see cref="MelILoggerFactory"/>) is covered end to end.
/// </summary>
#pragma warning disable TPEXP // ITestApplicationBuilder.Logging and the extension methods under test are experimental.
[TestClass]
public sealed class MicrosoftExtensionsLoggingBuilderExtensionsTests
{
    [TestMethod]
    public void AddMicrosoftExtensionsLogging_WithConfigureDelegate_WithNullBuilder_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(
            () => ((ITestApplicationBuilder)null!).AddMicrosoftExtensionsLogging(_ => { }));

    [TestMethod]
    public async Task AddMicrosoftExtensionsLogging_WithConfigureDelegate_WithNullConfigure_Throws()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);

        Assert.ThrowsExactly<ArgumentNullException>(
            () => builder.AddMicrosoftExtensionsLogging((Action<MelLoggingBuilder>)null!));
    }

    [TestMethod]
    public void AddMicrosoftExtensionsLogging_WithLoggerFactory_WithNullBuilder_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(
            () => ((ITestApplicationBuilder)null!).AddMicrosoftExtensionsLogging(MelLoggerFactory.Create(_ => { })));

    [TestMethod]
    public async Task AddMicrosoftExtensionsLogging_WithLoggerFactory_WithNullLoggerFactory_Throws()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);

        Assert.ThrowsExactly<ArgumentNullException>(
            () => builder.AddMicrosoftExtensionsLogging((MelILoggerFactory)null!));
    }

    [TestMethod]
    public async Task AddMicrosoftExtensionsLogging_WithConfigureDelegate_WhenPlatformLevelIsNone_SkipsConfigureAndForwardsNothing()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        bool configureInvoked = false;

        builder.AddMicrosoftExtensionsLogging(_ => configureInvoked = true);

        MtpILoggerFactory mtpLoggerFactory = await BuildLoggerFactoryAsync(builder, MtpLogLevel.None);
        MtpILogger logger = mtpLoggerFactory.CreateLogger("MyCategory");

        Assert.IsFalse(configureInvoked);
        Assert.IsFalse(logger.IsEnabled(MtpLogLevel.Critical));
    }

    [TestMethod]
    public async Task AddMicrosoftExtensionsLogging_WithConfigureDelegate_WhenPlatformLevelIsNotNone_InvokesConfigureAndForwardsLogs()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        bool configureInvoked = false;
        List<string> capturedMessages = [];

        builder.AddMicrosoftExtensionsLogging(loggingBuilder =>
        {
            configureInvoked = true;
            loggingBuilder.AddProvider(new CapturingLoggerProvider(capturedMessages));
        });

        MtpILoggerFactory mtpLoggerFactory = await BuildLoggerFactoryAsync(builder, MtpLogLevel.Information);
        MtpILogger logger = mtpLoggerFactory.CreateLogger("MyCategory");
        await logger.LogAsync(MtpLogLevel.Information, "hello", exception: null, (state, _) => state);

        Assert.IsTrue(configureInvoked);
        Assert.ContainsSingle(capturedMessages);
        Assert.AreEqual("hello", capturedMessages[0]);
    }

    [TestMethod]
    public async Task AddMicrosoftExtensionsLogging_WithLoggerFactory_WhenPlatformLevelIsNone_DoesNotTouchFactory()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        Mock<MelILoggerFactory> loggerFactoryMock = new(MockBehavior.Strict);

        builder.AddMicrosoftExtensionsLogging(loggerFactoryMock.Object);

        MtpILoggerFactory mtpLoggerFactory = await BuildLoggerFactoryAsync(builder, MtpLogLevel.None);
        mtpLoggerFactory.CreateLogger("MyCategory");

        loggerFactoryMock.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task AddMicrosoftExtensionsLogging_WithLoggerFactory_WhenPlatformLevelIsNotNone_ForwardsLogsAndDoesNotOwnFactory()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        List<string> capturedMessages = [];
        using MelILoggerFactory loggerFactory = MelLoggerFactory.Create(loggingBuilder => loggingBuilder.AddProvider(new CapturingLoggerProvider(capturedMessages)));

        builder.AddMicrosoftExtensionsLogging(loggerFactory);

        // ownsFactory is false for this overload, so the caller-owned loggerFactory must remain usable
        // after the MTP-side ILoggerFactory has produced and used a logger from it.
        MtpILoggerFactory mtpLoggerFactory = await BuildLoggerFactoryAsync(builder, MtpLogLevel.Information);
        MtpILogger logger = mtpLoggerFactory.CreateLogger("MyCategory");
        await logger.LogAsync(MtpLogLevel.Information, "hello", exception: null, (state, _) => state);

        Assert.ContainsSingle(capturedMessages);
        Assert.AreEqual("hello", capturedMessages[0]);
    }

    [TestMethod]
    public async Task AddMicrosoftExtensionsLogging_ReturnsSameBuilderForChaining()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);

        ITestApplicationBuilder result = builder.AddMicrosoftExtensionsLogging(_ => { });

        Assert.AreSame(builder, result);
    }

    /// <summary>
    /// Builds the <see cref="MtpILoggerFactory"/> from the <see cref="LoggingManager"/> registered on
    /// <paramref name="builder"/>, mirroring the call made from <c>TestHostBuilder.CommonServices</c> in
    /// production, so the provider factory delegate registered by the extension under test actually runs.
    /// </summary>
    private static Task<MtpILoggerFactory> BuildLoggerFactoryAsync(ITestApplicationBuilder builder, MtpLogLevel logLevel)
    {
        var loggingManager = (LoggingManager)((TestApplicationBuilder)builder).Logging;
        return loggingManager.BuildAsync(new ServiceProvider(), logLevel, new SystemMonitor());
    }

    private sealed class CapturingLoggerProvider(List<string> messages) : Microsoft.Extensions.Logging.ILoggerProvider
    {
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(List<string> messages) : Microsoft.Extensions.Logging.ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => messages.Add(formatter(state, exception));
        }
    }
}
