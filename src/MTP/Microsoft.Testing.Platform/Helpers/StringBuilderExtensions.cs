// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETCOREAPP
namespace System.Text;

internal static class StringBuilderExtensions
{
    public static StringBuilder Append(this StringBuilder builder, IFormatProvider? _, string value)
        => builder.Append(value);

    public static StringBuilder AppendLine(this StringBuilder builder, IFormatProvider? _, string value)
        => builder.AppendLine(value);
}
#endif
