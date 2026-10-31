// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Extensions.VideoRecorder;
using Microsoft.Testing.Extensions.VideoRecorder.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class VideoRecorderRegistrationBehaviorTests
{
    [TestMethod]
    [DataRow(false, DisplayName = "Configured results directory is used")]
    [DataRow(true, DisplayName = "Explicit output directory bypasses configuration lookup")]
    public async Task AddVideoRecorderProvider_RegistersSharedHandlerAndUsableOptions(bool explicitOutputDirectory)
    {
        var context = new RegistrationContext();
        var configured = new List<VideoRecorderOptions>();
        context.Builder.Object.AddVideoRecorderProvider(options =>
        {
            configured.Add(options);
            options.FfmpegPath = "\0configured-ffmpeg";
            if (explicitOutputDirectory)
            {
                options.OutputDirectory = "explicit-video-results";
            }
        });
        Assert.IsEmpty(configured, "Configuration belongs to session factory materialization, not builder registration.");
        CompositeExtensionFactory<VideoRecorderSessionHandler> consumerFactory = Assert.ContainsSingle(context.Consumers);
        CompositeExtensionFactory<VideoRecorderSessionHandler> lifetimeFactory = Assert.ContainsSingle(context.Lifetimes);
        Assert.AreSame(consumerFactory, lifetimeFactory, "The data consumer and session lifecycle must share one recording state.");
        ServiceProvider services = context.Services(new TestCommandLineOptions(new() { [VideoRecorderCommandLineProvider.EnableOptionName] = [] }));

        VideoRecorderSessionHandler handler = Assert.IsInstanceOfType<VideoRecorderSessionHandler>(((ICompositeExtensionFactory)consumerFactory).GetInstance(services));
        object lifetime = ((ICompositeExtensionFactory)lifetimeFactory).GetInstance(services);
        using var cancellation = new CancellationTokenSource();
        Mock<ITestSessionContext> session = CreateSession(cancellation.Token);
        await handler.OnTestSessionStartingAsync(session.Object);
        await handler.OnTestSessionFinishingAsync(session.Object);

        Assert.AreSame(handler, lifetime);
        Assert.HasCount(1, configured);
        Assert.IsTrue(await handler.IsEnabledAsync());
        Assert.AreSequenceEqual([typeof(TestNodeUpdateMessage)], handler.DataTypesConsumed);
        Assert.AreSequenceEqual([typeof(SessionFileArtifact)], handler.DataTypesProduced);
        context.Configuration.VerifyGet(
            configuration => configuration[PlatformConfigurationConstants.PlatformResultDirectory],
            explicitOutputDirectory ? Times.Never() : Times.Once());
        context.LoggerFactory.Verify(factory => factory.CreateLogger(typeof(VideoRecorderSessionHandler).FullName!), Times.Once());
        (IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken token) = Assert.ContainsSingle(context.OutputMessages);
        Assert.AreSame(handler, producer);
        Assert.AreEqual(cancellation.Token, token);
        Assert.AreEqual(VideoRecorderResources.FfmpegNotFound, Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(data).Message);
        context.Clock.VerifyNoOtherCalls();
        context.Bus.VerifyNoOtherCalls();

        VideoRecorderCommandLineProvider provider = Assert.IsInstanceOfType<VideoRecorderCommandLineProvider>(Assert.ContainsSingle(context.OptionProviders)());
        Assert.AreSequenceEqual(
            ["capture-video", "capture-video-source", "capture-video-granularity", "capture-video-args", "capture-video-max-duration", "capture-video-chapters"],
            provider.GetCommandLineOptions().Select(option => option.Name));
        ValidationResult invalid = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new()
        {
            [VideoRecorderCommandLineProvider.ChaptersOptionName] = ["off"],
        }));
        Assert.IsFalse(invalid.IsValid);
        Assert.AreEqual(
            string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.SubOptionsRequireEnable, "capture-video"),
            invalid.ErrorMessage);
        ValidationResult valid = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(new()
        {
            [VideoRecorderCommandLineProvider.EnableOptionName] = ["always"],
            [VideoRecorderCommandLineProvider.ChaptersOptionName] = ["off"],
        }));
        Assert.IsTrue(valid.IsValid, valid.ErrorMessage);
    }

    [TestMethod]
    public async Task AddVideoRecorderProvider_ClonedSessions_DoNotLeakCommandLineOverrides()
    {
        var context = new RegistrationContext();
        var configured = new List<VideoRecorderOptions>();
        context.Builder.Object.AddVideoRecorderProvider(options =>
        {
            configured.Add(options);
            options.FfmpegPath = "\0configured-ffmpeg";
            options.OutputDirectory = "explicit-video-results";
            options.Format = VideoRecorderFormat.WebMVp9;
            options.PersistMode = VideoRecorderPersistenceMode.Always;
            options.Source = VideoCaptureSource.Screen;
            options.Granularity = VideoCaptureGranularity.PerTest;
            options.ExtraRecorderArguments = "-configured";
            options.MaxRetainedDuration = TimeSpan.FromSeconds(11);
            options.IncludeChapters = true;
        });
        Assert.IsEmpty(configured);
        ICompositeExtensionFactory template = Assert.ContainsSingle(context.Consumers);
        var firstFactory = (ICompositeExtensionFactory)template.Clone();
        var secondFactory = (ICompositeExtensionFactory)template.Clone();
        ServiceProvider firstServices = context.Services(new TestCommandLineOptions(new()
        {
            [VideoRecorderCommandLineProvider.EnableOptionName] = ["on-failure"],
            [VideoRecorderCommandLineProvider.SourceOptionName] = ["window"],
            [VideoRecorderCommandLineProvider.GranularityOptionName] = ["session"],
            [VideoRecorderCommandLineProvider.ArgsOptionName] = ["-first"],
            [VideoRecorderCommandLineProvider.MaxDurationOptionName] = ["42"],
            [VideoRecorderCommandLineProvider.ChaptersOptionName] = ["off"],
        }));
        ServiceProvider secondServices = context.Services(new TestCommandLineOptions(new() { [VideoRecorderCommandLineProvider.EnableOptionName] = [] }));

        VideoRecorderSessionHandler first = Assert.IsInstanceOfType<VideoRecorderSessionHandler>(firstFactory.GetInstance(firstServices));
        VideoRecorderSessionHandler second = Assert.IsInstanceOfType<VideoRecorderSessionHandler>(secondFactory.GetInstance(secondServices));

        Assert.AreNotSame(first, second);
        Assert.AreSame(first, firstFactory.GetInstance(firstServices));
        Assert.AreSame(second, secondFactory.GetInstance(secondServices));
        Assert.IsTrue(await first.IsEnabledAsync());
        Assert.IsTrue(await second.IsEnabledAsync());
        Assert.HasCount(2, configured);
        Assert.AreNotSame(configured[0], configured[1]);
        Assert.AreEqual(VideoRecorderPersistenceMode.OnFailure, configured[0].PersistMode);
        Assert.AreEqual(VideoCaptureSource.Window, configured[0].Source);
        Assert.AreEqual(VideoCaptureGranularity.PerSession, configured[0].Granularity);
        Assert.AreEqual("-first", configured[0].ExtraRecorderArguments);
        Assert.AreEqual(TimeSpan.FromSeconds(42), configured[0].MaxRetainedDuration);
        Assert.IsFalse(configured[0].IncludeChapters);
        Assert.AreEqual(VideoRecorderPersistenceMode.Always, configured[1].PersistMode);
        Assert.AreEqual(VideoCaptureSource.Screen, configured[1].Source);
        Assert.AreEqual(VideoCaptureGranularity.PerTest, configured[1].Granularity);
        Assert.AreEqual("-configured", configured[1].ExtraRecorderArguments);
        Assert.AreEqual(TimeSpan.FromSeconds(11), configured[1].MaxRetainedDuration);
        Assert.IsTrue(configured[1].IncludeChapters);
        Assert.AreEqual(VideoRecorderFormat.WebMVp9, configured[1].Format);
        context.Configuration.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Direct registration without configure")]
    [DataRow(true, DisplayName = "Builder hook ignores its raw argument array")]
    public async Task Registration_WithoutConfigure_CreatesInertDisabledHandler(bool throughHook)
    {
        var context = new RegistrationContext();
        if (throughHook)
        {
            VideoRecorder.TestingPlatformBuilderHook.AddExtensions(context.Builder.Object, ["--capture-video", "always"]);
        }
        else
        {
            context.Builder.Object.AddVideoRecorderProvider();
        }

        CompositeExtensionFactory<VideoRecorderSessionHandler> consumerFactory = Assert.ContainsSingle(context.Consumers);
        CompositeExtensionFactory<VideoRecorderSessionHandler> lifetimeFactory = Assert.ContainsSingle(context.Lifetimes);
        Assert.AreSame(consumerFactory, lifetimeFactory);
        ServiceProvider services = context.Services(new TestCommandLineOptions([]));
        VideoRecorderSessionHandler handler = Assert.IsInstanceOfType<VideoRecorderSessionHandler>(((ICompositeExtensionFactory)consumerFactory).GetInstance(services));
        Mock<ITestSessionContext> session = CreateSession(CancellationToken.None);

        await handler.OnTestSessionStartingAsync(session.Object);
        await handler.ConsumeAsync(
            null!,
            VideoRecorderBehaviorContext.Update("test", "Disabled test", new FailedTestNodeStateProperty("ignored")),
            CancellationToken.None);
        await handler.OnTestSessionFinishingAsync(session.Object);

        Assert.IsFalse(await handler.IsEnabledAsync());
        Assert.AreSame(handler, ((ICompositeExtensionFactory)lifetimeFactory).GetInstance(services));
        Assert.IsInstanceOfType<VideoRecorderCommandLineProvider>(Assert.ContainsSingle(context.OptionProviders)());
        context.Configuration.VerifyNoOtherCalls();
        context.Clock.VerifyNoOtherCalls();
        context.Output.VerifyNoOtherCalls();
        context.Bus.VerifyNoOtherCalls();
        context.LoggerFactory.Verify(factory => factory.CreateLogger(typeof(VideoRecorderSessionHandler).FullName!), Times.Once());
    }

    [TestMethod]
    public async Task AddVideoRecorderProvider_ConfigureFailure_DoesNotCachePartialHandler()
    {
        var context = new RegistrationContext();
        var expected = new InvalidOperationException("configuration rejected");
        var configured = new List<VideoRecorderOptions>();
        context.Builder.Object.AddVideoRecorderProvider(options =>
        {
            configured.Add(options);
            if (configured.Count == 1)
            {
                throw expected;
            }
        });
        ICompositeExtensionFactory factory = Assert.ContainsSingle(context.Consumers);
        ServiceProvider services = context.Services(new TestCommandLineOptions([]));

        InvalidOperationException actual = Assert.ThrowsExactly<InvalidOperationException>(() => factory.GetInstance(services));

        Assert.AreSame(expected, actual.InnerException);
        Assert.Contains(nameof(VideoRecorderSessionHandler), actual.Message);
        Assert.HasCount(1, configured);
        context.LoggerFactory.Verify(loggerFactory => loggerFactory.CreateLogger(It.IsAny<string>()), Times.Never());
        context.Configuration.VerifyNoOtherCalls();
        VideoRecorderSessionHandler handler = Assert.IsInstanceOfType<VideoRecorderSessionHandler>(factory.GetInstance(services));
        Assert.AreSame(handler, factory.GetInstance(services));
        Assert.IsFalse(await handler.IsEnabledAsync());
        Assert.HasCount(2, configured);
        Assert.AreNotSame(configured[0], configured[1]);
        context.LoggerFactory.Verify(loggerFactory => loggerFactory.CreateLogger(typeof(VideoRecorderSessionHandler).FullName!), Times.Once());
        context.Output.VerifyNoOtherCalls();
        context.Bus.VerifyNoOtherCalls();
    }

    private static Mock<ITestSessionContext> CreateSession(CancellationToken token)
    {
        var session = new Mock<ITestSessionContext>(MockBehavior.Strict);
        session.SetupGet(value => value.SessionUid).Returns(new SessionUid("registered-video-session"));
        session.SetupGet(value => value.CancellationToken).Returns(token);
        return session;
    }

    private sealed class RegistrationContext
    {
        public RegistrationContext()
        {
            Host.Setup(host => host.AddDataConsumer(It.IsAny<CompositeExtensionFactory<VideoRecorderSessionHandler>>()))
                .Callback<CompositeExtensionFactory<VideoRecorderSessionHandler>>(Consumers.Add);
            Host.Setup(host => host.AddTestSessionLifetimeHandler(It.IsAny<CompositeExtensionFactory<VideoRecorderSessionHandler>>()))
                .Callback<CompositeExtensionFactory<VideoRecorderSessionHandler>>(Lifetimes.Add);
            CommandLine.Setup(commandLine => commandLine.AddProvider(It.IsAny<Func<ICommandLineOptionsProvider>>()))
                .Callback<Func<ICommandLineOptionsProvider>>(OptionProviders.Add);
            Builder.SetupGet(builder => builder.TestHost).Returns(Host.Object);
            Builder.SetupGet(builder => builder.CommandLine).Returns(CommandLine.Object);
            Configuration.SetupGet(configuration => configuration[PlatformConfigurationConstants.PlatformResultDirectory]).Returns("configured-results");
            LoggerFactory.Setup(factory => factory.CreateLogger(typeof(VideoRecorderSessionHandler).FullName!)).Returns(Logger.Object);
            Output.Setup(output => output.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
                .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>(
                    (producer, data, token) => OutputMessages.Add((producer, data, token)))
                .Returns(Task.CompletedTask);
        }

        public Mock<ITestApplicationBuilder> Builder { get; } = new(MockBehavior.Strict);

        public Mock<ITestHostManager> Host { get; } = new(MockBehavior.Strict);

        public Mock<ICommandLineManager> CommandLine { get; } = new(MockBehavior.Strict);

        public Mock<IConfiguration> Configuration { get; } = new(MockBehavior.Strict);

        public Mock<IClock> Clock { get; } = new(MockBehavior.Strict);

        public Mock<IMessageBus> Bus { get; } = new(MockBehavior.Strict);

        public Mock<IOutputDevice> Output { get; } = new(MockBehavior.Strict);

        public Mock<ILogger<VideoRecorderSessionHandler>> Logger { get; } = new(MockBehavior.Strict);

        public Mock<ILoggerFactory> LoggerFactory { get; } = new(MockBehavior.Strict);

        public List<CompositeExtensionFactory<VideoRecorderSessionHandler>> Consumers { get; } = [];

        public List<CompositeExtensionFactory<VideoRecorderSessionHandler>> Lifetimes { get; } = [];

        public List<Func<ICommandLineOptionsProvider>> OptionProviders { get; } = [];

        public List<(IOutputDeviceDataProducer Producer, IOutputDeviceData Data, CancellationToken Token)> OutputMessages { get; } = [];

        public ServiceProvider Services(ICommandLineOptions options)
        {
            var services = new ServiceProvider();
            services.AddServices([Configuration.Object, options, Bus.Object, Output.Object, Clock.Object, LoggerFactory.Object]);
            return services;
        }
    }
}
