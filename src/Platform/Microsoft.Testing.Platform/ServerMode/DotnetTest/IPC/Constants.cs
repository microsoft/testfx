// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC;

internal static class TestStates
{
    internal const byte Discovered = 0;
    internal const byte Passed = 1;
    internal const byte Skipped = 2;
    internal const byte Failed = 3;
    internal const byte Error = 4;
    internal const byte Timeout = 5;
    internal const byte Cancelled = 6;
    internal const byte InProgress = 7;
}

internal static class SessionEventTypes
{
    internal const byte TestSessionStart = 0;
    internal const byte TestSessionEnd = 1;
}

internal static class HandshakeMessagePropertyNames
{
    internal const byte PID = 0;
    internal const byte Architecture = 1;
    internal const byte Framework = 2;
    internal const byte OS = 3;
    internal const byte SupportedProtocolVersions = 4;
    internal const byte HostType = 5;
    internal const byte ModulePath = 6;
    internal const byte ExecutionId = 7;
    internal const byte InstanceId = 8;
    internal const byte IsIDE = 9;
}

internal static class ProtocolConstants
{
    // The change between 1.0.0 and 1.0.1 is that TerminalOutputDevice is no longer plugged in.
    // That's not really a protocol change, but we use the version to signify to SDK that it can avoid output redirection.
    // So, when SDK declares itself as supporting 1.0.1, and MTP is also using 1.0.1, and we negotiate to that version.
    // Then SDK can assume that MTP output doesn't interfere with SDK output, and we can safely let live output to work.
    internal const string SupportedVersions = "1.0.0;1.0.1";
}
