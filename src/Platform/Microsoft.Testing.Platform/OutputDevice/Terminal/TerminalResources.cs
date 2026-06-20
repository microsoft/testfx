// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !IS_CORE_MTP
using System.Resources;
#endif

using Microsoft.CodeAnalysis;

#if !IS_CORE_MTP
#pragma warning disable IDE0005 // Using directive is unnecessary.
using Microsoft.Testing.Platform.Builder;
#pragma warning restore IDE0005 // Using directive is unnecessary.
#endif

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

// Strongly-typed accessor for TerminalResources.resx; [Embedded] so it is per-assembly when shared. The
// !IS_CORE_MTP branch points the ResourceManager at the core MTP assembly for in-repo linked consumers.
[Embedded]
internal static partial class TerminalResources
{
#if !IS_CORE_MTP

    internal static ResourceManager ResourceManager => field ??= new ResourceManager("Microsoft.Testing.Platform.OutputDevice.Terminal.TerminalResources", typeof(ITestApplication).Assembly);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetResourceString(string resourceKey) => ResourceManager.GetString(resourceKey, null)!;

    internal static string @Aborted => GetResourceString("Aborted");

    internal static string @ActiveTestsRunning_FullTestsCount => GetResourceString("ActiveTestsRunning_FullTestsCount");

    internal static string @ActiveTestsRunning_MoreTestsCount => GetResourceString("ActiveTestsRunning_MoreTestsCount");

    internal static string @Actual => GetResourceString("Actual");

    internal static string @CancelledLowercase => GetResourceString("CancelledLowercase");

    internal static string @CancellingTestSession => GetResourceString("CancellingTestSession");

    internal static string @ConsoleIsAlreadyInBatchingMode => GetResourceString("ConsoleIsAlreadyInBatchingMode");

    internal static string @DiscoveringTestsFrom => GetResourceString("DiscoveringTestsFrom");

    internal static string @DurationLowercase => GetResourceString("DurationLowercase");

    internal static string @Error => GetResourceString("Error");

    internal static string @ExitCode => GetResourceString("ExitCode");

    internal static string @Expected => GetResourceString("Expected");

    internal static string @Failed => GetResourceString("Failed");

    internal static string @FailedLowercase => GetResourceString("FailedLowercase");

    internal static string @FailedWithErrors => GetResourceString("FailedWithErrors");

    internal static string @ForTest => GetResourceString("ForTest");

    internal static string @HandshakeFailuresHeader => GetResourceString("HandshakeFailuresHeader");

    internal static string @InProcessArtifactsProduced => GetResourceString("InProcessArtifactsProduced");

    internal static string @MinimumExpectedTestsPolicyViolation => GetResourceString("MinimumExpectedTestsPolicyViolation");

    internal static string @OutOfProcessArtifactsProduced => GetResourceString("OutOfProcessArtifactsProduced");

    internal static string @Passed => GetResourceString("Passed");

    internal static string @PassedLowercase => GetResourceString("PassedLowercase");

    internal static string @PressCtrlCAgainToForceExit => GetResourceString("PressCtrlCAgainToForceExit");

    internal static string @Retried => GetResourceString("Retried");

    internal static string @RunningTestsFrom => GetResourceString("RunningTestsFrom");

    internal static string @SkippedLowercase => GetResourceString("SkippedLowercase");

    internal static string @StackFrameAt => GetResourceString("StackFrameAt");

    internal static string @StackFrameIn => GetResourceString("StackFrameIn");

    internal static string @StandardError => GetResourceString("StandardError");

    internal static string @StandardOutput => GetResourceString("StandardOutput");

    internal static string @SucceededLowercase => GetResourceString("SucceededLowercase");

    internal static string @TerminalAnsiOptionDescription => GetResourceString("TerminalAnsiOptionDescription");

    internal static string @TerminalAnsiOptionInvalidArgument => GetResourceString("TerminalAnsiOptionInvalidArgument");

    internal static string @TerminalNoAnsiOptionDescription => GetResourceString("TerminalNoAnsiOptionDescription");

    internal static string @TerminalNoProgressOptionDescription => GetResourceString("TerminalNoProgressOptionDescription");

    internal static string @TerminalOutputOptionDescription => GetResourceString("TerminalOutputOptionDescription");

    internal static string @TerminalOutputOptionInvalidArgument => GetResourceString("TerminalOutputOptionInvalidArgument");

    internal static string @TerminalProgressHeartbeat => GetResourceString("TerminalProgressHeartbeat");

    internal static string @TerminalProgressHeartbeatActiveSuffix => GetResourceString("TerminalProgressHeartbeatActiveSuffix");

    internal static string @TerminalProgressOptionDescription => GetResourceString("TerminalProgressOptionDescription");

    internal static string @TerminalProgressOptionInvalidArgument => GetResourceString("TerminalProgressOptionInvalidArgument");

    internal static string @TerminalProgressSlowTest => GetResourceString("TerminalProgressSlowTest");

    internal static string @TerminalShowOutputOptionInvalidArgument => GetResourceString("TerminalShowOutputOptionInvalidArgument");

    internal static string @TerminalShowStderrOptionDescription => GetResourceString("TerminalShowStderrOptionDescription");

    internal static string @TerminalShowStdoutOptionDescription => GetResourceString("TerminalShowStdoutOptionDescription");

    internal static string @TerminalTestReporterDescription => GetResourceString("TerminalTestReporterDescription");

    internal static string @TerminalTestReporterDisplayName => GetResourceString("TerminalTestReporterDisplayName");

    internal static string @TestDiscoverySummarySingular => GetResourceString("TestDiscoverySummarySingular");

    internal static string @TestRunSummary => GetResourceString("TestRunSummary");

    internal static string @TotalLowercase => GetResourceString("TotalLowercase");

    internal static string @Try => GetResourceString("Try");

    internal static string @ZeroTestsRan => GetResourceString("ZeroTestsRan");

#endif
}
