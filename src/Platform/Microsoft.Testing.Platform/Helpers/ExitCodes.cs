// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

/// <summary>
/// We use positive exit codes for failure because POSIX/BASH exit codes are unsigned 8-bit integers.
/// On POSIX systems the standard exit code is 0 for success and any number from 1 to 255 for anything else.
/// </summary>
internal static class ExitCodes
{
    public const int Success = 0;
    public const int GenericFailure = 1;
    public const int AtLeastOneTestFailed = 2;
    public const int TestSessionAborted = 3;
    public const int InvalidPlatformSetup = 4;
    public const int InvalidCommandLine = 5;
    public const int FeatureNotImplemented = 6;
    public const int TestHostProcessExitedNonGracefully = 7;
    public const int ZeroTests = 8;
    public const int MinimumExpectedTestsPolicyViolation = 9;
    public const int TestAdapterTestSessionFailure = 10;
    public const int DependentProcessExited = 11;
    public const int IncompatibleProtocolVersion = 12;
    public const int TestExecutionStoppedForMaxFailedTests = 13;
}
