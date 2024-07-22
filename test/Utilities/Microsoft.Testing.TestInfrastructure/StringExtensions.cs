// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

public static class StringExtensions
{
    // Double checking that is is not null on purpose.
    public static string OrDefault(this string? value, string defaultValue) => string.IsNullOrEmpty(defaultValue)
                ? throw new ArgumentNullException(nameof(defaultValue))
                : !string.IsNullOrWhiteSpace(value)
                    ? value
                    : defaultValue;
}
