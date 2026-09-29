// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Builder;

/// <summary>
/// Represents the options for a test application.
/// </summary>
public sealed class TestApplicationOptions
{
    internal ITestApplicationHostLifetimeBridge? HostLifetimeBridge { get; set; }

    /// <summary>
    /// Gets or sets a token that requests cooperative cancellation of the test application.
    /// </summary>
    /// <remarks>
    /// Cancellation is observed while the test application is running and uses the same application
    /// cancellation path as timeouts, Ctrl+C, controller cancellation, and test framework stop policies.
    /// A canceled test run returns the test-session-aborted exit code after cleanup completes.
    /// </remarks>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether telemetry is enabled.
    /// </summary>
    public bool EnableTelemetry { get; set; } = true;

    /// <summary>
    /// Gets the configuration options for the test application.
    /// </summary>
    public ConfigurationOptions Configuration { get; } = new();
}
