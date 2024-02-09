// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Builder;

/// <summary>
/// Represents an interface for a test application.
/// </summary>
public interface ITestApplication : IDisposable
{
    /// <summary>
    /// Runs the test application asynchronously.
    /// </summary>
    /// <returns>The exit code of the test application.</returns>
    Task<int> RunAsync();
}
