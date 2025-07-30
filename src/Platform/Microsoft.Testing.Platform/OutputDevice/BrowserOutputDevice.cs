// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET7_0_OR_GREATER
using System.Runtime.InteropServices.JavaScript;
#endif

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice.Terminal;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Implementation of output device that writes to terminal with progress and optionally with ANSI.
/// </summary>
[SupportedOSPlatform("browser")]
[SupportedOSPlatform("wasi")]
internal sealed partial class BrowserOutputDevice : IPlatformOutputDevice,
    IDataConsumer,
    IOutputDeviceDataProducer,
    ITestSessionLifetimeHandler,
    IAsyncInitializableExtension
{
#pragma warning disable SA1310 // Field names should not contain underscore
    private const string TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER = nameof(TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER);
#pragma warning restore SA1310 // Field names should not contain underscore

    private readonly IConsole _console;
    private readonly IAsyncMonitor _asyncMonitor;
    private readonly IRuntimeFeature _runtimeFeature;
    private readonly IEnvironment _environment;
    private readonly IPlatformInformation _platformInformation;
    private readonly IStopPoliciesService _policiesService;
    private readonly string? _longArchitecture;

    // The effective runtime that is executing the application e.g. .NET 9, when .NET 8 application is running with --roll-forward latest.
    private readonly string? _runtimeFramework;

    // The targeted framework, .NET 8 when application specifies <TargetFramework>net8.0</TargetFramework>
    private readonly string? _targetFramework;
    private readonly string _assemblyName;

    private bool _firstCallTo_OnSessionStartingAsync = true;
    private bool _bannerDisplayed;

    private int _passedTests;
    private int _failedTests;
    private int _skippedTests;

    public BrowserOutputDevice(
        IConsole console,
        ITestApplicationModuleInfo testApplicationModuleInfo, IAsyncMonitor asyncMonitor,
        IRuntimeFeature runtimeFeature, IEnvironment environment, IPlatformInformation platformInformation,
        IStopPoliciesService policiesService)
    {
        _console = console;
        _asyncMonitor = asyncMonitor;
        _runtimeFeature = runtimeFeature;
        _environment = environment;
        _platformInformation = platformInformation;
        _policiesService = policiesService;

        if (_runtimeFeature.IsDynamicCodeSupported)
        {
#if !NETCOREAPP
            _longArchitecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
#else
            // RID has the operating system, we want to see that in the banner, but not next to every dll.
            _longArchitecture = RuntimeInformation.RuntimeIdentifier;
#endif
            _runtimeFramework = TargetFrameworkParser.GetShortTargetFramework(RuntimeInformation.FrameworkDescription);
            _targetFramework = TargetFrameworkParser.GetShortTargetFramework(Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkDisplayName) ?? _runtimeFramework;
        }

        _assemblyName = testApplicationModuleInfo.GetDisplayName();

        if (environment.GetEnvironmentVariable(TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER) is not null)
        {
            _bannerDisplayed = true;
        }
    }

    public async Task InitializeAsync()
        => await _policiesService.RegisterOnAbortCallbackAsync(
            () =>
            {
                ConsoleLog(PlatformResources.CancellingTestSession);
                return Task.CompletedTask;
            }).ConfigureAwait(false);

    public Type[] DataTypesConsumed { get; } =
    [
        typeof(TestNodeUpdateMessage),
        typeof(SessionFileArtifact),
        typeof(FileArtifact),
    ];

    /// <inheritdoc />
    public string Uid => nameof(BrowserOutputDevice);

    /// <inheritdoc />
    public string Version => AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName => "Test Platform Browser Console Service";

    /// <inheritdoc />
    public string Description => "Test Platform default browser console service";

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task DisplayBannerAsync(string? bannerMessage, CancellationToken cancellationToken)
    {
        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false))
        {
            if (_bannerDisplayed)
            {
                return;
            }

            // skip the banner for the children processes
            _environment.SetEnvironmentVariable(TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER, "1");

            _bannerDisplayed = true;

            if (bannerMessage is not null)
            {
                ConsoleLog(bannerMessage);
                return;
            }

            StringBuilder stringBuilder = new();
            stringBuilder.Append(_platformInformation.Name);

            if (_platformInformation.Version is { } version)
            {
                stringBuilder.Append(CultureInfo.InvariantCulture, $" v{version}");
                if (_platformInformation.CommitHash is { } commitHash)
                {
                    stringBuilder.Append(CultureInfo.InvariantCulture, $"+{commitHash[..10]}");
                }
            }

            if (_platformInformation.BuildDate is { } buildDate)
            {
                stringBuilder.Append(CultureInfo.InvariantCulture, $" (UTC {buildDate.UtcDateTime.ToShortDateString()})");
            }

            if (_runtimeFeature.IsDynamicCodeSupported)
            {
                stringBuilder.Append(" [");
                stringBuilder.Append(_longArchitecture);
                stringBuilder.Append(" - ");
                stringBuilder.Append(_runtimeFramework);
                stringBuilder.Append(']');
            }

            ConsoleLog(stringBuilder.ToString());
        }
    }

    public Task DisplayBeforeSessionStartAsync(CancellationToken cancellationToken)
    {
        AppendAssemblyLinkTargetFrameworkAndArchitecture(_console, _assemblyName, _targetFramework, _longArchitecture);
        return Task.CompletedTask;
    }

    private static void AppendAssemblyLinkTargetFrameworkAndArchitecture(IConsole console, string assembly, string? targetFramework, string? architecture)
    {
        var builder = new StringBuilder();
        builder.Append(assembly);
        if (targetFramework == null && architecture == null)
        {
            console.WriteLine(builder.ToString());
            return;
        }

        builder.Append(" (");
        if (targetFramework != null)
        {
            builder.Append(targetFramework);
            builder.Append('|');
        }

        if (architecture != null)
        {
            builder.Append(architecture);
        }

        builder.Append(')');

        console.WriteLine(builder.ToString());
    }

    public async Task DisplayAfterSessionEndRunAsync(CancellationToken cancellationToken)
    {
        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false))
        {
            if (_firstCallTo_OnSessionStartingAsync)
            {
                return;
            }

            int total = _skippedTests + _passedTests + _failedTests;
            // TODO: Duplicate the logic from TerminalTestReporter.AppendTestRunSummary, or refactor it
            // so that it's easily shareable between the two implementations.
            string text = $"""
                    Total tests: {total}
                    Failed tests: {_failedTests}
                    Passed tests: {_passedTests}
                    Skipped tests: {_skippedTests}
                    """;

            if (_failedTests > 0)
            {
                ConsoleError(text);
            }
            else
            {
                ConsoleLog(text);
            }
        }
    }

    public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext) => Task.CompletedTask;

    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        if (testSessionContext.CancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        // We implement IDataConsumerService and IOutputDisplayService.
        // So the engine is calling us before as IDataConsumerService and after as IOutputDisplayService.
        // The engine look for the ITestSessionLifetimeHandler in both case and call it.
        if (_firstCallTo_OnSessionStartingAsync)
        {
            _firstCallTo_OnSessionStartingAsync = false;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Displays provided data through IConsole, which is typically System.Console.
    /// </summary>
    /// <param name="producer">The producer that sent the data.</param>
    /// <param name="data">The data to be displayed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
    {
        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false))
        {
            switch (data)
            {
                case FormattedTextOutputDeviceData formattedTextData:
                    ConsoleLog(formattedTextData.Text);
                    break;

                case TextOutputDeviceData textData:
                    ConsoleLog(textData.Text);
                    break;

                case WarningMessageOutputDeviceData warningData:
                    ConsoleWarn(warningData.Message);
                    break;

                case ErrorMessageOutputDeviceData errorData:
                    ConsoleError(errorData.Message);
                    break;

                case ExceptionOutputDeviceData exceptionOutputDeviceData:
                    ConsoleError(exceptionOutputDeviceData.Exception.ToString());
                    break;
            }
        }
    }

#if NET7_0_OR_GREATER
    [JSImport("globalThis.console.warn", "main.js")]
    private static partial void ConsoleWarn(string? message);

    [JSImport("globalThis.console.error")]
    private static partial void ConsoleError(string? message);

    [JSImport("globalThis.console.log")]
    private static partial void ConsoleLog(string? message);
#else
    private void ConsoleWarn(string? message) => _console.WriteLine(message);

    private void ConsoleError(string? message) => _console.WriteLine(message);

    private void ConsoleLog(string? message) => _console.WriteLine(message);
#endif

    private void OnFailedTest(TestNodeUpdateMessage testNodeStateChanged, TestNodeStateProperty state, Exception? exception, TimeSpan duration)
    {
        _failedTests++;
        var builder = new StringBuilder();
        builder.Append("failed ");
        builder.Append(testNodeStateChanged.TestNode.DisplayName);
        builder.Append(' ');
        HumanReadableDurationFormatter.Append(builder, static (builder, s) => builder!.Append(s), duration);
        if (state.Explanation is not null)
        {
            builder.AppendLine();
            builder.Append("  ");
            builder.Append(state.Explanation);
        }

        if (exception is not null)
        {
            builder.AppendLine();
            builder.Append("  ");
            builder.Append(exception.ToString());
        }

        ConsoleError(builder.ToString());
    }

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        switch (value)
        {
            case TestNodeUpdateMessage testNodeStateChanged:

                TimeSpan duration = testNodeStateChanged.TestNode.Properties.SingleOrDefault<TimingProperty>()?.GlobalTiming.Duration ?? TimeSpan.Zero;

                switch (testNodeStateChanged.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>())
                {
                    case InProgressTestNodeStateProperty:
                        break;

                    case ErrorTestNodeStateProperty errorState:
                        OnFailedTest(testNodeStateChanged, errorState, errorState.Exception, duration);
                        break;

                    case FailedTestNodeStateProperty failedState:
                        OnFailedTest(testNodeStateChanged, failedState, failedState.Exception, duration);
                        break;

                    case TimeoutTestNodeStateProperty timeoutState:
                        OnFailedTest(testNodeStateChanged, timeoutState, timeoutState.Exception, duration);
                        break;

                    case CancelledTestNodeStateProperty cancelledState:
                        OnFailedTest(testNodeStateChanged, cancelledState, cancelledState.Exception, duration);
                        break;

                    case PassedTestNodeStateProperty:
                        _passedTests++;
                        break;

                    case SkippedTestNodeStateProperty:
                        _skippedTests++;
                        break;
                }

                // TODO:
                // foreach (FileArtifactProperty testFileArtifact in testNodeStateChanged.TestNode.Properties.OfType<FileArtifactProperty>())
                // {
                // }
                break;

            case SessionFileArtifact:
                {
                    // TODO
                }

                break;
            case FileArtifact:
                {
                    // TODO
                }

                break;
        }

        return Task.CompletedTask;
    }

    public async Task HandleProcessRoleAsync(TestProcessRole processRole, CancellationToken cancellationToken)
    {
        if (processRole == TestProcessRole.TestHost)
        {
            await _policiesService.RegisterOnMaxFailedTestsCallbackAsync(
                async (maxFailedTests, _) => await DisplayAsync(
                    this, new TextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.ReachedMaxFailedTestsMessage, maxFailedTests)), cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        }
    }
}
