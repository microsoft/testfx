// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.IPC;

[Embedded]
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

[Embedded]
internal static class SessionEventTypes
{
    internal const byte TestSessionStart = 0;
    internal const byte TestSessionEnd = 1;
}

[Embedded]
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

    // Reports which command-line execution mode the test host is running in,
    // so consumers (e.g. dotnet test in the SDK) can detect mismatches such
    // as a help/list-tests option leaking from RunArguments into a normal run.
    // Values come from HandshakeMessageExecutionModes.
    internal const byte ExecutionMode = 10;

    // Identifies the orchestration feature responsible for the orchestrator host
    // (e.g. "retry"). Only sent by the test host orchestrator so consumers (e.g.
    // dotnet test in the SDK) can understand why an orchestrator is participating
    // in the run. The value is the orchestrator extension Uid.
    internal const byte OrchestratorFeature = 11;
}

[Embedded]
internal static class HandshakeMessageHostTypes
{
    // A regular (console or server) test host that actually runs tests.
    internal const string TestHost = "TestHost";

    // A test host controller process that restarts/monitors the test host.
    internal const string TestHostController = "TestHostController";

    // A test host running in server (JSON-RPC) mode.
    internal const string ServerTestHost = "ServerTestHost";

    // A test host orchestrator process (e.g. the retry orchestrator) that drives
    // one or more test host executions.
    internal const string TestHostOrchestrator = "TestHostOrchestrator";
}

[Embedded]
internal static class HandshakeMessageExecutionModes
{
    // Standard test run.
    internal const string Run = "run";

    // The host is going to print command-line help (e.g. --help, -?).
    internal const string Help = "help";

    // The host is going to discover tests (e.g. --list-tests).
    internal const string Discover = "discover";
}

[Embedded]
internal static class ProtocolConstants
{
    internal const string Version = "1.0.0";
}
