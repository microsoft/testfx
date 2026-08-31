// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Well-known resource keys for common process-global state, for use with
/// <see cref="ResourceLockAttribute"/>. These are plain string constants: they use the exact same
/// equality-based conflict mechanism as any user-invented key, and are provided only so that tests
/// contending on the same ambient state agree on a spelling.
/// </summary>
public static class WellKnownResources
{
    /// <summary>
    /// A key representing the process-wide current working directory
    /// (<see cref="System.IO.Directory.GetCurrentDirectory"/> / <see cref="System.Environment.CurrentDirectory"/>).
    /// </summary>
    public const string CurrentDirectory = "System.Environment.CurrentDirectory";

    /// <summary>
    /// A key representing process-wide environment variables
    /// (<see cref="System.Environment.GetEnvironmentVariable(string)"/> /
    /// <see cref="System.Environment.SetEnvironmentVariable(string, string)"/>). Use this coarse key
    /// for variables whose blast radius you cannot bound, such as <c>PATH</c>, which is inherited by
    /// child processes.
    /// </summary>
    public const string EnvironmentVariables = "System.Environment.Variables";

    /// <summary>
    /// A key representing the process-wide console (<see cref="System.Console"/> streams, encoding,
    /// colors and cursor).
    /// </summary>
    public const string Console = "System.Console";
}
