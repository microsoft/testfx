// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Microsoft.Testing.TestInfrastructure;

public class DotnetMuxer : IDisposable
{
    private static readonly string Root = RootFinder.Find();
    private static readonly IDictionary<string, string> DefaultEnvironmentVariables
        = new Dictionary<string, string>()
            {
                { "DOTNET_ROOT", $"{Root}/.dotnet" },
                { "DOTNET_INSTALL_DIR", $"{Root}/.dotnet" },
                { "DOTNET_SKIP_FIRST_TIME_EXPERIENCE", "1" },
                { "DOTNET_MULTILEVEL_LOOKUP", "0" },
            };

    private readonly string _dotnet;
    private readonly IDictionary<string, string> _environmentVariables;
    private readonly CommandLine _commandLine;
    private bool _isDisposed;

    public DotnetMuxer()
        : this(
              DefaultEnvironmentVariables,
              new Dictionary<string, string>(),
              mergeDefaultEnvironmentVariables: true,
              useDefaultArtifactsPackages: true)
    {
    }

    public DotnetMuxer(
        IDictionary<string, string> environmentVariables,
        bool mergeEnvironmentVariables = true,
        bool useDefaultArtifactPackages = true)
        : this(
              DefaultEnvironmentVariables,
              environmentVariables,
              mergeEnvironmentVariables,
              useDefaultArtifactPackages)
    {
    }

    private DotnetMuxer(
        IDictionary<string, string> defaultEnvironmentVariables,
        IDictionary<string, string> environmentVariables,
        bool mergeDefaultEnvironmentVariables = true,
        bool useDefaultArtifactsPackages = true)
    {
        _dotnet = $"{Root}/.dotnet/dotnet{Constants.ExecutableExtension}";

        _commandLine = new CommandLine();
        _environmentVariables = mergeDefaultEnvironmentVariables
            ? MergeEnvironmentVariables(defaultEnvironmentVariables, environmentVariables)
            : environmentVariables;

        if (useDefaultArtifactsPackages)
        {
            _environmentVariables["ArtifactsPackagesNonShipping"] = Constants.ArtifactsPackagesNonShipping;
            _environmentVariables["ArtifactsPackagesShipping"] = Constants.ArtifactsPackagesShipping;
        }
    }

    public string StandardOutput => _commandLine.StandardOutput;

    public ReadOnlyCollection<string> StandardOutputLines => _commandLine.StandardOutputLines;

    public string StandardError => _commandLine.ErrorOutput;

    public ReadOnlyCollection<string> StandardErrorLines => _commandLine.ErrorOutputLines;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            _commandLine.Dispose();
        }

        _isDisposed = true;
    }

    public async Task<int> Args(string arguments, string? workingDirectory = null, int timeoutInSeconds = 60)
        => await Args(arguments, workingDirectory, _environmentVariables, timeoutInSeconds);

    public async Task<int> Args(
        string arguments,
        string? workingDirectory,
        IDictionary<string, string> environmentVariables,
        int timeoutInSeconds = 60)
    {
        return await _commandLine.RunAsyncAndReturnExitCode(
            $"{_dotnet} {arguments}",
            environmentVariables,
            workingDirectory: workingDirectory,
            cleanDefaultEnvironmentVariableIfCustomAreProvided: true,
            timeoutInSeconds: timeoutInSeconds);
    }

    private IDictionary<string, string> MergeEnvironmentVariables(
        IDictionary<string, string> environmentVariables1,
        IDictionary<string, string> environmentVariables2)
    {
        if (environmentVariables1.Count == 0)
        {
            return new Dictionary<string, string>(environmentVariables2);
        }

        if (environmentVariables2.Count == 0)
        {
            return new Dictionary<string, string>(environmentVariables1);
        }

        IDictionary<string, string> mergedEnvironmentVariables = new Dictionary<string, string>(environmentVariables1);
        foreach (KeyValuePair<string, string> kvp in environmentVariables2)
        {
            mergedEnvironmentVariables[kvp.Key] = kvp.Value;
        }

        return mergedEnvironmentVariables;
    }
}
