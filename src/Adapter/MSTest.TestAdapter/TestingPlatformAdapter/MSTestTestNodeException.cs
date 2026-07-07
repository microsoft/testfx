// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A lightweight exception used to carry a test's failure message and stack trace into a
/// Microsoft.Testing.Platform failed/error test node state, without depending on the VSTest object model.
/// It mirrors the shape of the VSTest bridge's exception: the provided stack trace string is returned verbatim
/// from <see cref="StackTrace"/> instead of being captured from the current call site.
/// </summary>
[StackTraceHidden]
internal sealed class MSTestTestNodeException : Exception
{
    public MSTestTestNodeException(string? message, string? stackTrace)
        : base(message)
        => StackTrace = stackTrace;

    public override string? StackTrace { get; }
}
#endif
