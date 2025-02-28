﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework.SourceGeneration.Helpers;

internal static class FileHeaderUtils
{
    public static void AppendAutoGeneratedHeader(this IndentedStringBuilder stringBuilder)
    {
        stringBuilder.AppendLine("//------------------------------------------------------------------------------");
        stringBuilder.AppendLine("// <auto-generated>");
        stringBuilder.AppendLine("//     This code was generated by Microsoft Testing Framework Generator.");
        stringBuilder.AppendLine("// </auto-generated>");
        stringBuilder.AppendLine("//------------------------------------------------------------------------------");
    }
}
