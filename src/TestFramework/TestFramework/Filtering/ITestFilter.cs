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
/// level. The MSTest adapter materializes the filter lazily on the first test invocation for that
/// assembly using the public parameterless constructor; the same instance is then reused for
/// every test in the assembly.
/// </para>
/// <para>
/// Implementations should be allocation-free and thread-safe; <see cref="Filter"/> may be
/// invoked concurrently for tests in different classes.
/// </para>
/// <para>
/// <strong>Ordering with built-in filtering:</strong> <see cref="ITestFilter"/> is composed with —
/// not a replacement for — the adapter's default filtering. Adapter-level filters such as the
/// VSTest <c>--filter</c> command line or test-explorer selection run <em>before</em>
/// <see cref="ITestFilter"/>, so <see cref="Filter"/> only ever sees tests that already survived
/// those gates. By contrast, <c>[Ignore]</c> is evaluated <em>after</em> <see cref="ITestFilter"/>
/// (it requires loading the declaring type, which <see cref="ITestFilter"/> is specifically designed
/// to avoid). Returning <see cref="TestFilterResult.Run"/> does not override a later
/// <c>[Ignore]</c>; an ignored test is still ignored.
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
    /// <remarks>
    /// If this method throws, the test is reported as Error with diagnostic <c>UTA078</c>; the
    /// exception is never silently swallowed. Implementations that want to opt the test in to
    /// running on failure should catch their own exceptions and return <see cref="TestFilterResult.Run"/>.
    /// </remarks>
    TestFilterResult Filter(TestFilterContext context);
}
