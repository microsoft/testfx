// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Specifies the kind of test execution request.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public enum TestExecutionRequestKind
{
    /// <summary>
    /// A test discovery request.
    /// </summary>
    Discovery,

    /// <summary>
    /// A test run request.
    /// </summary>
    Run,
}
