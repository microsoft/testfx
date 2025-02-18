﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET7_0_OR_GREATER
using System.Runtime.InteropServices.JavaScript;
#endif

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Implementation of output device that writes to terminal with progress and optionally with ANSI.
/// </summary>
[SupportedOSPlatform("browser")]
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
                _console.WriteLine(PlatformResources.CancellingTestSession);
                return Task.CompletedTask;
            });

    public Type[] DataTypesConsumed { get; } =
    [
        typeof(TestNodeUpdateMessage),
        typeof(SessionFileArtifact),
        typeof(TestNodeFileArtifact),
        typeof(FileArtifact),
    ];

    /// <inheritdoc />
    public string Uid { get; } = nameof(BrowserOutputDevice);

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = "Test Platform Browser Console Service";

    /// <inheritdoc />
    public string Description { get; } = "Test Platform default browser console service";

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task DisplayBannerAsync(string? bannerMessage)
    {
        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout))
        {
            if (!_bannerDisplayed)
            {
                // skip the banner for the children processes
                _environment.SetEnvironmentVariable(TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER, "1");

                _bannerDisplayed = true;

                if (bannerMessage is not null)
                {
                    _console.WriteLine(bannerMessage);
                }
                else
                {
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

                    _console.WriteLine(stringBuilder.ToString());
                }
            }
        }
    }

    public Task DisplayBeforeSessionStartAsync()
    {
        AppendAssemblyLinkTargetFrameworkAndArchitecture(_console, _assemblyName, _targetFramework, _longArchitecture);
        return Task.CompletedTask;
    }

    private static void AppendAssemblyLinkTargetFrameworkAndArchitecture(IConsole console, string assembly, string? targetFramework, string? architecture)
    {
        var builder = new StringBuilder();
        builder.Append(assembly);
        if (targetFramework != null || architecture != null)
        {
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
        }

        console.WriteLine(builder.ToString());
    }

    public async Task DisplayAfterSessionEndRunAsync()
    {
        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout))
        {
            if (!_firstCallTo_OnSessionStartingAsync)
            {
                // TODO: Print summary
            }
        }
    }

    public Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
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
    public async Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data)
    {
        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout))
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
    [JSImport("console.warn")]
    private static partial void ConsoleWarn(string? message);

    [JSImport("console.error")]
    private static partial void ConsoleError(string? message);

    [JSImport("console.log")]
    private static partial void ConsoleLog(string? message);
#else
    private void ConsoleWarn(string? message) => _console.WriteLine(message);

    private void ConsoleError(string? message) => _console.WriteLine(message);

    private void ConsoleLog(string? message) => _console.WriteLine(message);
#endif

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
                string? standardOutput = testNodeStateChanged.TestNode.Properties.SingleOrDefault<StandardOutputProperty>()?.StandardOutput;
                string? standardError = testNodeStateChanged.TestNode.Properties.SingleOrDefault<StandardErrorProperty>()?.StandardError;

                switch (testNodeStateChanged.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>())
                {
                    case InProgressTestNodeStateProperty:
                        break;

                    case ErrorTestNodeStateProperty errorState:
                        ConsoleError($"{nameof(ErrorTestNodeStateProperty)}: {testNodeStateChanged.TestNode.DisplayName}");
                        ConsoleError(errorState.Explanation);
                        ConsoleError(errorState.Exception?.ToString());
                        break;

                    case FailedTestNodeStateProperty failedState:
                        ConsoleError($"{nameof(FailedTestNodeStateProperty)}: {testNodeStateChanged.TestNode.DisplayName}");
                        ConsoleError(failedState.Explanation);
                        ConsoleError(failedState.Exception?.ToString());
                        break;

                    case TimeoutTestNodeStateProperty timeoutState:
                        ConsoleError($"{nameof(TimeoutTestNodeStateProperty)}: {testNodeStateChanged.TestNode.DisplayName}");
                        ConsoleError(timeoutState.Explanation);
                        ConsoleError(timeoutState.Exception?.ToString());
                        break;

                    case CancelledTestNodeStateProperty cancelledState:
                        ConsoleError($"{nameof(CancelledTestNodeStateProperty)}: {testNodeStateChanged.TestNode.DisplayName}");
                        ConsoleError(cancelledState.Explanation);
                        ConsoleError(cancelledState.Exception?.ToString());
                        break;

                    case PassedTestNodeStateProperty:
                        break;

                    case SkippedTestNodeStateProperty skippedState:
                        break;
                }

                break;

            case TestNodeFileArtifact artifact:
                {
                    // TODO
                }

                break;

            case SessionFileArtifact artifact:
                {
                    // TODO
                }

                break;
            case FileArtifact artifact:
                {
                    // TODO
                }

                break;
        }

        return Task.CompletedTask;
    }

    public async Task HandleProcessRoleAsync(TestProcessRole processRole)
    {
        if (processRole == TestProcessRole.TestHost)
        {
            await _policiesService.RegisterOnMaxFailedTestsCallbackAsync(
                async (maxFailedTests, _) => await DisplayAsync(
                    this, new TextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.ReachedMaxFailedTestsMessage, maxFailedTests))));
        }
    }
}
