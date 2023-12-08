// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Threading;

namespace Microsoft.Testing.TestInfrastructure;

public class DotnetMuxer : IDisposable
{
    private static int s_maxMuxerPerProcessValue = int.MaxValue;
    private static SemaphoreSlim s_maxMuxerPerProcess = new(s_maxMuxerPerProcessValue);

    public static int MaxMuxerPerProcess
    {
        get => s_maxMuxerPerProcessValue;
        set
        {
            s_maxMuxerPerProcessValue = value;
            s_maxMuxerPerProcess = new SemaphoreSlim(s_maxMuxerPerProcessValue);
        }
    }

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

    public static string ARTIFACTS_PACKAGES_NONSHIPPING => $"{Path.Combine(RootFinder.Find(), "artifacts", "packages", Constants.BuildConfiguration, "NonShipping")}";

    public static string ARTIFACTS_PACKAGES_SHIPPING => $"{Path.Combine(RootFinder.Find(), "artifacts", "packages", Constants.BuildConfiguration, "Shipping")}";

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
            _environmentVariables["ARTIFACTS_PACKAGES_NONSHIPPING"] = ARTIFACTS_PACKAGES_NONSHIPPING;
            _environmentVariables["ARTIFACTS_PACKAGES_SHIPPING"] = ARTIFACTS_PACKAGES_SHIPPING;
        }
    }

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

    public string StandardOutput => _commandLine.StandardOutput;

    public ReadOnlyCollection<string> StandardOutputLines => _commandLine.StandardOutputLines;

    public string StandardError => _commandLine.ErrorOutput;

    public ReadOnlyCollection<string> StandardErrorLines => _commandLine.ErrorOutputLines;

    public async Task<int> Args(string arguments)
    {
        return await Args(arguments, _environmentVariables);
    }

    public async Task<int> Args(
        string arguments,
        IDictionary<string, string> environmentVariables)
    {
        await s_maxMuxerPerProcess.WaitAsync();
        try
        {
            return await _commandLine.RunAsyncAndReturnExitCode($"{_dotnet} {arguments}", environmentVariables, cleanDefaultEnvironmentVariableIfCustomAreProvided: true, 60 * 30);
        }
        finally
        {
            s_maxMuxerPerProcess.Release();
        }
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
