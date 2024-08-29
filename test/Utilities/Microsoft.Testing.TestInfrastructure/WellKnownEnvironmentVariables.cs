// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

public static class WellKnownEnvironmentVariables
{
    public static readonly string[] ToSkipEnvironmentVariables =
    [
        // Skip dotnet root, we redefine it below.
        "DOTNET_ROOT",

        // Skip all environment variables related to minidump functionality.
        // https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/xplat-minidump-generation.md
        "DOTNET_DbgEnableMiniDump",
        "DOTNET_DbgMiniDumpName",
        "DOTNET_CreateDumpDiagnostics",
        "DOTNET_CreateDumpVerboseDiagnostics",
        "DOTNET_CreateDumpLogToFile",
        "DOTNET_EnableCrashReport",
        "DOTNET_EnableCrashReportOnly",

        // Old syntax for the minidump functionality.
        "COMPlus_DbgEnableMiniDump",
        "COMPlus_DbgEnableElfDumpOnMacOS",
        "COMPlus_DbgMiniDumpName",
        "COMPlus_DbgMiniDumpType",

        // Hot reload mode
        "TESTINGPLATFORM_HOTRELOAD_ENABLED",

        // Telemetry
        // By default arcade set this environment variable
        "DOTNET_CLI_TELEMETRY_OPTOUT",
        "TESTINGPLATFORM_TELEMETRY_OPTOUT",
        "DOTNET_NOLOGO",
        "TESTINGPLATFORM_NOBANNER",

        // Diagnostics
        "TESTINGPLATFORM_DIAGNOSTIC",

        // dotnet test
        "TESTINGPLATFORM_DOTNETTEST_EXECUTIONID",

        // Isolate from the skip banner in case of parent, children tests
        "TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER"
    ];
}
