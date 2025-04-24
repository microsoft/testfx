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

        string repoRoot = RootFinder.Find();

        string stackTrace = exception.StackTrace;
        foreach (string? stackFrame in stackTrace.Split(NewlineCharacters, StringSplitOptions.RemoveEmptyEntries))
        {
            (string Code, string File, int LineNumber)? location = GetStackFrameLocation(stackFrame);
            if (location == null)
            {
                continue;
            }

            string file = location.Value.File;

            // TODO: We need better rule for stackframes to opt out from being interesting.
            if (file.EndsWith("Assert.cs", StringComparison.Ordinal))
            {
                continue;
            }

            // Deterministic build paths start with "/_/"
            string? relativePath = null;
            if (file.StartsWith(DeterministicBuildRoot, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = file.Substring(DeterministicBuildRoot.Length);
            }
            else if (file.StartsWith(repoRoot, StringComparison.CurrentCultureIgnoreCase))
            {
                relativePath = file.Substring(repoRoot.Length);
            }
            else
            {
                // Path does not belong to current repo, keep it null.
            }

            if (relativePath == null || !File.Exists(Path.Combine(repoRoot, relativePath)))
            {
                // Path does not belong to current repository or does not exist, no need to report it because it will not show up in the PR error, we will only see it details of the run, which is the same
                // as not reporting it this way. Maybe there can be 2 modes, but right now we want this to be usable for GitHub + AzDo, not for pure AzDo.
                //
                // In case of deterministic build, all the paths will be relative, so if library carries symbols and matches our path we would see the error as coming from our file
                // even though it would not. That change is slim and something we have to live with.
                //
                // Deterministic build will also have paths normalized to /, luckily File.Exist does not care about the slash direction (on Windows).
                continue;
            }

            // The slashes must be / for GitHub to render the error placement correctly.
            string relativeNormalizedPath = relativePath.Replace('\\', '/');

            string err = AzDoEscaper.Escape(message);

            string line = $"##vso[task.logissue type={_severity};sourcepath={relativeNormalizedPath};linenumber={location.Value.LineNumber};columnnumber=1]{err}";
            await _outputDisplay.DisplayAsync(this, new FormattedTextOutputDeviceData(line));
        }
    }

    internal /* for testing */ static (string Code, string File, int LineNumber)? GetStackFrameLocation(string stackTraceLine)
    {
        Match match = StackTraceHelper.GetFrameRegex().Match(stackTraceLine);
        if (!match.Success)
        {
            return null;
        }

        string code = match.Groups["code"].Value;
        bool weHaveFilePathAndCodeLine = !RoslynString.IsNullOrWhiteSpace(code);
        if (!weHaveFilePathAndCodeLine)
        {
            return null;
        }

        string file = match.Groups["file"].Value;
        if (RoslynString.IsNullOrWhiteSpace(file) || !File.Exists(file))
        {
            return null;
        }

        int line = int.TryParse(match.Groups["line"].Value, out int value) ? value : 0;

        return (code, file, line);
    }
}
