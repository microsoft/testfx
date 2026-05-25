// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

// Copied from https://github.com/dotnet/sdk/blob/main/src/Microsoft.CodeAnalysis.NetAnalyzers/src/Utilities/Compiler/Extensions/SymbolVisibility.cs
// (previously sourced from dotnet/roslyn-analyzers before that repo was archived).
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
