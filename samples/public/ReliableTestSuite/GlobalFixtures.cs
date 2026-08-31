// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ReliableTestSuite;

/// <summary>
/// SUITE-WIDE FIXTURES - and the distinction people most often get wrong.
///
/// There are two assembly-scoped lifecycle hooks, and they run at very different cadences:
///
///   [AssemblyInitialize] / [AssemblyCleanup] run exactly ONCE per assembly - before the first
///   test starts and after the last test finishes. This is where once-only suite bootstrapping
///   belongs (start a shared server, seed a database, warm a cache). [AssemblyInitialize] takes a
///   TestContext; [AssemblyCleanup] may optionally take one.
///
///   [GlobalTestInitialize] / [GlobalTestCleanup] are the assembly-wide equivalent of
///   [TestInitialize] / [TestCleanup]: they run before and after EVERY test in the assembly,
///   independent of any single class. Use them to reset ambient state per test - NOT for
///   once-only setup. Both must be public static and take a TestContext.
///
/// Putting expensive once-only work in a per-test hook (or expecting once-per-run semantics from
/// [GlobalTestInitialize]) is a classic scaling mistake; naming both here makes the difference
/// explicit so a reader copies the right one.
/// </summary>
[TestClass]
public static class GlobalFixtures
{
    // Runs ONCE, before any test - the right home for suite-wide bootstrapping.
    [AssemblyInitialize]
    public static void SuiteSetup(TestContext context)
        => context.WriteLine("Reliable suite starting - shared bootstrapping happens here, once.");

    // Runs ONCE, after every test has finished.
    [AssemblyCleanup]
    public static void SuiteTeardown(TestContext context)
        => context.WriteLine("Reliable suite finished.");

    // Runs before EVERY test (assembly-wide [TestInitialize]). Keep it cheap and idempotent; it
    // is not the place for once-only work. Here it is a no-op that documents the cadence.
    [GlobalTestInitialize]
    public static void BeforeEachTest(TestContext context)
    {
        // Per-test hook: e.g. reset a piece of ambient state so no test inherits another's leftovers.
    }
}
