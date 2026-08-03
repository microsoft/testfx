// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ReliableTestSuite;

/// <summary>
/// STEP 5 - RETRY IS CONTAINMENT, NOT A FIX.
///
/// [Retry] re-runs a failed test up to N times. Read that plainly: it makes a nondeterministic
/// test PASS more often - it does not make it deterministic. Every previous step in this sample
/// removes a source of nondeterminism (isolation, resource coordination, declarative gating,
/// cooperative timeouts). Retry is the last resort for residual flakiness you have not yet been
/// able to eliminate - for example a genuinely external dependency you do not control.
///
/// Use it deliberately and visibly, never as the default. Under Microsoft.Testing.Platform each
/// attempt is reported, so a retried test still shows up as "flaky" rather than a clean green -
/// that visibility is the point. If you find yourself adding [Retry] to hide a race in your OWN
/// code, stop and fix the race; the earlier steps are how.
///
/// (Also distinct from the Microsoft.Testing.Extensions.Retry orchestrator's
/// --retry-failed-tests, which re-runs failed tests in a fresh host process. If you combine
/// them, the attempt counts multiply.)
/// </summary>
[TestClass]
public sealed class FlakyDependencyTests
{
    private static int s_attempts;

    // Simulates a flaky *external* dependency (e.g. a remote service) that fails the first time
    // and then succeeds. In real code the earlier steps cannot remove this - it is outside the
    // process - so retry is the honest containment tool.
    private static bool CallExternalService()
        => Interlocked.Increment(ref s_attempts) >= 2;

    [TestMethod]
    [Retry(3, MillisecondsDelayBetweenRetries = 50, BackoffType = DelayBackoffType.Exponential)]
    public void ExternalCall_EventuallySucceeds()
    {
        // NOTE: this passing on retry does NOT make the dependency deterministic. It contains
        // the flakiness so unrelated tests are not blocked; the durable fix is a test double.
        Assert.IsTrue(CallExternalService(), "External dependency was not available on this attempt.");
    }
}
