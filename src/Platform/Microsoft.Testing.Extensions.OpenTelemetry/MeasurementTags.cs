// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.OpenTelemetry;

/// <summary>
/// Converts the platform's dependency-free tag representation into the array shape accepted by
/// <c>System.Diagnostics.Metrics</c> instruments.
/// </summary>
internal static class MeasurementTags
{
    private static readonly KeyValuePair<string, object?>[] Empty = [];

    public static KeyValuePair<string, object?>[] ToArray(IEnumerable<KeyValuePair<string, object?>>? tags)
        => tags switch
        {
            null => Empty,
            KeyValuePair<string, object?>[] array => array,
            ICollection<KeyValuePair<string, object?>> { Count: 0 } => Empty,
            _ => CopyToArray(tags),
        };

    private static KeyValuePair<string, object?>[] CopyToArray(IEnumerable<KeyValuePair<string, object?>> tags)
    {
        List<KeyValuePair<string, object?>> buffer = [];
        foreach (KeyValuePair<string, object?> tag in tags)
        {
            buffer.Add(tag);
        }

        return buffer.ToArray();
    }
}
