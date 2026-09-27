// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using MelEventId = Microsoft.Extensions.Logging.EventId;
using MelILogger = Microsoft.Extensions.Logging.ILogger;
using MelILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;
using MelILoggerProvider = Microsoft.Extensions.Logging.ILoggerProvider;
using MelLoggerFactory = Microsoft.Extensions.Logging.LoggerFactory;
using MelLogLevel = Microsoft.Extensions.Logging.LogLevel;
using MtpILoggerFactory = Microsoft.Testing.Platform.Logging.ILoggerFactory;
using MtpLogLevel = Microsoft.Testing.Platform.Logging.LogLevel;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class MicrosoftExtensionsLoggingBuilderExtensionsTests
{
    [TestMethod]
    public void AddMicrosoftExtensionsLogging_WithNullBuilderAndConfigureDelegate_Throws()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => ((ITestApplicationBuilder)null!).AddMicrosoftExtensionsLogging(static _ => { }));

        Assert.AreEqual("builder", exception.ParamName);
    }

    [TestMethod]
    public async Task AddMicrosoftExtensionsLogging_WithNullConfigureDelegate_Throws()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => builder.AddMicrosoftExtensionsLogging((Action<ILoggingBuilder>)null!));

        Assert.AreEqual("configure", exception.ParamName);
    }

    [TestMethod]
    public void AddMicrosoftExtensionsLogging_WithNullBuilderAndLoggerFactory_Throws()
    {
        using MelILoggerFactory loggerFactory = MelLoggerFactory.Create(static _ => { });

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => ((ITestApplicationBuilder)null!).AddMicrosoftExtensionsLogging(loggerFactory));

        Assert.AreEqual("builder", exception.ParamName);
    }

    [TestMethod]
    public async Task AddMicrosoftExtensionsLogging_WithNullLoggerFactory_Throws()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => builder.AddMicrosoftExtensionsLogging((MelILoggerFactory)null!));

        Assert.AreEqual("loggerFactory", exception.ParamName);
    }

    [TestMethod]
    public async Task AddMicrosoftExtensionsLogging_WithConfigureDelegateAndNoneLevel_DoesNotInvokeConfigureDelegate()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        bool configured = false;

        builder.AddMicrosoftExtensionsLogging(_ => configured = true);

        using var loggerFactory = (IDisposable)await BuildLoggerFactoryAsync(builder, MtpLogLevel.None);

        Assert.IsFalse(configured);
    }

    [TestMethod]
    public async Task AddMicrosoftExtensionsLogging_WithLoggerFactoryAndNoneLevel_DoesNotUseFactory()
    {
        CapturingLoggerFactory loggerFactory = new();
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        builder.AddMicrosoftExtensionsLogging(loggerFactory);

        using var mtpLoggerFactory = (IDisposable)await BuildLoggerFactoryAsync(builder, MtpLogLevel.None);
        ((MtpILoggerFactory)mtpLoggerFactory).CreateLogger("category").Log(MtpLogLevel.Error, "message", null, static (state, _) => state);

        Assert.AreEqual(0, loggerFactory.CreateLoggerCallCount);
        Assert.AreEqual(0, loggerFactory.DisposeCallCount);
    }

    [TestMethod]
    public async Task AddMicrosoftExtensionsLogging_WithConfigureDelegate_ForwardsLogsAndDisposesOwnedFactory()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        CapturingLoggerProvider provider = new();

        ITestApplicationBuilder returnedBuilder = builder.AddMicrosoftExtensionsLogging(
            logging => logging.Services.AddSingleton<MelILoggerProvider>(_ => provider));

        Assert.AreSame(builder, returnedBuilder);
        using (var loggerFactory = (IDisposable)await BuildLoggerFactoryAsync(builder, MtpLogLevel.Information))
        {
            ((MtpILoggerFactory)loggerFactory).CreateLogger("category").Log(MtpLogLevel.Information, "message", null, static (state, _) => state);
        }

        Assert.AreEqual("category", provider.CategoryName);
        Assert.AreEqual(MelLogLevel.Information, provider.LogLevel);
        Assert.AreEqual("message", provider.Message);
        Assert.AreEqual(1, provider.DisposeCallCount);
    }

    [TestMethod]
    public async Task AddMicrosoftExtensionsLogging_WithLoggerFactory_ForwardsLogsWithoutDisposingCallerFactory()
    {
        CapturingLoggerFactory loggerFactory = new();
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);

        ITestApplicationBuilder returnedBuilder = builder.AddMicrosoftExtensionsLogging(loggerFactory);

        Assert.AreSame(builder, returnedBuilder);
        using (var mtpLoggerFactory = (IDisposable)await BuildLoggerFactoryAsync(builder, MtpLogLevel.Warning))
        {
            ((MtpILoggerFactory)mtpLoggerFactory).CreateLogger("category").Log(MtpLogLevel.Warning, "message", null, static (state, _) => state);
        }

        Assert.AreEqual("category", loggerFactory.Provider.CategoryName);
        Assert.AreEqual(MelLogLevel.Warning, loggerFactory.Provider.LogLevel);
        Assert.AreEqual("message", loggerFactory.Provider.Message);
        Assert.AreEqual(0, loggerFactory.DisposeCallCount);
    }

    [TestMethod]
    public async Task AddMicrosoftExtensionsLogging_ReturnsSameBuilder()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        using MelILoggerFactory loggerFactory = MelLoggerFactory.Create(static _ => { });

        Assert.AreSame(builder, builder.AddMicrosoftExtensionsLogging(loggerFactory));
        Assert.AreSame(builder, builder.AddMicrosoftExtensionsLogging(static _ => { }));
    }

    private static Task<MtpILoggerFactory> BuildLoggerFactoryAsync(ITestApplicationBuilder builder, MtpLogLevel logLevel)
        => ((LoggingManager)((TestApplicationBuilder)builder).Logging).BuildAsync(new Microsoft.Testing.Platform.Services.ServiceProvider(), logLevel, new SystemMonitor());

    private sealed class CapturingLoggerFactory : MelILoggerFactory
    {
        public CapturingLoggerProvider Provider { get; } = new();

        public int CreateLoggerCallCount { get; private set; }

        public int DisposeCallCount { get; private set; }

        public void AddProvider(MelILoggerProvider provider)
            => throw new NotSupportedException();

        public MelILogger CreateLogger(string categoryName)
        {
            CreateLoggerCallCount++;
            return Provider.CreateLogger(categoryName);
        }

        public void Dispose()
            => DisposeCallCount++;
    }

    private sealed class CapturingLoggerProvider : MelILoggerProvider
    {
        public string? CategoryName { get; private set; }

        public MelLogLevel? LogLevel { get; private set; }

        public string? Message { get; private set; }

        public int DisposeCallCount { get; private set; }

        public MelILogger CreateLogger(string categoryName)
        {
            CategoryName = categoryName;
            return new CapturingLogger(this);
        }

        public void Dispose()
            => DisposeCallCount++;

        private sealed class CapturingLogger(CapturingLoggerProvider provider) : MelILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(MelLogLevel logLevel)
                => true;

            public void Log<TState>(
                MelLogLevel logLevel,
                MelEventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                provider.LogLevel = logLevel;
                provider.Message = formatter(state, exception);
            }
        }
    }
}
