// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace Analyzer.Utilities.Extensions;

#pragma warning disable CA1027 // Mark enums with FlagsAttribute
internal enum SymbolVisibility
#pragma warning restore CA1027 // Mark enums with FlagsAttribute
{
    Public = 0,
    Internal = 1,
    Private = 2,
    Friend = Internal,
}
