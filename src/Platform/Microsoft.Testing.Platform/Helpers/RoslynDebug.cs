// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Copied from https://github.com/dotnet/sdk/blob/main/src/Microsoft.CodeAnalysis.NetAnalyzers/src/Utilities/Compiler/Debug.cs
// (previously sourced from dotnet/roslyn-analyzers before that repo was archived).
namespace Microsoft.Testing.Platform;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "This is the replacement type for Debug")]
[ExcludeFromCodeCoverage]
internal static class RoslynDebug
{
    /// <inheritdoc cref="Debug.Assert(bool)"/>
    [Conditional("DEBUG")]
    public static void Assert([DoesNotReturnIf(false)] bool b)
#pragma warning disable SA1405 // Debug.Assert should provide message text
        => Debug.Assert(b);
#pragma warning restore SA1405 // Debug.Assert should provide message text

    /// <inheritdoc cref="Debug.Assert(bool, string)"/>
    [Conditional("DEBUG")]
    public static void Assert([DoesNotReturnIf(false)] bool b, string message)
        => Debug.Assert(b, message);
}
