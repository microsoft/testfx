// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.Helpers;

[Embedded]
[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Use nameof pattern")]
internal static class EnvironmentVariableConstants
{
    public const string DOTNET_WATCH = nameof(DOTNET_WATCH);
    public const string TESTINGPLATFORM_HOTRELOAD_ENABLED = nameof(TESTINGPLATFORM_HOTRELOAD_ENABLED);
    public const string TESTINGPLATFORM_DEFAULT_HANG_TIMEOUT = nameof(TESTINGPLATFORM_DEFAULT_HANG_TIMEOUT);
    public const string TESTINGPLATFORM_MESSAGEBUS_DRAINDATA_ATTEMPTS = nameof(TESTINGPLATFORM_MESSAGEBUS_DRAINDATA_ATTEMPTS);

    // Overrides, in seconds, how long the message bus shutdown handshake waits for a consumer to finish once
    // the run has already been canceled. See ShutdownTimeouts.
    public const string TESTINGPLATFORM_MESSAGEBUS_CANCELED_SHUTDOWN_TIMEOUT_SECONDS = nameof(TESTINGPLATFORM_MESSAGEBUS_CANCELED_SHUTDOWN_TIMEOUT_SECONDS);

    // Overrides the directory where the IPC named pipe (Unix domain socket) files are created.
    // Only honored on Unix; Windows named pipes live in the kernel namespace, not on disk.
    public const string TESTINGPLATFORM_PIPE_DIRECTORY = nameof(TESTINGPLATFORM_PIPE_DIRECTORY);

    public const string TESTINGPLATFORM_TESTHOSTCONTROLLER_SKIPEXTENSION = nameof(TESTINGPLATFORM_TESTHOSTCONTROLLER_SKIPEXTENSION);
    public const string TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME = nameof(TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME);
    public const string TESTINGPLATFORM_TESTHOSTCONTROLLER_CONTROLPIPENAME = nameof(TESTINGPLATFORM_TESTHOSTCONTROLLER_CONTROLPIPENAME);
    public const string TESTINGPLATFORM_TESTHOSTCONTROLLER_CORRELATIONID = nameof(TESTINGPLATFORM_TESTHOSTCONTROLLER_CORRELATIONID);
    public const string TESTINGPLATFORM_TESTHOSTCONTROLLER_PARENTPID = nameof(TESTINGPLATFORM_TESTHOSTCONTROLLER_PARENTPID);
    public const string TESTINGPLATFORM_TESTHOSTCONTROLLER_TESTHOSTPROCESSSTARTTIME = nameof(TESTINGPLATFORM_TESTHOSTCONTROLLER_TESTHOSTPROCESSSTARTTIME);

    // Overrides, in seconds, the total post-exit budget for controller callbacks, output, and disposal.
    public const string TESTINGPLATFORM_TESTHOSTCONTROLLER_FINALIZATION_TIMEOUT_SECONDS = nameof(TESTINGPLATFORM_TESTHOSTCONTROLLER_FINALIZATION_TIMEOUT_SECONDS);

    public const string TESTINGPLATFORM_DIAGNOSTIC = nameof(TESTINGPLATFORM_DIAGNOSTIC);
    public const string TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY = nameof(TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY);
    public const string TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_DIRECTORY = nameof(TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_DIRECTORY);

    [Obsolete("Use " + nameof(TESTINGPLATFORM_DIAGNOSTIC_FILE_PREFIX) + " instead. This name matches the renamed --diagnostic-file-prefix CLI option (see https://github.com/microsoft/testfx/issues/7159). Kept for backward compatibility and may be removed in a future major version.", error: false)]
    public const string TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_FILEPREFIX = nameof(TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_FILEPREFIX);
    public const string TESTINGPLATFORM_DIAGNOSTIC_FILE_PREFIX = nameof(TESTINGPLATFORM_DIAGNOSTIC_FILE_PREFIX);

    [Obsolete("Use " + nameof(TESTINGPLATFORM_DIAGNOSTIC_SYNCHRONOUS_WRITE) + " instead. This name matches the renamed --diagnostic-synchronous-write CLI option (see https://github.com/microsoft/testfx/issues/7159). Kept for backward compatibility and may be removed in a future major version.", error: false)]
    public const string TESTINGPLATFORM_DIAGNOSTIC_FILELOGGER_SYNCHRONOUSWRITE = nameof(TESTINGPLATFORM_DIAGNOSTIC_FILELOGGER_SYNCHRONOUSWRITE);
    public const string TESTINGPLATFORM_DIAGNOSTIC_SYNCHRONOUS_WRITE = nameof(TESTINGPLATFORM_DIAGNOSTIC_SYNCHRONOUS_WRITE);
    public const string TESTINGPLATFORM_NOBANNER = nameof(TESTINGPLATFORM_NOBANNER);
    public const string TESTINGPLATFORM_EXITCODE_IGNORE = nameof(TESTINGPLATFORM_EXITCODE_IGNORE);

    // Telemetry
    public const string TESTINGPLATFORM_TELEMETRY_OPTOUT = nameof(TESTINGPLATFORM_TELEMETRY_OPTOUT);
    public const string DOTNET_CLI_TELEMETRY_OPTOUT = nameof(DOTNET_CLI_TELEMETRY_OPTOUT);
    public const string DOTNET_NOLOGO = nameof(DOTNET_NOLOGO);

    // OpenTelemetry
    // W3C trace context of the process that started this test run, so the run nests under it.
    public const string TRACEPARENT = nameof(TRACEPARENT);
    public const string TRACESTATE = nameof(TRACESTATE);
    public const string TESTINGPLATFORM_TRACEPARENT = nameof(TESTINGPLATFORM_TRACEPARENT);
    public const string TESTINGPLATFORM_TRACESTATE = nameof(TESTINGPLATFORM_TRACESTATE);

    // Opts out of capturing potentially large or sensitive test output (stdout/stderr) as span attributes.
    public const string TESTINGPLATFORM_OTEL_CAPTURE_TEST_OUTPUT = nameof(TESTINGPLATFORM_OTEL_CAPTURE_TEST_OUTPUT);

    // Maximum number of characters kept for a single captured output/stack trace attribute.
    public const string TESTINGPLATFORM_OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT = nameof(TESTINGPLATFORM_OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT);

    // Opts out of emitting the pre-4.x attribute and instrument names alongside the semantic-convention ones.
    public const string TESTINGPLATFORM_OTEL_EMIT_LEGACY_ATTRIBUTES = nameof(TESTINGPLATFORM_OTEL_EMIT_LEGACY_ATTRIBUTES);

    // Debugging
    public const string TESTINGPLATFORM_LAUNCH_ATTACH_DEBUGGER = nameof(TESTINGPLATFORM_LAUNCH_ATTACH_DEBUGGER);
    public const string TESTINGPLATFORM_WAIT_ATTACH_DEBUGGER = nameof(TESTINGPLATFORM_WAIT_ATTACH_DEBUGGER);

    // dotnet test
    public const string TESTINGPLATFORM_DOTNETTEST_EXECUTIONID = nameof(TESTINGPLATFORM_DOTNETTEST_EXECUTIONID);

    // Carries the 1-based retry attempt number from the retry orchestrator to each launched test host, which
    // then reports it back to dotnet test through the AttemptNumber handshake property.
    public const string TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER = nameof(TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER);
    public const string DOTNET_CLI_TEST_COMMAND_WORKING_DIRECTORY = nameof(DOTNET_CLI_TEST_COMMAND_WORKING_DIRECTORY);

    // Unhandled Exception
    public const string TESTINGPLATFORM_EXIT_PROCESS_ON_UNHANDLED_EXCEPTION = nameof(TESTINGPLATFORM_EXIT_PROCESS_ON_UNHANDLED_EXCEPTION);

    // Correlates the processes that make up one logical test run, so report formats that can describe a run
    // spanning several documents (CTRF 'runId') can tie those documents back together. The retry orchestrator
    // sets it before launching its attempts, and any process tree started from there inherits it.
    //
    // NOTE: a multi-project 'dotnet test' does NOT set this. Each module is a separate root test application
    // with its own execution id (see docs/mstest-runner-protocol/004-protocol-dotnet-test-pipe.md), so its
    // modules are distinct execution trees. Correlating them — or correlating shards running on different
    // machines — requires setting this variable explicitly before launching them.
    public const string TESTINGPLATFORM_LOGICAL_RUN_ID = nameof(TESTINGPLATFORM_LOGICAL_RUN_ID);

    // Trx
    public const string TESTINGPLATFORM_TRX_TESTRUN_ID = nameof(TESTINGPLATFORM_TRX_TESTRUN_ID);

    // Deadline-aware cancellation. TESTINGPLATFORM_DEADLINE is an absolute wall-clock instant
    // (ISO 8601 round-trip, parsed to UTC) that the CI runner will hard-cancel the process at.
    // The margins are the lead time before the deadline at which the platform reacts: graceful
    // stop (stop scheduling new tests, let reporters finalize) and hang dump (out-of-proc dump).
    public const string TESTINGPLATFORM_DEADLINE = nameof(TESTINGPLATFORM_DEADLINE);
    public const string TESTINGPLATFORM_DEADLINE_STOP_MARGIN = nameof(TESTINGPLATFORM_DEADLINE_STOP_MARGIN);
    public const string TESTINGPLATFORM_DEADLINE_DUMP_MARGIN = nameof(TESTINGPLATFORM_DEADLINE_DUMP_MARGIN);
}
