// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

// Copied from https://github.com/dotnet/roslyn-analyzers/blob/main/src/Utilities/Compiler/Extensions/SymbolVisibility.cs
namespace Analyzers.Utilities;

#pragma warning disable CA1027 // Mark enums with FlagsAttribute
internal enum SymbolVisibility
#pragma warning restore CA1027 // Mark enums with FlagsAttribute
{
    Public = 0,
    Internal = 1,
    Private = 2,
    Friend = Internal,
}
