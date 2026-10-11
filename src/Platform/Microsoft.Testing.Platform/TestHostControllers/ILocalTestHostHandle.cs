// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

/// <summary>
/// Represents a test host handle backed by a local operating-system process.
/// </summary>
/// <remarks>
/// The platform uses <see cref="ProcessId"/> to verify that the process connecting to the controller
/// is the process created by the launcher before privileged lifetime extensions inspect or terminate it.
/// <para>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </para>
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ILocalTestHostHandle : ITestHostHandle
{
    /// <summary>
    /// Gets the operating-system process identifier of the launched test host.
    /// </summary>
    int ProcessId { get; }
}
