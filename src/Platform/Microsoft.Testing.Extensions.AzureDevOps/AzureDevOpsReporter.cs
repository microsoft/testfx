// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOps.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.AzureDevOps;

internal sealed class AzureDevOpsReporter :
    IDataConsumer,
    IDataProducer,
    IOutputDeviceDataProducer
{
    private const string DeterministicBuildRoot = "/_/";

    private readonly IOutputDevice _outputDisplay;

    private static readonly char[] NewlineCharacters = new char[] { '\r', '\n' };
    private readonly ICommandLineOptions _commandLine;
    private readonly IEnvironment _environment;
    private string _severity = "error";

    public AzureDevOpsReporter(
        ICommandLineOptions commandLine,
        IEnvironment environment,
        IOutputDevice outputDisplay)
    {
        _commandLine = commandLine;
        _environment = environment;
        _outputDisplay = outputDisplay;
    }

    public Type[] DataTypesConsumed { get; } =
    [
        typeof(TestNodeUpdateMessage)
    ];

    public Type[] DataTypesProduced { get; } = [typeof(SessionFileArtifact)];

    /// <inheritdoc />
    public string Uid { get; } = nameof(AzureDevOpsReporter);

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = AzureDevOpsResources.DisplayName;

    /// <inheritdoc />
    public string Description { get; } = AzureDevOpsResources.Description;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync()
    {
        bool isEnabled = _commandLine.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsOptionName)
            && string.Equals(_environment.GetEnvironmentVariable("TF_BUILD"), "true", StringComparison.OrdinalIgnoreCase);

        if (isEnabled)
        {
            bool found = _commandLine.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity, out string[]? arguments);
            if (found && arguments?.Length > 0)
            {
                _severity = arguments[0].ToLowerInvariant();
            }
        }

        return Task.FromResult(isEnabled);
    }

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (value is not TestNodeUpdateMessage nodeUpdateMessage)
        {
            return;
        }

        TestNodeStateProperty nodeState = nodeUpdateMessage.TestNode.Properties.Single<TestNodeStateProperty>();

        switch (nodeState)
        {
            case FailedTestNodeStateProperty failed:
                await WriteExceptionAsync(failed.Explanation, failed.Exception);
                break;
            case ErrorTestNodeStateProperty error:
                await WriteExceptionAsync(error.Explanation, error.Exception);
                break;
            case CancelledTestNodeStateProperty cancelled:
                await WriteExceptionAsync(cancelled.Explanation, cancelled.Exception);
                break;
            case TimeoutTestNodeStateProperty timeout:
                await WriteExceptionAsync(timeout.Explanation, timeout.Exception);
                break;
        }
    }

    private async Task WriteExceptionAsync(string? explanation, Exception? exception)
    {
        if (exception == null || exception.StackTrace == null)
        {
            return;
        }

        string message = explanation ?? exception.Message;

        if (message == null)
        {
            return;
        }

        string stackTrace = exception.StackTrace;
        foreach (string? stackFrame in stackTrace.Split(NewlineCharacters, StringSplitOptions.RemoveEmptyEntries))
        {
            (string Code, string File, int LineNumber)? location = GetStackFrameLocation(stackFrame);
            if (location != null)
            {
                string file = location.Value.File;

                // TODO: We need better rule for stackframes to opt out from being interesting.
                if (file.EndsWith("Assert.cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Deterministic build paths start with "/_/"
                string root = file.StartsWith(DeterministicBuildRoot, StringComparison.Ordinal) ? DeterministicBuildRoot : RootFinder.Find();

                string relativePath = file.StartsWith(root, StringComparison.CurrentCultureIgnoreCase) ? file.Substring(root.Length) : file;
                string relativeNormalizedPath = relativePath.Replace('\\', '/');

                string err = AzDoEscaper.Escape(message);

                string line = $"##vso[task.logissue type={_severity};sourcepath={relativeNormalizedPath};linenumber={location.Value.LineNumber};columnnumber=1]{err}";
                await _outputDisplay.DisplayAsync(this, new FormattedTextOutputDeviceData(line));
            }
        }
    }

    internal /* for testing */ static (string Code, string File, int LineNumber)? GetStackFrameLocation(string stackTraceLine)
    {
        Match match = StackTraceHelper.GetFrameRegex().Match(stackTraceLine);
        if (!match.Success)
        {
            return null;
        }

        bool weHaveFilePathAndCodeLine = !RoslynString.IsNullOrWhiteSpace(match.Groups["code"].Value);
        if (!weHaveFilePathAndCodeLine)
        {
            return null;
        }

        if (RoslynString.IsNullOrWhiteSpace(match.Groups["file"].Value))
        {
            return null;
        }

        int line = int.TryParse(match.Groups["line"].Value, out int value) ? value : 0;

        return (match.Groups["code"].Value, match.Groups["file"].Value, line);
    }
}
