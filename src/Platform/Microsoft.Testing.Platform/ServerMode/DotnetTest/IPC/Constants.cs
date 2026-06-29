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
    // The change between 1.0.0 and 1.1.0 is that TerminalOutputDevice is no longer plugged in.
    // That's not really a protocol change, but we use the version to signal to the SDK that it
    // can safely keep the test host's standard output/error visible (TerminalTestReporter and
    // host output will no longer collide).
    // When both sides advertise 1.1.0 and we negotiate to that version, the SDK can keep its
    // live output enabled.
    //
    // 1.2.0 adds the AzureDevOpsLogMessage: under the pipe protocol the host installs a no-op output
    // device (see below), so any Azure DevOps logging commands (##[group], ##vso[...]) produced by the
    // AzureDevOpsReport extension would otherwise be swallowed. When both sides negotiate 1.2.0 the host
    // forwards those marked lines to the SDK over the pipe, and the SDK writes them verbatim to its
    // TerminalTestReporter so they reach the pipeline log. An older SDK that only negotiates 1.1.0 never
    // receives the message (the host gates forwarding on the negotiated version), so it stays compatible.
    //
    // NOTE: Under the pipe protocol the host installs a no-op output device for regular output
    // regardless of the negotiated protocol version (the SDK's TerminalTestReporter owns user-facing
    // output). The sole exception is when running on an Azure DevOps agent with a negotiated version of
    // 1.2.0 or later: the host then installs a forwarder that still discards regular output but relays
    // Azure DevOps logging commands as AzureDevOpsLogMessage (see OutputDeviceManager.BuildAsync).
    // With an old SDK that only supports 1.0.0, both sides will produce no live output (the SDK
    // suppresses its TerminalTestReporter to avoid colliding with the host output it expected before
    // this change). Users must update to an SDK that negotiates 1.1.0 to see live output via the SDK's
    // TerminalTestReporter.
    internal const string SupportedVersions = "1.0.0;1.1.0;1.2.0";
}
