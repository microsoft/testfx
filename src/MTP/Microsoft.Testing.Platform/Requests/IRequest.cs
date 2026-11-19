// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Requests;

/// <summary>
/// Represents a request interface.
/// </summary>
public interface IRequest
{
    /// <summary>
    /// Gets the test session context.
    /// </summary>
    TestSessionContext Session { get; }
}
