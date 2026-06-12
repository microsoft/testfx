// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Indicates how the MSTest adapter should treat a test for which an <see cref="ITestFilter"/>
/// returned a particular <see cref="TestFilterResult"/>.
/// </summary>
public enum TestFilterAction
{
    /// <summary>
    /// Run the test normally. This is the default action so that <c>default(TestFilterResult)</c>
    /// is the safe choice when an implementation forgets to set a result explicitly.
    /// </summary>
    Run = 0,

    /// <summary>
    /// Silently drop the test. No <see cref="TestResult"/> is emitted, the test does not appear in
    /// the test count, and the declaring class's <c>[ClassInitialize]</c> is not invoked unless
    /// another (non-dropped) test in the same class still has to run. This matches the semantics
    /// of the platform <c>--filter</c> command-line option.
    /// </summary>
    Drop,

    /// <summary>
    /// Report the test as Skipped, with the reason supplied to <see cref="TestFilterResult.Skip(string)"/>.
    /// The test appears in the test count, the TRX/console output, and IDE test explorers.
    /// </summary>
    Skip,
}
