// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

/// <summary>
/// Wraps each test assembly's output in an Azure DevOps log group (<c>##[group]</c> /
/// <c>##[endgroup]</c>) so the raw pipeline log view collapses the assembly's test output by
/// default. Unlike <c>##vso[task.logdetail]</c> records, the <c>##[group]</c> format commands are
/// rendered by the modern Azure DevOps Pipelines log viewer.
/// </summary>
internal sealed class AzureDevOpsLogGroupReporter : ITestSessionLifetimeHandler, IOutputDeviceDataProducer
{
    private const string TfBuildVariableName = "TF_BUILD";

    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IEnvironment _environment;
    private readonly IOutputDevice _outputDevice;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly ILogger _logger;
    private readonly Lazy<string> _targetFrameworkMoniker;

    private bool _groupOpened;

    public AzureDevOpsLogGroupReporter(
        ICommandLineOptions commandLineOptions,
        IEnvironment environment,
        IOutputDevice outputDevice,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        ILoggerFactory loggerFactory)
    {
        _commandLineOptions = commandLineOptions;
        _environment = environment;
        _outputDevice = outputDevice;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _logger = loggerFactory.CreateLogger<AzureDevOpsLogGroupReporter>();
        _targetFrameworkMoniker = new(TargetFrameworkMonikerHelper.GetTargetFrameworkMoniker);
    }

    public string Uid => nameof(AzureDevOpsLogGroupReporter);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => AzureDevOpsResources.DisplayName;

    public string Description => AzureDevOpsResources.Description;

    public Task<bool> IsEnabledAsync()
    {
        bool isEnabledByParameter = _commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsOptionName);
        if (!isEnabledByParameter)
        {
            return Task.FromResult(false);
        }

        bool isEnabledByEnvVariable = string.Equals(_environment.GetEnvironmentVariable(TfBuildVariableName), "true", StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(isEnabledByEnvVariable);
    }

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        try
        {
            testSessionContext.CancellationToken.ThrowIfCancellationRequested();

            string name = $"{_testApplicationModuleInfo.TryGetAssemblyName() ?? "unknown"} ({_targetFrameworkMoniker.Value})";
            string line = $"##[group]{AzDoEscaper.Escape(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.LogGroupHeader, name))}";
            await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData(line), testSessionContext.CancellationToken).ConfigureAwait(false);
            _groupOpened = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogUnexpectedException(nameof(OnTestSessionStartingAsync), ex);
        }
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        try
        {
            testSessionContext.CancellationToken.ThrowIfCancellationRequested();

            if (!_groupOpened)
            {
                return;
            }

            await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData("##[endgroup]"), testSessionContext.CancellationToken).ConfigureAwait(false);
            _groupOpened = false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogUnexpectedException(nameof(OnTestSessionFinishingAsync), ex);
        }
    }

    private void LogUnexpectedException(string callbackName, Exception ex)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning($"Unexpected exception in {callbackName}: {ex}");
        }
    }
}
