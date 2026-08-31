// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using MessageLevel = Microsoft.VisualStudio.TestTools.UnitTesting.MessageLevel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// A dependency edge declared in <c>testconfig.json</c> rather than by a <c>[DependsOn]</c> attribute:
/// <see cref="Dependent"/> runs after <see cref="Prerequisite"/>.
/// </summary>
/// <remarks>
/// <para>
/// Declaring dependencies in configuration exists for the cases the attribute cannot serve - orchestrating
/// tests somebody else owns, keeping a whole pipeline visible in one reviewable place, or letting a role
/// that does not edit code define the run. It is the same idea as TestNG's <c>testng.xml</c> or
/// Playwright's project <c>dependencies</c>, expressed in the configuration file MSTest already has.
/// </para>
/// <para>
/// A test is referenced by <c>Namespace.Class.Method</c>, or by <c>Namespace.Class.*</c> to mean every test
/// of a class. Configured edges are merged with attribute-declared ones; neither overrides the other.
/// </para>
/// </remarks>
#if NETFRAMEWORK
[Serializable]
#endif
internal sealed class TestDependencyDeclaration
{
    public TestDependencyDeclaration(string dependent, string prerequisite, bool proceedOnFailure)
    {
        Dependent = dependent;
        Prerequisite = prerequisite;
        ProceedOnFailure = proceedOnFailure;
    }

    /// <summary>Gets the reference to the test that runs second.</summary>
    public string Dependent { get; }

    /// <summary>Gets the reference to the test that must run first.</summary>
    public string Prerequisite { get; }

    /// <summary>Gets a value indicating whether the dependent runs even when the prerequisite does not pass.</summary>
    public bool ProceedOnFailure { get; }

    /// <summary>
    /// Reports the declarations whose diagnostics can only be judged against the whole run: a malformed
    /// reference, and a dependent that matches no test anywhere. Kept separate from <see cref="ApplyAll"/>,
    /// which runs once per source and therefore cannot tell "this names a test in another assembly" from
    /// "this names nothing at all".
    /// </summary>
    public static void ReportUnmatchedDeclarations(IEnumerable<TestDependencyDeclaration> declarations, IEnumerable<UnitTestElement> allTests, IAdapterMessageLogger? logger)
    {
        if (logger is null)
        {
            return;
        }

        UnitTestElement[] tests = [.. allTests];
        foreach (TestDependencyDeclaration declaration in declarations)
        {
            if (!TestReference.TryParse(declaration.Dependent, out TestReference? dependent))
            {
                logger.SendMessage(
                    MessageLevel.Warning,
                    string.Format(CultureInfo.CurrentCulture, Resource.DependencyConfigurationInvalidReference, declaration.Dependent));
                continue;
            }

            if (!TestReference.TryParse(declaration.Prerequisite, out TestReference? _))
            {
                logger.SendMessage(
                    MessageLevel.Warning,
                    string.Format(CultureInfo.CurrentCulture, Resource.DependencyConfigurationInvalidReference, declaration.Prerequisite));
                continue;
            }

            if (!tests.Any(dependent.Matches))
            {
                logger.SendMessage(
                    MessageLevel.Warning,
                    string.Format(CultureInfo.CurrentCulture, Resource.DependencyConfigurationDependentNotFound, declaration.Dependent));
            }
        }
    }

    /// <summary>
    /// Applies <paramref name="declarations"/> to the tests they name, so that from here on a configured
    /// edge is indistinguishable from one declared by an attribute.
    /// </summary>
    /// <returns><see langword="true"/> when at least one edge was applied.</returns>
    public static bool ApplyAll(IEnumerable<TestDependencyDeclaration> declarations, UnitTestElement[] tests, IAdapterMessageLogger? adapterMessageLogger)
    {
        bool applied = false;
        foreach (TestDependencyDeclaration declaration in declarations)
        {
            if (!TestReference.TryParse(declaration.Dependent, out TestReference? dependent))
            {
                continue;
            }

            if (!TestReference.TryParse(declaration.Prerequisite, out TestReference? prerequisite))
            {
                continue;
            }

            foreach (UnitTestElement test in tests)
            {
                if (!dependent.Matches(test))
                {
                    continue;
                }

                // A wildcard dependent (Ns.Class.*) expands onto every test of the class, including the
                // prerequisite itself when that is also named in the class. The user wrote "every test of
                // this class waits for Setup", never "Setup waits for itself", so that generated self-edge is
                // dropped - exactly as discovery drops it for a class-level [DependsOn]. Without this, Setup
                // would be reported as a cycle and the whole class skipped.
                //
                // Only a *specific* prerequisite is suppressed this way. When the prerequisite is itself a
                // whole class, dropping the edge here would silently discard the entire declaration; those
                // edges go to the graph, which removes just each test's own self-edge and reports whatever
                // genuine cycle remains.
                if (dependent.MethodName is null && prerequisite.MethodName is not null && prerequisite.Matches(test))
                {
                    continue;
                }

                // The prerequisite is stored as declared rather than resolved to concrete tests here: the
                // graph already knows how to expand a class-wide target and how to report one that matches
                // nothing, and doing it in one place keeps both sources of edges behaving identically.
                var dependency = new TestDependencyInfo(prerequisite.ClassName, prerequisite.MethodName, declaration.ProceedOnFailure);
                test.Dependencies = test.Dependencies is { Length: > 0 } existing
                    ? [.. existing, dependency]
                    : [dependency];
                applied = true;
            }
        }

        return applied;
    }

    /// <summary>
    /// A reference to a test, or to every test of a class, written as <c>Namespace.Class.Method</c> or
    /// <c>Namespace.Class.*</c>.
    /// </summary>
    private sealed class TestReference
    {
        private TestReference(string className, string? methodName)
        {
            ClassName = className;
            MethodName = methodName;
        }

        public string ClassName { get; }

        /// <summary>Gets the method name, or <see langword="null"/> when the reference covers a whole class.</summary>
        public string? MethodName { get; }

        public static bool TryParse(string value, [NotNullWhen(true)] out TestReference? reference)
        {
            string trimmed = value.Trim();
            int lastDot = trimmed.LastIndexOf('.');

            // Both parts are required: a bare identifier could be a class or a method, and guessing would
            // silently point the edge at the wrong thing.
            if (lastDot <= 0 || lastDot == trimmed.Length - 1)
            {
                reference = null;
                return false;
            }

            string className = trimmed.Substring(0, lastDot);
            string methodName = trimmed.Substring(lastDot + 1);
            reference = methodName == "*" ? new TestReference(className, null) : new TestReference(className, methodName);
            return true;
        }

        public bool Matches(UnitTestElement element)
            => string.Equals(element.TestMethod.FullClassName, ClassName, StringComparison.Ordinal)
                && (MethodName is null || string.Equals(element.TestMethod.Name, MethodName, StringComparison.Ordinal));
    }
}
