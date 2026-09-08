// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Describes the request for which a test execution filter provider is being evaluated.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class TestExecutionFilterContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestExecutionFilterContext"/> class.
    /// </summary>
    /// <param name="requestKind">The request kind.</param>
    /// <param name="origin">The request origin.</param>
    public TestExecutionFilterContext(TestExecutionRequestKind requestKind, TestExecutionRequestOrigin origin)
    {
        RequestKind = requestKind;
        Origin = origin;
    }

    /// <summary>
    /// Gets the request kind.
    /// </summary>
    public TestExecutionRequestKind RequestKind { get; }

    /// <summary>
    /// Gets the request origin.
    /// </summary>
    public TestExecutionRequestOrigin Origin { get; }
}
