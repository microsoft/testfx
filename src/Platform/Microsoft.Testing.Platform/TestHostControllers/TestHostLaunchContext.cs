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
    /// <param name="arguments">The arguments, already including the test host controller PID option.</param>
    /// <param name="environmentVariables">
    /// The final environment for the test host, including the controller-to-host IPC pipe name the
    /// host must connect back on.
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
    /// Gets the arguments, already including the test host controller PID option.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Gets the final environment for the test host, after all
    /// <see cref="ITestHostEnvironmentVariableProvider"/> ran. Includes the controller-to-host IPC
    /// pipe name the host must connect back on.
    /// </summary>
    public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; }

    /// <summary>
    /// Gets the working directory, or <see langword="null"/> to inherit the current one.
    /// </summary>
    public string? WorkingDirectory { get; }
}
