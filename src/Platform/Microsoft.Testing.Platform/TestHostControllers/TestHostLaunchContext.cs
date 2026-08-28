// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

/// <summary>
/// Carries the fully prepared information the platform would have used to start the test host,
/// passed to an <see cref="ITestHostLauncher"/>.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class TestHostLaunchContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestHostLaunchContext"/> class.
    /// </summary>
    /// <param name="fileName">The test host executable path the platform would have started.</param>
    /// <param name="arguments">The fully prepared arguments for the active launch mode.</param>
    /// <param name="environmentVariables">
    /// The final environment for the test host, including any controller or orchestrator connection
    /// metadata the host must consume.
    /// </param>
    /// <param name="workingDirectory">The working directory, or <see langword="null"/> to inherit the current one.</param>
    public TestHostLaunchContext(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?> environmentVariables,
        string? workingDirectory)
    {
        FileName = fileName;
        Arguments = arguments;
        EnvironmentVariables = environmentVariables;
        WorkingDirectory = workingDirectory;
    }

    /// <summary>
    /// Gets the test host executable path the platform would have started.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Gets the fully prepared arguments for the active launch mode.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Gets the final environment for the test host, including any controller or orchestrator connection
    /// metadata required by the active launch mode.
    /// </summary>
    public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; }

    /// <summary>
    /// Gets the working directory, or <see langword="null"/> to inherit the current one.
    /// </summary>
    public string? WorkingDirectory { get; }
}
