// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UI;

internal class ConsoleLoggerOptions
{
    /// <summary>
    /// Gets path to which all other paths in output should be relative.
    /// </summary>
    public string? BaseDirectory { get; init; }

    /// <summary>
    /// Gets a value indicating whether we should show passed tests.
    /// </summary>
    public bool ShowPassedTests { get; init; }

    /// <summary>
    /// Gets a value indicating whether we should show information about which assembly is the source of the data on screen. Turn this off when running directly from an exe to reduce noise, because the path will always be the same.
    /// </summary>
    public bool ShowAssembly { get; init; }

    /// <summary>
    /// Gets a value indicating whether we should show information about which assembly started or completed. Turn this off when running directly from an exe to reduce noise, because the path will always be the same.
    /// </summary>
    public bool ShowAssemblyStartAndComplete { get; init; }

    /// <summary>
    /// Gets minimum amount of tests to run.
    /// </summary>
    public int MinimumExpectedTests { get; init; }

    public bool ShowProgress { get; init; }

    public bool UseAnsi { get; init; }
}
