// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal static class IdentityKeyBuilder
{
    internal static void AppendLengthPrefixedComponent(StringBuilder builder, string? component)
    {
        builder.Append((component?.Length ?? -1).ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(component);
    }
}
