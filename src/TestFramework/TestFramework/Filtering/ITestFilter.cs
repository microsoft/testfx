// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Implemented by a user-supplied test filter that decides, on a per-test basis, whether the test
/// should run, be silently dropped, or be reported as skipped.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are registered via <see cref="TestFilterProviderAttribute"/> at the assembly
/// level. The MSTest adapter creates one instance per assembly per test run (via the public
/// parameterless constructor) and invokes <see cref="Filter"/> for every test that survived the
/// command-line filter, before that test's type is loaded.
/// </para>
/// <para>
/// Implementations should be allocation-free and thread-safe; they may be called concurrently for
/// tests in different classes.
/// </para>
/// </remarks>
public interface ITestFilter
{
    /// <summary>
    /// Decides whether the test described by <paramref name="context"/> should run.
    /// </summary>
    /// <param name="context">Metadata describing the test under consideration.</param>
    /// <returns>
    /// <see cref="TestFilterResult.Run"/> to let the test execute normally,
    /// <see cref="TestFilterResult.Drop"/> to silently drop the test (no result emitted), or
    /// <see cref="TestFilterResult.Skip(string)"/> to report the test as Skipped with a reason.
    /// </returns>
    TestFilterResult Filter(TestFilterContext context);
}
