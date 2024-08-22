﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC;

internal static class Constants
{
    internal const string DotnetTestCliProtocol = "dotnettestcli";
}

internal static class TestStates
{
    internal const byte Passed = 0;
    internal const byte Skipped = 1;
    internal const byte Failed = 2;
    internal const byte Error = 3;
    internal const byte Timeout = 4;
    internal const byte Cancelled = 5;
}

internal static class SessionEventTypes
{
    internal const byte TestSessionStart = 0;
    internal const byte TestSessionEnd = 1;
}

internal static class HandshakeInfoPropertyNames
{
    internal const byte PID = 0;
    internal const byte Architecture = 1;
    internal const byte Framework = 2;
    internal const byte OS = 3;
    internal const byte ProtocolVersion = 4;
    internal const byte HostType = 5;
    internal const byte ModulePath = 6;
    internal const byte ExecutionId = 7;
}

internal static class ProtocolConstants
{
    internal const string Version = "1.0.0";
}
