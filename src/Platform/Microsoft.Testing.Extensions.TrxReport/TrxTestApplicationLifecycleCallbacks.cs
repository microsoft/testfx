// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Extensions.TestReports.Resources;
using Microsoft.Testing.Extensions.TrxReport.Abstractions.Serializers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class TrxTestApplicationLifecycleCallbacks : ITestApplicationLifecycleCallbacks, IDisposable
{
    private readonly bool _isEnabled;
    private readonly IEnvironment _environment;

    public TrxTestApplicationLifecycleCallbacks(
        ICommandLineOptions commandLineOptionsService,
        IEnvironment environment)
    {
        _isEnabled =
           // TrxReportGenerator is enabled only when trx report is enabled
           commandLineOptionsService.IsOptionSet(TrxReportGeneratorCommandLine.TrxReportOptionName) &&
           // TestController is not used when we run in server mode
           !commandLineOptionsService.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey) &&
           // If crash dump is not enabled we run trx in-process only
           commandLineOptionsService.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName);

        _environment = environment;
    }

    public NamedPipeClient? NamedPipeClient { get; private set; }

    public string Uid { get; } = nameof(TrxTestApplicationLifecycleCallbacks);

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = ExtensionResources.TrxReportGeneratorDisplayName;

    /// <inheritdoc />
    public string Description { get; } = ExtensionResources.TrxReportGeneratorDescription;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);

    public Task AfterRunAsync(int exitCode, CancellationToken cancellation) => Task.CompletedTask;

    public async Task BeforeRunAsync(CancellationToken cancellationToken)
    {
        if (!_isEnabled || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            if (_isEnabled)
            {
                string namedPipeName = _environment.GetEnvironmentVariable(TrxEnvironmentVariableProvider.TRXNAMEDPIPENAME)
                    ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TrxReportGeneratorMissingTrxNamedPipeEnvironmentVariable, TrxEnvironmentVariableProvider.TRXNAMEDPIPENAME));
                NamedPipeClient = new NamedPipeClient(namedPipeName);
                NamedPipeClient.RegisterSerializer(new ReportFileNameRequestSerializer(), typeof(ReportFileNameRequest));
                NamedPipeClient.RegisterSerializer(new TestAdapterInformationRequestSerializer(), typeof(TestAdapterInformationRequest));
                NamedPipeClient.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));

                // Connect to the named pipe server
                await NamedPipeClient.ConnectAsync(cancellationToken).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, cancellationToken);
            }
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            // Do nothing, we're stopping
        }
    }

    public void Dispose() => NamedPipeClient?.Dispose();
}
