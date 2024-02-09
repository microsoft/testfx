// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Builder;

/// <summary>
/// Represents the options for a test application.
/// </summary>
public sealed class TestApplicationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether telemetry is enabled.
    /// </summary>
    public bool EnableTelemetry { get; set; } = true;

    /// <summary>
    /// Gets the configuration options for the test application.
    /// </summary>
    public ConfigurationOptions Configuration { get; } = new();
}
