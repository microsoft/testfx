// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.Helpers;

/// <summary>
/// We use positive exit codes for failure because POSIX/BASH exit codes are unsigned 8-bit integers.
/// On POSIX systems the standard exit code is 0 for success and any number from 1 to 255 for anything else.
/// </summary>
[Embedded]
internal enum ExitCodes
{
    Success = 0,
    GenericFailure = 1,
    AtLeastOneTestFailed = 2,
    TestSessionAborted = 3,
    InvalidPlatformSetup = 4,
    InvalidCommandLine = 5,
    // FeatureNotImplemented = 6,
    TestHostProcessExitedNonGracefully = 7,
    ZeroTests = 8,
    MinimumExpectedTestsPolicyViolation = 9,
    TestAdapterTestSessionFailure = 10,
    DependentProcessExited = 11,
    IncompatibleProtocolVersion = 12,
    TestExecutionStoppedForMaxFailedTests = 13,
}
