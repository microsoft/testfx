// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Services;

/// <summary>
/// Describes the test-coverage messaging capabilities available in the current test application.
/// </summary>
public interface ITestCoverageCapabilities
{
    /// <summary>
    /// Gets a value indicating whether this platform supports the test-coverage message contract.
    /// </summary>
    bool SupportsTestCoverageMessages { get; }

    /// <summary>
    /// Gets the UIDs of enabled data producers that declare at least one test-coverage message type.
    /// </summary>
    /// <remarks>
    /// The collection is a snapshot of the producers registered when this property is read. Extensions should
    /// query it from a test-session lifecycle callback, after extension construction has completed, rather than
    /// from their factory or constructor.
    /// </remarks>
    IReadOnlyCollection<string> EnabledProducerUids { get; }
}
