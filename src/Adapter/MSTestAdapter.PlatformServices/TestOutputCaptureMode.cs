// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Controls how Console/Debug/Trace output produced while a test is running is handled.
/// </summary>
internal enum TestOutputCaptureMode
{
    /// <summary>
    /// Output is neither captured into the test result nor echoed live. It is discarded from the
    /// test result (equivalent to the legacy <c>CaptureTraceOutput=false</c> setting).
    /// </summary>
    None,

    /// <summary>
    /// Output is captured and attached to the test result, then surfaced once the test completes.
    /// This is the default (equivalent to the legacy <c>CaptureTraceOutput=true</c> setting).
    /// </summary>
    Result,

    /// <summary>
    /// Output is captured and attached to the test result (like <see cref="Result"/>) and is
    /// additionally echoed live to the original console as the test runs, similar to xUnit's
    /// <c>showLiveOutput</c> option.
    /// </summary>
    Live,
}
