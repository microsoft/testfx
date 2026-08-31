// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Telemetry;

/// <summary>
/// Knobs that control how much detail the platform puts on OpenTelemetry spans and metrics.
/// </summary>
/// <remarks>
/// Test output and stack traces can be very large and can contain secrets, so they are opt-out-able and always
/// truncated. Everything is driven by environment variables rather than command line options so the same
/// configuration can be applied to a whole CI job without touching each invocation.
/// </remarks>
internal sealed class PlatformOpenTelemetryOptions
{
    internal const int DefaultAttributeValueLengthLimit = 8 * 1024;

    private PlatformOpenTelemetryOptions(bool captureTestOutput, int attributeValueLengthLimit, bool emitLegacyAttributes)
    {
        CaptureTestOutput = captureTestOutput;
        AttributeValueLengthLimit = attributeValueLengthLimit;
        EmitLegacyAttributes = emitLegacyAttributes;
    }

    /// <summary>
    /// Gets a value indicating whether standard output/error captured for a test is attached to its span.
    /// </summary>
    public bool CaptureTestOutput { get; }

    /// <summary>
    /// Gets the maximum number of characters kept for a single string attribute. Longer values are truncated
    /// and suffixed with an ellipsis.
    /// </summary>
    public int AttributeValueLengthLimit { get; }

    /// <summary>
    /// Gets a value indicating whether the pre-semantic-convention attribute and instrument names are emitted
    /// alongside the new ones. Keeping them on by default means existing dashboards do not break.
    /// </summary>
    public bool EmitLegacyAttributes { get; }

    public static PlatformOpenTelemetryOptions Default { get; } = new(captureTestOutput: true, DefaultAttributeValueLengthLimit, emitLegacyAttributes: true);

    public static PlatformOpenTelemetryOptions FromEnvironment(IEnvironment environment)
    {
        bool captureTestOutput = GetBoolean(environment, EnvironmentVariableConstants.TESTINGPLATFORM_OTEL_CAPTURE_TEST_OUTPUT, defaultValue: true);
        bool emitLegacyAttributes = GetBoolean(environment, EnvironmentVariableConstants.TESTINGPLATFORM_OTEL_EMIT_LEGACY_ATTRIBUTES, defaultValue: true);

        int limit = DefaultAttributeValueLengthLimit;
        if (int.TryParse(environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT), out int parsedLimit)
            && parsedLimit > 0)
        {
            limit = parsedLimit;
        }

        return new PlatformOpenTelemetryOptions(captureTestOutput, limit, emitLegacyAttributes);
    }

    /// <summary>
    /// Truncates <paramref name="value"/> to <see cref="AttributeValueLengthLimit"/>.
    /// </summary>
    public string? Truncate(string? value)
        => value is not null && value.Length > AttributeValueLengthLimit
            ? value.Substring(0, AttributeValueLengthLimit) + "…"
            : value;

    private static bool GetBoolean(IEnvironment environment, string name, bool defaultValue)
    {
        string? value = environment.GetEnvironmentVariable(name);
        return value switch
        {
            "1" or "true" or "True" or "TRUE" => true,
            "0" or "false" or "False" or "FALSE" => false,
            _ => defaultValue,
        };
    }
}
