// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ReliableTestSuite;

/// <summary>
/// Per-assembly setup/teardown that runs once, regardless of how many test classes exist.
/// [GlobalTestInitialize]/[GlobalTestCleanup] methods must be public static and take a
/// TestContext. Unlike [AssemblyInitialize], they are not tied to a single class, so shared
/// suite-wide bootstrapping lives in one obvious place.
/// </summary>
[TestClass]
public static class GlobalFixtures
{
    [GlobalTestInitialize]
    public static void SuiteSetup(TestContext context)
        => context.WriteLine("Reliable suite starting - environment is deterministic from here.");

    [GlobalTestCleanup]
    public static void SuiteTeardown(TestContext context)
        => context.WriteLine("Reliable suite finished.");
}
