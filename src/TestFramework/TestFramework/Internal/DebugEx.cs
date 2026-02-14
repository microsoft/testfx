// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

[SuppressMessage("ApiDesign", "RS0030:Do not used banned APIs", Justification = "Replacement API to allow nullable hints for compiler")]
internal static class DebugEx
{
    /// <inheritdoc cref="Debug.Assert(bool, string)"/>
    [Conditional("DEBUG")]
    public static void Assert([DoesNotReturnIf(false)] bool b, string message)
#if NETFRAMEWORK && DEBUG
    {
        if (!b)
        {
            // In CI scenarios, we don't want Debug.Assert to show a dialog that
            // ends up causing the job to timeout. We use FailFast instead.
            // FailFast is better than throwing an exception to avoid anyone
            // catching an exception and masking an assert failure.
#pragma warning disable CA2201 // Do not raise reserved exception types
            var ex = new Exception($"Debug.Assert failed: {message}");
#pragma warning restore CA2201 // Do not raise reserved exception types
            Environment.FailFast(ex.Message, ex);
        }
    }
#else
        => Debug.Assert(b, message);
#endif
}
