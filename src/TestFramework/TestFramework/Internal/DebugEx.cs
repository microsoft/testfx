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
            // ends up causing the job to timeout. Instead, we want to throw an Exception
            throw new Exception($"Debug.Assert failed: {message}");
        }
    }
#else
        => Debug.Assert(b, message);
#endif
}
