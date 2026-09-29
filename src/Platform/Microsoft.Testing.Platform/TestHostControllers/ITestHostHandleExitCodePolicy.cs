// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

/// <summary>
/// Describes whether the operating-system exit code exposed by an <see cref="ITestHostHandle"/> is
/// authoritative for the test run.
/// </summary>
/// <remarks>
/// <para>
/// Some activation mechanisms report a process exit code that represents the application lifecycle
/// rather than the MTP run result. Such launchers can return <see langword="false"/> so the controller
/// reconciles the run from the test-host completion protocol instead.
/// </para>
/// <para>This API is experimental. It may change, break, or be removed at any time without notice.</para>
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestHostHandleExitCodePolicy
{
    /// <summary>
    /// Gets a value indicating whether <see cref="ITestHostHandle.ExitCode"/> is authoritative.
    /// </summary>
    bool IsExitCodeAuthoritative { get; }
}
