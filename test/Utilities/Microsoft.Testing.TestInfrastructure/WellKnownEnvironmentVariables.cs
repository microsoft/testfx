// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

public static class WellKnownEnvironmentVariables
{
    /// <summary>
    /// Environment variables that the Microsoft.Testing.Platform LLM detector inspects.
    /// Keep in sync with <c>LLMEnvironmentDetector</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> LLMEnvironmentVariables =
    [
        "CLAUDECODE",
        "CLAUDE_CODE_ENTRYPOINT",
        "CURSOR_EDITOR",
        "CURSOR_AI",
        "GEMINI_CLI",
        "GITHUB_COPILOT_CLI_MODE",
        "GH_COPILOT_WORKING_DIRECTORY",
        "COPILOT_CLI",
        "CODEX_CLI",
        "CODEX_SANDBOX",
        "OR_APP_NAME",
        "AMP_HOME",
        "QWEN_CODE",
        "DROID_CLI",
        "OPENCODE_AI",
        "ZED_ENVIRONMENT",
        "ZED_TERM",
        "KIMI_CLI",
        "GOOSE_TERMINAL",
        "CLINE_TASK_ID",
        "ROO_CODE_TASK_ID",
        "WINDSURF_SESSION",
        "AGENT_CLI",
    ];

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
        "COMPlus_CreateDumpDiagnostics",
        "COMPlus_CreateDumpVerboseDiagnostics",
        "COMPlus_CreateDumpLogToFile",
        "COMPlus_EnableCrashReport",
        "COMPlus_EnableCrashReportOnly",

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
        "DOTNET_CLI_TEST_COMMAND_WORKING_DIRECTORY",

        // Isolate from the skip banner in case of parent, children tests
        "TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER",

        // LLM / AI agent CLI environment variables - keep in sync with
        // src/Platform/Microsoft.Testing.Platform/Helpers/LLMEnvironmentDetector.cs.
        // We filter these out so acceptance tests are not affected by the ambient
        // shell the developer (or CI) happens to be running them from. Tests that
        // need to exercise LLM-aware behavior must set the relevant variable explicitly.
        .. LLMEnvironmentVariables,
    ];
}
