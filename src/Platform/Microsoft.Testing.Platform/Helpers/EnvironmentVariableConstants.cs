﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.Helpers;

[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Use nameof pattern")]
internal static class EnvironmentVariableConstants
{
    public const string DOTNET_WATCH = nameof(DOTNET_WATCH);
    public const string TESTINGPLATFORM_HOTRELOAD_ENABLED = nameof(TESTINGPLATFORM_HOTRELOAD_ENABLED);
    public const string TESTINGPLATFORM_DEFAULT_HANG_TIMEOUT = nameof(TESTINGPLATFORM_DEFAULT_HANG_TIMEOUT);
    public const string TESTINGPLATFORM_MESSAGEBUS_DRAINDATA_ATTEMPTS = nameof(TESTINGPLATFORM_MESSAGEBUS_DRAINDATA_ATTEMPTS);

    public const string TESTINGPLATFORM_TESTHOSTCONTROLLER_SKIPEXTENSION = nameof(TESTINGPLATFORM_TESTHOSTCONTROLLER_SKIPEXTENSION);
    public const string TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME = nameof(TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME);
    public const string TESTINGPLATFORM_TESTHOSTCONTROLLER_CORRELATIONID = nameof(TESTINGPLATFORM_TESTHOSTCONTROLLER_CORRELATIONID);
    public const string TESTINGPLATFORM_TESTHOSTCONTROLLER_PARENTPID = nameof(TESTINGPLATFORM_TESTHOSTCONTROLLER_PARENTPID);
    public const string TESTINGPLATFORM_TESTHOSTCONTROLLER_TESTHOSTPROCESSSTARTTIME = nameof(TESTINGPLATFORM_TESTHOSTCONTROLLER_TESTHOSTPROCESSSTARTTIME);

    public const string TESTINGPLATFORM_DIAGNOSTIC = nameof(TESTINGPLATFORM_DIAGNOSTIC);
    public const string TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY = nameof(TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY);
    public const string TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_DIRECTORY = nameof(TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_DIRECTORY);
    public const string TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_FILEPREFIX = nameof(TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_FILEPREFIX);
    public const string TESTINGPLATFORM_DIAGNOSTIC_FILELOGGER_SYNCHRONOUSWRITE = nameof(TESTINGPLATFORM_DIAGNOSTIC_FILELOGGER_SYNCHRONOUSWRITE);
    public const string TESTINGPLATFORM_NOBANNER = nameof(TESTINGPLATFORM_NOBANNER);
    public const string TESTINGPLATFORM_EXITCODE_IGNORE = nameof(TESTINGPLATFORM_EXITCODE_IGNORE);

    // Telemetry
    public const string TESTINGPLATFORM_TELEMETRY_OPTOUT = nameof(TESTINGPLATFORM_TELEMETRY_OPTOUT);
    public const string DOTNET_CLI_TELEMETRY_OPTOUT = nameof(DOTNET_CLI_TELEMETRY_OPTOUT);
    public const string DOTNET_NOLOGO = nameof(DOTNET_NOLOGO);

    // Debugging
    public const string TESTINGPLATFORM_LAUNCH_ATTACH_DEBUGGER = nameof(TESTINGPLATFORM_LAUNCH_ATTACH_DEBUGGER);

    // dotnet test
    public const string TESTINGPLATFORM_DOTNETTEST_EXECUTIONID = nameof(TESTINGPLATFORM_DOTNETTEST_EXECUTIONID);

    // Unhandled Exception
    public const string TESTINGPLATFORM_EXIT_PROCESS_ON_UNHANDLED_EXCEPTION = nameof(TESTINGPLATFORM_EXIT_PROCESS_ON_UNHANDLED_EXCEPTION);
}
